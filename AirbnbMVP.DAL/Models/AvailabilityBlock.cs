using AirbnbMVP.DAL.Models;

namespace AirbnbMVP.Models.Listings;

public class AvailabilityBlock : BaseEntity
{
    public Guid ListingId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }

    // Navigation
    public Listing Listing { get; set; } = null!;
}
