using Geteilt.Domain;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Members;
using Geteilt.Domain.Settlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.HasKey(settlement => settlement.Id);

        builder.Property(settlement => settlement.Amount).HasColumnType(MoneyColumns.Amount);
        builder.Property(settlement => settlement.AmountInBaseCurrency).HasColumnType(MoneyColumns.Amount);

        builder.Property(settlement => settlement.Currency)
            .IsRequired()
            .HasColumnType($"char({Currency.CodeLength})");

        builder.Property(settlement => settlement.Method).HasConversion<string>().HasMaxLength(16);

        builder.Property(settlement => settlement.Note).HasMaxLength(Settlement.MaxNoteLength);

        builder.HasIndex(settlement => new { settlement.GroupId, settlement.Date })
            .IsDescending(false, true)
            .HasFilter("deleted_at IS NULL");

        // Both directions are read when balances are computed, so both get an index.
        builder.HasIndex(settlement => settlement.FromMemberId);
        builder.HasIndex(settlement => settlement.ToMemberId);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(settlement => settlement.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(settlement => settlement.FromMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(settlement => settlement.ToMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_settlements_amount_positive", "amount > 0");

            // Paying yourself changes no balance and is always a mistake in the caller.
            table.HasCheckConstraint("ck_settlements_from_not_to", "from_member_id <> to_member_id");
        });

        builder.HasQueryFilter(settlement => settlement.DeletedAt == null);
    }
}
