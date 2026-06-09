using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
namespace AirbnbMVP.Models.Bookings;

public class Booking : BaseEntity
{
    public Guid ListingId { get; set; }
    public Guid GuestId { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int NumGuests { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Listing Listing { get; set; } = null!;
    public User Guest { get; set; } = null!;
    public Review? Review { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}
