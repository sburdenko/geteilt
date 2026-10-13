using Geteilt.Infrastructure.Persistence;
using Geteilt.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Geteilt.Infrastructure.Encryption;

/// <summary>
/// Encrypts IBANs that were stored before encryption existed, then has the database confirm that
/// none are left. Idempotent: an already protected value is left alone, so it is safe as
/// a step of every deploy.
/// </summary>
/// <remarks>
/// Plain SQL rather than entities: loading an entity would run the decrypting converter over a
/// plaintext value, which refuses on purpose.
/// </remarks>
public sealed class IbanBackfill(AppDbContext dbContext, IbanCipher cipher)
{
    private static readonly (string Table, string Constraint)[] Columns =
    [
        ("members", IbanColumns.MembersConstraint),
        ("users", IbanColumns.UsersConstraint),
    ];

    /// <returns>How many values were encrypted.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        cipher.EnsureKey();

        // All or nothing: a run that stops halfway leaves no question about which rows it reached.
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        int protectedCount = 0;

        foreach ((string table, string constraint) in Columns)
        {
            protectedCount += await ProtectTableAsync(table, cancellationToken);

            // The migration adds the constraint NOT VALID, so it only checks new writes until here.
#pragma warning disable EF1002 // Table and constraint names come from the fixed list above, never from input.
            await dbContext.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE {table} VALIDATE CONSTRAINT {constraint}", cancellationToken);
#pragma warning restore EF1002
        }

        await transaction.CommitAsync(cancellationToken);

        return protectedCount;
    }

    /// <summary>For the readiness check: any value left in plain text means the backfill was skipped.</summary>
    public async Task<int> CountUnprotectedAsync(CancellationToken cancellationToken)
    {
        int count = 0;

        foreach ((string table, _) in Columns)
        {
#pragma warning disable EF1002 // Table names come from the fixed list above, never from input.
            count += await dbContext.Database
                .SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM {table} WHERE {UnprotectedFilter}")
                .SingleAsync(cancellationToken);
#pragma warning restore EF1002
        }

        return count;
    }

    private const string UnprotectedFilter = "iban IS NOT NULL AND iban NOT LIKE 'v1:%'";

    private async Task<int> ProtectTableAsync(string table, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // Table names come from the fixed list above, never from input.
        List<PlainIban> plain = await dbContext.Database
            .SqlQueryRaw<PlainIban>($"SELECT id AS \"Id\", iban AS \"Iban\" FROM {table} WHERE {UnprotectedFilter}")
            .ToListAsync(cancellationToken);

        foreach (PlainIban row in plain)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"UPDATE {table} SET iban = {{0}} WHERE id = {{1}}",
                [cipher.Protect(row.Iban), row.Id],
                cancellationToken);
        }
#pragma warning restore EF1002

        return plain.Count;
    }

    private sealed record PlainIban(Guid Id, string Iban);
}
