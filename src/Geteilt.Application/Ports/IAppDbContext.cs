using Geteilt.Domain.Expenses;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Idempotency;
using Geteilt.Domain.Members;
using Geteilt.Domain.Settlements;
using Geteilt.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Application.Ports;

/// <summary>
/// The database as the application sees it: the domain tables and a way to commit.
/// </summary>
/// <remarks>
/// Exposing <see cref="DbSet{TEntity}"/> means this layer formally knows about EF Core, which is
/// a compromise made on purpose. The alternative — a repository per entity — would
/// duplicate what DbContext already is (a unit of work and a set of repositories) and leak LINQ
/// through badly shaped methods anyway.
///
/// Only domain entities appear here. Identity and refresh tokens are Infrastructure concerns and
/// stay out of reach.
/// </remarks>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Group> Groups { get; }

    DbSet<Member> Members { get; }

    DbSet<Expense> Expenses { get; }

    DbSet<Settlement> Settlements { get; }

    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
