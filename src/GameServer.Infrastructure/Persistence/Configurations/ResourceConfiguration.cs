using GameServer.Domain.Entities;

namespace GameServer.Infrastructure.Persistence.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");

        builder.HasKey(r => new { r.PlayerId, r.Type });

        builder.Property(r => r.PlayerId)
            .IsRequired();

        builder.Property(r => r.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Amount)
            .IsRequired();

        builder.HasOne(r => r.Player)
            .WithMany(p => p.Resources)
            .HasForeignKey(r => r.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

