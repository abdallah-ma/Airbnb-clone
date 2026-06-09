using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirbnbMVP.DAL.Configurations.Listings;

public class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.ToTable("amenities");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(a => a.Name).IsUnique();
    }
}

public class ListingAmenityConfiguration : IEntityTypeConfiguration<ListingAmenity>
{
    public void Configure(EntityTypeBuilder<ListingAmenity> builder)
    {
        builder.ToTable("listing_amenities");

        builder.HasKey(la => new { la.ListingId, la.AmenityId });

        builder.HasOne(la => la.Listing)
            .WithMany(l => l.ListingAmenities)
            .HasForeignKey(la => la.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(la => la.Amenity)
            .WithMany(a => a.ListingAmenities)
            .HasForeignKey(la => la.AmenityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
