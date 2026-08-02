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
            return !await Context.AvailabilityBlocks
                .AnyAsync(block => block.ListingId == listingId &&
                                   block.StartDate < checkOut &&
                                   block.EndDate > checkIn);
        }

        public async Task<bool> CheckListingBookings(Guid listingId, DateOnly checkIn, DateOnly checkOut, Guid? excludedBookingId)
        {
            var excludedId = excludedBookingId ?? Guid.Empty;

            var hasOverlap = await Context.Bookings
                .FromSqlInterpolated($"""
                    SELECT * FROM bookings WITH (UPDLOCK, HOLDLOCK)
                    WHERE ListingId = {listingId}
                      AND Status <> 'Cancelled'
                      AND CheckIn < {checkOut}
                      AND CheckOut > {checkIn}
                      AND Id <> {excludedId}
                    """)
                .AnyAsync();

            return !hasOverlap;
        }
    }
}
