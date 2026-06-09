using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.DTOs.Responses
{
    public class BookingResponseDto
    {


        public DateOnly CheckIn { get; set; }
        public DateOnly CheckOut { get; set; }
        public int NumGuests { get; set; }
        public decimal TotalPrice { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public string ClientSecret { get; set; }
        public Listing Listing { get; set; } = null!;
        public User Guest { get; set; } = null!;
        public Review? Review { get; set; }

    }
}
