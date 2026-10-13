using Geteilt.Application.Balances;
using Geteilt.Application.Expenses;
using Geteilt.Application.Groups;
using Geteilt.Application.Members;
using Geteilt.Application.Ports;
using Geteilt.Application.Settlements;
using Geteilt.Application.Users;
using Geteilt.Infrastructure.Encryption;
using Geteilt.Infrastructure.Identity;
using Geteilt.Infrastructure.Persistence;
using Geteilt.Infrastructure.QrCodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Geteilt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            DatabaseOptions database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(database.Postgres)
                .UseSnakeCaseNamingConvention();
        });

        services.AddOptions<IbanProtectionOptions>()
            .Bind(configuration.GetSection(IbanProtectionOptions.SectionName))
            .Validate(options => options.HasValidKey(), "IbanProtection:Key must be a 32-byte key in base64.")
            .ValidateOnStart();
        services.AddSingleton<IbanCipher>();
        services.AddScoped<IbanBackfill>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingletonTimeProvider();
        services.AddIdentityCore<AppUser>(ConfigurePolicy)
            .AddEntityFrameworkStores<AppDbContext>()
            .AddPasswordValidator<LetterRequiredPasswordValidator>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IGirocodeService, GirocodeService>();
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<IGirocodeRenderer, GirocodeRenderer>();

        return services;
    }

    /// <summary>Password and lockout policy.</summary>
    private static void ConfigurePolicy(IdentityOptions options)
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;

        // Off on purpose. Mixed case and punctuation raise irritation more than they raise
        // strength; length plus "a digit and a letter" is where the value is.
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        // Registered here rather than assumed: every timestamp in the domain comes from a caller,
        // and tests swap this for a fake clock.
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
