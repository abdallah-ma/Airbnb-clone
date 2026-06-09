using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.DTOs.Requests.Listing
{
    public class UpdateListingRequest
    {
        public Guid ListingId { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string PropertyType { get; set; } = null!;
        public int? MaxGuests { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public decimal? PricePerNight { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public List<Guid>? AmenityIds { get; set; }

    }
}
