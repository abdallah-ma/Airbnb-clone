using AirbnbMVP.Models.Listings;
using AirbnbMVP.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirbnbMVP.DAL.Configurations.Listings;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id);

        builder.Property(l => l.Title).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Description).IsRequired();
        builder.Property(l => l.Address).IsRequired().HasMaxLength(500);
        builder.Property(l => l.City).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Country).IsRequired().HasMaxLength(100);
        builder.Property(l => l.PropertyType).IsRequired().HasMaxLength(50);
        builder.Property(l => l.PricePerNight).HasColumnType("numeric(10,2)");
        builder.Property(l => l.IsActive).HasDefaultValue(true);
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(l => l.RowVersion).IsRowVersion();

        builder.HasIndex(l => new { l.City, l.Country });
        builder.HasIndex(l => l.HostId);

        builder.HasOne(l => l.Host)
            .WithMany(u => u.Listings)
            .HasForeignKey(l => l.HostId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
