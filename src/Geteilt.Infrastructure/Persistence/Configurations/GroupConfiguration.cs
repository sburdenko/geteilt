using Geteilt.Domain;
using Geteilt.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Name)
            .IsRequired()
            .HasMaxLength(Group.MaxNameLength);

        builder.Property(group => group.BaseCurrency)
            .IsRequired()
            .HasColumnType($"char({Currency.CodeLength})");

        // Duplicated from the domain on purpose: the domain guards against bugs in our code,
        // the CHECK guards against a hand-written UPDATE and a bad migration.
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_groups_currency_len",
            $"char_length(base_currency) = {Currency.CodeLength}"));
    }
}
