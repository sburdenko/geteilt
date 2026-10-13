using Geteilt.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        // The key the client chose is the primary key, together with the person it belongs to:
        // one client's key must never collide with — or be occupied by — another's.
        builder.HasKey(record => new { record.UserId, record.Key });

        builder.Property(record => record.Key).HasMaxLength(IdempotencyRecord.MaxKeyLength);
        builder.Property(record => record.Endpoint).HasMaxLength(200).IsRequired();
        builder.Property(record => record.RequestHash).HasColumnType($"char({HashLength})").IsRequired();

        // Cleaned up by TTL, so the sweep reads an index instead of the whole table.
        builder.HasIndex(record => record.CreatedAt);
    }
}
