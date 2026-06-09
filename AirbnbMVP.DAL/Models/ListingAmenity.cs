using AirbnbMVP.DAL.Models;

namespace AirbnbMVP.Models.Listings;

public class ListingAmenity : BaseEntity
{
    public Guid ListingId { get; set; }
    public Guid AmenityId { get; set; }

    // Navigation
    public Listing Listing { get; set; } = null!;
    public Amenity Amenity { get; set; } = null!;
}
