using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.DTOs.Requests.Booking
{
    public class BookingRequestDto
    {
        public Guid ListingId { get; set; }
        public DateOnly CheckIn { get; set; }
        public DateOnly CheckOut { get; set; }
        public int NumGuests { get; set; }
        public DateTime CreatedAt { get; set; }


        

    }
}
