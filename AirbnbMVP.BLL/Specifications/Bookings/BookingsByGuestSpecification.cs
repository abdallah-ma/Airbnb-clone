
using AirbnbMVP.DAL;
using AirbnbMVP.Models.Bookings;

namespace AirbnbMVP.BLL.Specifications.Bookings
{

    public class BookingsByGuestSpecification : Specification<Booking>
    {
        public BookingsByGuestSpecification(Guid guestId) : base(b => b.GuestId == guestId)
        {
            AddInclude(b => b.Listing);
            AddInclude(b => b.Review);
        }
    }

}
