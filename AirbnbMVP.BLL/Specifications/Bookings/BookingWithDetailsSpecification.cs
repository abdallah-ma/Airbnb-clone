using AirbnbMVP.DAL;
using AirbnbMVP.Models.Bookings;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Specifications.Bookings
{

    public class BookingWithDetailsSpecification : Specification<Booking>
    {
        public BookingWithDetailsSpecification(Guid bookingId) : base(b => b.Id == bookingId)
        {
            AddInclude(b => b.Listing);
            AddInclude(b => b.Guest);
            AddInclude(b => b.Review);
        }
    }
}
