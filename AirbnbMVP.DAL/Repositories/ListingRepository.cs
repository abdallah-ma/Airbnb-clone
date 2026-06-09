using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.Data;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AirbnbMVP.DAL.Repositories
{
    public class ListingRepository : GenericRepository<Listing> , IListingRepository
    {

        public ListingRepository(AirbnbDbContext context) : base(context) { }


        public async Task<bool> CheckListingAvailabilityBlocks(Guid listingId, DateOnly checkIn, DateOnly checkOut)
        {

            var listing = await Context.Listings.Include(l => l.Bookings).FirstOrDefaultAsync(l => l.Id == listingId);

            return !listing.AvailabilityBlocks.Any(block => (block.StartDate <= checkIn && checkIn <= block.EndDate) ||
                                                            (block.StartDate <= checkOut && checkOut <= block.EndDate));


        }

        public async Task<bool> CheckListingBookings(Guid listingId, DateOnly checkIn, DateOnly checkOut , Guid? excludedBookingId)
        {
            var listing = await Context.Listings.Include(l => l.Bookings).FirstOrDefaultAsync(l => l.Id == listingId);

            return !listing.Bookings.Any(booking => (booking.CheckIn <= checkIn && checkIn <= booking.CheckOut && booking.Id != excludedBookingId) ||
                                                    (booking.CheckIn <= checkOut && checkOut <= booking.CheckOut && booking.Id != excludedBookingId)  
                                                );

        }
    }
}
