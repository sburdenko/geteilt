using Geteilt.Domain.Members;
using Geteilt.Domain.Users;
using Geteilt.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Geteilt.Infrastructure.Persistence.Configurations;

/// <summary>
/// The two IBAN columns, encrypted on the way in and decrypted on the way out.
/// </summary>
/// <remarks>
/// Applied from the context rather than from the entity configurations: those are created by
/// <c>ApplyConfigurationsFromAssembly</c> without arguments, and the converter needs the cipher.
/// Values are only ever projected, never compared — a fresh nonce makes equal IBANs unequal in the
/// column, so a <c>WHERE iban = …</c> would silently find nothing.
/// </remarks>
internal static class IbanColumns
{
    internal static void ApplyTo(ModelBuilder builder, IbanCipher cipher)
    {
        // EF never hands a null to a converter: a missing IBAN stays NULL in the column.
        ValueConverter encrypted = new ValueConverter<string, string>(
            iban => cipher.Protect(iban),
            stored => cipher.Unprotect(stored));

        builder.Entity<Member>().Property(member => member.Iban)
            .HasMaxLength(IbanCipher.MaxStoredLength)
            .HasConversion(encrypted);
        builder.Entity<Member>().ToTable(table => table.HasCheckConstraint(MembersConstraint, ProtectedOnly));

        builder.Entity<User>().Property(user => user.Iban)
            .HasMaxLength(IbanCipher.MaxStoredLength)
            .HasConversion(encrypted);
        builder.Entity<User>().ToTable(table => table.HasCheckConstraint(UsersConstraint, ProtectedOnly));
    }

    internal const string MembersConstraint = "ck_members_iban_protected";

    internal const string UsersConstraint = "ck_users_iban_protected";

    // Duplicated from the cipher on purpose, like the other CHECKs: the converter
    // guards this code, the constraint guards everything else that can write — an old version
    // still running during a deploy, a hand-written UPDATE.
    private const string ProtectedOnly = "iban IS NULL OR iban LIKE 'v1:%'";
}
