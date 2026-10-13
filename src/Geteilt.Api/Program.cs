using System.Text.Json.Serialization;
using Geteilt.Api.Authentication;
using Geteilt.Api.Commands;
using Geteilt.Api.Cors;
using Geteilt.Api.Errors;
using Geteilt.Api.Health;
using Geteilt.Api.Idempotency;
using Geteilt.Api.RateLimiting;
using Geteilt.Api.ReverseProxy;
using Geteilt.Contracts;
using Geteilt.Infrastructure;
using Geteilt.Infrastructure.Encryption;
using Geteilt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// JSON to stdout: Docker collects it and a log aggregator can query the fields instead of
// grepping rendered sentences.
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// Registered for every controller rather than per action: a POST added later is protected
// without anyone remembering to ask for it, and opting out is explicit.
builder.Services
    .AddControllers(options => options.Filters.Add<IdempotencyFilter>())
    // The shape of the JSON comes from Contracts, so the server and every client are configured
    // from one place rather than from three opinions that happen to match.
    .AddJsonOptions(options =>
    {
        foreach (JsonConverter converter in GeteiltJson.Options.Converters)
        {
            options.JsonSerializerOptions.Converters.Add(converter);
        }
    });
builder.Services.AddHostedService<IdempotencyCleanupService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddGeteiltRateLimiting();
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["traceId"] = ErrorResponse.TraceIdOf(context.HttpContext);
});

// Order matters: the domain handler declines anything that is not a DomainException, and the
// unhandled one accepts everything, so it has to come last.
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

// Development only. In production the reverse proxy serves the browser client and the API from one
// origin, so no policy is needed — and a CORS policy that exists in
// production is one more thing to get wrong.
if (builder.Environment.IsDevelopment())
{
    string[] configuredOrigins =
        builder.Configuration.GetSection("Cors:DevelopmentOrigins").Get<string[]>() ?? [];

    builder.Services.AddCors(options => options.AddPolicy(
        DevelopmentCorsPolicy,
        policy => policy
            .SetIsOriginAllowed(origin => LocalNetworkOrigins.Allows(origin, configuredOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod()));
}

builder.Services.AddGeteiltReverseProxy(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionStringFactory: provider =>
            provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Postgres,
        name: "postgres",
        tags: ["ready"])
    .AddCheck<IbansProtectedHealthCheck>("ibans_protected", tags: ["ready"]);

WebApplication app = builder.Build();

if (await DeployCommands.TryRunAsync(app.Services, app.Logger, args))
{
    return;
}

// First: everything after it, the rate limiter included, has to see the real caller.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Development only, deliberately. In production a migration runs as its own deploy step:
    // applying it automatically means a failed migration takes the application down with it,
    // with nothing left running to roll back from.
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IbanBackfill>().RunAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevelopmentCorsPolicy);
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness runs no checks at all: it answers "the process is up", and a dead database must not
// make an orchestrator restart a perfectly healthy container.
// Not described in OpenAPI: health check endpoints produce no ApiExplorer description, so any
// OpenAPI metadata put on them would be silently ignored. They are listed in the README.
app.MapHealthChecks("/api/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();

/// <summary>Exposed so <c>WebApplicationFactory</c> can boot this application in tests.</summary>
public partial class Program
{
    private const string DevelopmentCorsPolicy = "development";
}
