using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.DTOs.Requests.Booking
{
    public class UpdateBookingRequest
    {

        public Guid BookingId  { get; set; }
        public Guid ListingId { get; set; }
        public DateOnly? CheckIn { get; set; }
        public DateOnly? CheckOut { get; set; }
        public int? NumGuests { get; set; }

    }
}
