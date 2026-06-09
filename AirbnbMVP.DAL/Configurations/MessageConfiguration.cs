using AirbnbMVP.Models.Messaging;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirbnbMVP.DAL.Configurations.Messaging;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id);

        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.IsRead).HasDefaultValue(false);
        builder.Property(m => m.SentAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(m => new { m.SenderId, m.ReceiverId, m.SentAt });

        builder.HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Receiver)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Listing).WithMany(l => l.Messages)
            .HasForeignKey(m => m.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
