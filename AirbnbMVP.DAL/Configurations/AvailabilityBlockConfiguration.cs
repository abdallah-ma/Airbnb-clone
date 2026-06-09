using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirbnbMVP.DAL.Configurations.Listings;

public class AvailabilityBlockConfiguration : IEntityTypeConfiguration<AvailabilityBlock>
{
    public void Configure(EntityTypeBuilder<AvailabilityBlock> builder)
    {
        builder.ToTable("availability_blocks");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWID()");

        builder.Property(a => a.StartDate).HasColumnType("date");
        builder.Property(a => a.EndDate).HasColumnType("date");

        builder.Property(a => a.Reason).HasMaxLength(500);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_availability_blocks_dates",
            "EndDate > StartDate"));

        builder.HasIndex(a => new { a.ListingId, a.StartDate, a.EndDate });

        builder.HasOne(a => a.Listing)
            .WithMany(l => l.AvailabilityBlocks)
            .HasForeignKey(a => a.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
