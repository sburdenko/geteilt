using Geteilt.Application.Ports;
using Geteilt.Domain.Expenses;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Idempotency;
using Geteilt.Domain.Members;
using Geteilt.Domain.Settlements;
using Geteilt.Domain.Users;
using Geteilt.Infrastructure.Encryption;
using Geteilt.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Infrastructure.Persistence;

/// <summary>
/// One context for both halves of the system. Identity needs its own tables, and keeping them here
/// is what lets a registration write the login and the domain user inside a single transaction.
/// </summary>
/// <remarks>
/// Derives from <see cref="IdentityUserContext{TUser, TKey}"/> and not IdentityDbContext: the
/// latter adds three tables for Identity roles, and this application has no use for them.
/// Permissions here come from membership in a group, not from a role on an account.
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IbanCipher ibanCipher)
    : IdentityUserContext<AppUser, Guid>(options), IAppDbContext
{
    public DbSet<Group> Groups => Set<Group>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<Settlement> Settlements => Set<Settlement>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// The domain user, deliberately not called <c>Users</c>: that name is already taken by
    /// Identity's <see cref="AppUser"/> set. Two things in this system are called a user, and at
    /// every call site it should be obvious which one is meant.
    /// </summary>
    public DbSet<User> DomainUsers => Set<User>();

    // Explicit: IAppDbContext calls the domain table "Users" because that is what it is called from
    // the application's point of view, where Identity does not exist.
    DbSet<User> IAppDbContext.Users => DomainUsers;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity brings its own configuration; ours is applied on top.
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        Configurations.IdentityTableNames.ApplyTo(builder);
        Configurations.IbanColumns.ApplyTo(builder, ibanCipher);
    }
}
