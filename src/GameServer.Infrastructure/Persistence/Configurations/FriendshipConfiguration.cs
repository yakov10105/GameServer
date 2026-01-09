using GameServer.Domain.Entities;

namespace GameServer.Infrastructure.Persistence.Configurations;

public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("Friendships");

        builder.HasKey(f => new { f.PlayerId1, f.PlayerId2 });

        builder.Property(f => f.PlayerId1)
            .IsRequired();

        builder.Property(f => f.PlayerId2)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.HasOne(f => f.Player1)
            .WithMany()
            .HasForeignKey(f => f.PlayerId1)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Player2)
            .WithMany()
            .HasForeignKey(f => f.PlayerId2)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.PlayerId1);
        builder.HasIndex(f => f.PlayerId2);
    }
}

