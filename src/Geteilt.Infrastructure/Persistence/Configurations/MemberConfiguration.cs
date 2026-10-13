using Geteilt.Domain.Groups;
using Geteilt.Domain.Members;
using Geteilt.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Geteilt.Infrastructure.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(member => member.Id);

        builder.Property(member => member.DisplayName)
            .IsRequired()
            .HasMaxLength(Member.MaxDisplayNameLength);

        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(member => member.GroupId);

        // Partial: one person cannot join the same group twice, but any number of members may
        // have no account at all, and NULLs would otherwise collide under a plain unique index.
        builder.HasIndex(member => new { member.GroupId, member.UserId })
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");

        // Answers "which groups am I in", and skips the members who never had an account.
        builder.HasIndex(member => member.UserId).HasFilter("user_id IS NOT NULL");

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(member => member.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not cascade: deleting a person must not silently erase them from the expense
        // history of a group, which would break the "balances sum to zero" invariant.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
