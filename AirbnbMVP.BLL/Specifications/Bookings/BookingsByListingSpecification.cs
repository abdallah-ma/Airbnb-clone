using AirbnbMVP.DAL;
using AirbnbMVP.Models.Bookings;


namespace AirbnbMVP.BLL.Specifications.Bookings
{
    public class BookingsByListingSpecification : Specification<Booking>
    {
        public BookingsByListingSpecification(Guid listingId) : base(b => b.ListingId == listingId)
        {
            AddInclude(b => b.Guest);
            AddInclude(b => b.Review);
        }
    }
}
