using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
namespace AirbnbMVP.Models.Messaging;

public class Message : BaseEntity
{
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public Guid ListingId { get; set; }
    public string Body { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }

    // Navigation
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
    public Listing Listing { get; set; } = null!;
}
