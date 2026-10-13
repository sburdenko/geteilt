using Geteilt.Domain.Expenses;
using Geteilt.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseShareConfiguration : IEntityTypeConfiguration<ExpenseShare>
{
    public void Configure(EntityTypeBuilder<ExpenseShare> builder)
    {
        // Named explicitly: this entity is reached through Expense.Shares and has no DbSet of its
        // own, so the naming convention had nothing to pluralise and produced "expense_share".
        builder.ToTable("expense_shares");

        // Composite key: one member gets at most one share of an expense, and the database says so
        // rather than trusting the code that builds the list.
        builder.HasKey(share => new { share.ExpenseId, share.MemberId });

        builder.Property(share => share.ShareAmount).HasColumnType(MoneyColumns.Amount);
        builder.Property(share => share.ShareAmountInBaseCurrency).HasColumnType(MoneyColumns.Amount);

        // The weight the user actually typed — 2:1:1, or 33.33% — kept so editing an expense can
        // show the input rather than the computed cents.
        builder.Property(share => share.InputWeight).HasColumnType(MoneyColumns.Rate);

        builder.HasIndex(share => share.MemberId);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(share => share.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Zero is legal — a member can be part of an expense and owe nothing — but negative is not.
        builder.ToTable(table => table.HasCheckConstraint("ck_shares_non_negative", "share_amount >= 0"));
    }
}
