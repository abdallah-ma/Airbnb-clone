using AirbnbMVP.DAL.Models;

namespace AirbnbMVP.Models.Listings;

public class Amenity : BaseEntity
{
    public string Name { get; set; } = null!;

    // Navigation
    public ICollection<ListingAmenity> ListingAmenities { get; set; } = new List<ListingAmenity>();
}
