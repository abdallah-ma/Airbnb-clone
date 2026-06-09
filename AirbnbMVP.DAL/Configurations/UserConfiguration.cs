using AirbnbMVP.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirbnbMVP.DAL.Configurations.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Table name is set by IdentityDbContext to "AspNetUsers" by default.
        // Override if you prefer a custom name:
        builder.ToTable("users");

        // Only configure custom columns — Identity columns (Id, Email, etc.) are
        // handled automatically by IdentityDbContext.
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.ProfilePhoto).HasMaxLength(1024);
        builder.Property(u => u.Bio).HasMaxLength(2000);
        builder.Property(u => u.IsHost).HasDefaultValue(false);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
    }
}