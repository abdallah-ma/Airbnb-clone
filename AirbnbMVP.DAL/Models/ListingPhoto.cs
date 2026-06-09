using AirbnbMVP.DAL.Models;

namespace AirbnbMVP.Models.Listings;

public class ListingPhoto : BaseEntity
{
    public Guid ListingId { get; set; }
    public string Url { get; set; } = null!;
    public bool IsCover { get; set; }
    public int SortOrder { get; set; }

    // Navigation
}
