using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Identity;
namespace AirbnbMVP.Models.Bookings;

public class Review : BaseEntity
{
    public Guid BookingId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid RevieweeId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public ReviewType ReviewType { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Booking Booking { get; set; } = null!;
    public User Reviewer { get; set; } = null!;
    public User Reviewee { get; set; } = null!;
}

public enum ReviewType
{
    GuestToHost,
    HostToGuest
}
