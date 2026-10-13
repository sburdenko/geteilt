using Geteilt.Domain;
using Geteilt.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(User.MaxEmailLength)
            .IsRequired();

        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(User.MaxDisplayNameLength)
            .IsRequired();


        builder.Property(user => user.PreferredCurrency)
            .HasColumnType($"char({Currency.CodeLength})")
            .IsRequired();

        // Soft deleted rows are filtered out everywhere by default; admin paths use
        // IgnoreQueryFilters.
        builder.HasQueryFilter(user => user.DeletedAt == null);
    }
}
