using AirbnbMVP.DAL;
using AirbnbMVP.Models.Bookings;


namespace AirbnbMVP.BLL.Specifications.Bookings
{
    public class OverlappingBookingsSpec : Specification<Booking>
    {
        public OverlappingBookingsSpec(Guid listingId, DateOnly checkIn, DateOnly checkOut, Guid? excludeBookingId = null) : 
            
            base(b => b.ListingId == listingId &&
                            b.Status != BookingStatus.Cancelled &&
                            (excludeBookingId == null || b.Id != excludeBookingId) &&
                            b.CheckIn < checkOut &&
                            b.CheckOut > checkIn)   { }

    }
}
