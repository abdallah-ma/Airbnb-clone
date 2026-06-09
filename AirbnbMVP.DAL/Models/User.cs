using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;
using AirbnbMVP.Models.Messaging;
using Microsoft.AspNetCore.Identity;
namespace AirbnbMVP.Models.Identity;

public class User : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? ProfilePhoto { get; set; }
    public string? Bio { get; set; }
    public bool IsHost { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> ReviewsWritten { get; set; } = new List<Review>();
    public ICollection<Review> ReviewsReceived { get; set; } = new List<Review>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();

    public ICollection<Transaction> PayerTransactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> PayeeTransactions { get; set; } = new List<Transaction>();
}
