using Geteilt.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <summary>Hex of a SHA-256 digest is always this long.</summary>
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .HasColumnType($"char({HashLength})")
            .IsRequired();

        // Every redemption looks a token up by hash, and it must match at most one row.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Revoking a chain loads one user's tokens in a single query.
        builder.HasIndex(token => token.UserId);

        builder.Property(token => token.CreatedByIp).HasMaxLength(45);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
