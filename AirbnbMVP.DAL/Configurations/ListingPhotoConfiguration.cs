using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirbnbMVP.DAL.Configurations.Listings;

public class ListingPhotoConfiguration : IEntityTypeConfiguration<ListingPhoto>
{
    public void Configure(EntityTypeBuilder<ListingPhoto> builder)
    {
        builder.ToTable("listing_photos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id);

        builder.Property(p => p.Url).IsRequired().HasMaxLength(1024);
        builder.Property(p => p.IsCover).HasDefaultValue(false);
        builder.Property(p => p.SortOrder).HasDefaultValue(0);

    }
}
