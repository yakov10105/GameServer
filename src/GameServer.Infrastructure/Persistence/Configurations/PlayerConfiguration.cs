using GameServer.Domain.Entities;

namespace GameServer.Infrastructure.Persistence.Configurations;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(p => p.DeviceId)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(p => p.DeviceId)
            .IsUnique();

        builder.Property(p => p.LastLogin)
            .IsRequired();

        builder.HasMany(p => p.Resources)
            .WithOne(r => r.Player)
            .HasForeignKey(r => r.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Resources)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

