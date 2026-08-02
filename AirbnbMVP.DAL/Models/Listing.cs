using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Messaging;
namespace AirbnbMVP.Models.Listings;

public class Listing : BaseEntity
{
    public Guid HostId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Country { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string PropertyType { get; set; } = null!;
    public int MaxGuests { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public decimal PricePerNight { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation
    public User Host { get; set; } = null!;
    public ICollection<ListingPhoto> Photos { get; set; } = new List<ListingPhoto>();
    public ICollection<ListingAmenity> ListingAmenities { get; set; } = new List<ListingAmenity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<AvailabilityBlock> AvailabilityBlocks { get; set; } = new List<AvailabilityBlock>();

    public ICollection<Review> Reviews { get; set; }
}
