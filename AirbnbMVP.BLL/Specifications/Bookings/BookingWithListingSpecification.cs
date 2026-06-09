using AirbnbMVP.DAL;
using AirbnbMVP.Models.Bookings;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Specifications.Bookings
{
    public class BookingWithListingSpecification : Specification<Booking>
    {
        public BookingWithListingSpecification(Guid bookingId) : base(b => b.Id == bookingId)
        {
            AddInclude(b => b.Listing);
        }
    }
}
