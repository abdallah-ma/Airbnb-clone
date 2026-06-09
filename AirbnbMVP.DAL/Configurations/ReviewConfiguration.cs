using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;
using AirbnbMVP.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirbnbMVP.DAL.Configurations.Bookings;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id);

        builder.Property(r => r.Rating).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_reviews_rating", "Rating >= 1 AND Rating <= 5"));

        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.Property(r => r.ReviewType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        // One booking → one review per type (guest-to-host or host-to-guest)
        builder.HasIndex(r => new { r.BookingId, r.ReviewType }).IsUnique();

        builder.HasOne(r => r.Booking)
            .WithOne(b => b.Review)
            .HasForeignKey<Review>(r => r.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Reviewer)
            .WithMany(u => u.ReviewsWritten)
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Reviewee)
            .WithMany(u => u.ReviewsReceived)
            .HasForeignKey(r => r.RevieweeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
