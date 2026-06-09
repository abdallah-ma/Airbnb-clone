using AirbnbMVP.Models.Listings;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.DAL.Interfaces
{
    public interface IListingRepository : IGenericRepository<Listing>
    {

        Task<bool> CheckListingAvailabilityBlocks(Guid listingId, DateOnly checkIn, DateOnly checkOut);

        Task<bool> CheckListingBookings(Guid listingId, DateOnly checkIn, DateOnly checkOut, Guid? excludedBookingId);


    }
}
