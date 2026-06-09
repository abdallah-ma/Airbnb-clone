using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace AirbnbMVP.DAL.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("transactions");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasDefaultValueSql("NEWID()");

            builder.Property(t => t.Amount).HasColumnType("decimal(10,2)");
            builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);

            builder.Property(t => t.PaymentReference).HasMaxLength(256);
            builder.Property(t => t.ProcessedAt).HasDefaultValueSql("GETUTCDATE()");

            builder.HasIndex(t => t.BookingId);
            builder.HasIndex(t => t.PayerId);

            builder.HasOne(t => t.Booking)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>(t => t.Payer)
                .WithMany(u => u.PayerTransactions)
                .HasForeignKey(t => t.PayerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>(t => t.Payee)
                .WithMany(u => u.PayeeTransactions)
                .HasForeignKey(t => t.PayeeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
