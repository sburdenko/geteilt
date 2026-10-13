using Geteilt.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Infrastructure.Persistence.Configurations;

/// <summary>
/// Renames the Identity tables to snake_case.
/// </summary>
/// <remarks>
/// Identity hard-codes <c>ToTable("AspNetUsers")</c> in its own configuration, and an explicit
/// table name beats the naming convention — so without this the schema would be half snake_case
/// and half PascalCase, and every hand-written query against the Identity side would need double
/// quotes. That is precisely what the convention is there to avoid.
/// </remarks>
internal static class IdentityTableNames
{
    internal static void ApplyTo(ModelBuilder builder)
    {
        // "app_users" rather than "users": the domain user already owns that name.
        builder.Entity<AppUser>().ToTable("app_users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("app_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("app_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("app_user_tokens");
    }
}
