using Geteilt.Domain;
using Geteilt.Domain.Expenses;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Description)
            .IsRequired()
            .HasMaxLength(Expense.MaxDescriptionLength);

        builder.Property(expense => expense.Amount).HasColumnType(MoneyColumns.Amount);
        builder.Property(expense => expense.AmountInBaseCurrency).HasColumnType(MoneyColumns.Amount);
        builder.Property(expense => expense.ExchangeRate).HasColumnType(MoneyColumns.Rate);

        builder.Property(expense => expense.Currency)
            .IsRequired()
            .HasColumnType($"char({Currency.CodeLength})");

        builder.Property(expense => expense.SplitMethod).HasConversion<string>().HasMaxLength(16);

        // The feed reads newest first and pages by keyset, so the index has to be ordered the same
        // way the query is; id breaks ties between two expenses on one day.
        builder.HasIndex(expense => new { expense.GroupId, expense.Date, expense.Id })
            .IsDescending(false, true, true)
            .HasFilter("deleted_at IS NULL");

        builder.HasIndex(expense => expense.PaidByMemberId);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(expense => expense.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(expense => expense.PaidByMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(expense => expense.Shares)
            .WithOne()
            .HasForeignKey(share => share.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(expense => expense.Shares).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(table => table.HasCheckConstraint("ck_expenses_amount_positive", "amount > 0"));

        // Every query filters on this, so the filter lives here rather than in twenty call sites
        //. Admin paths opt out with IgnoreQueryFilters.
        builder.HasQueryFilter(expense => expense.DeletedAt == null);
    }
}
