using System.ComponentModel.DataAnnotations;

namespace Geteilt.Infrastructure.Persistence;

/// <summary>
/// Bound to the <c>ConnectionStrings</c> section and validated at startup, so a missing connection
/// string stops the application immediately instead of surfacing on the first request.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(AllowEmptyStrings = false)]
    public string Postgres { get; init; } = string.Empty;
}
