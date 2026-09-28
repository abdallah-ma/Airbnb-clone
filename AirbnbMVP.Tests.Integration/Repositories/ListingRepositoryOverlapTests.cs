using AirbnbMVP.DAL.Repositories;
using AirbnbMVP.Tests.Integration.Infrastructure;

namespace AirbnbMVP.Tests.Integration.Repositories;

public class ListingRepositoryOverlapTests : LocalDbTestBase
{
    private static readonly DateOnly CheckIn = new(2030, 5, 10);
    private static readonly DateOnly CheckOut = new(2030, 5, 15);

    private async Task<Models.Listings.Listing> GivenListingAsync() =>
        await SeedListingAsync((await SeedUserAsync(isHost: true)).Id);

    // ---------- CheckListingBookings ----------

    [Fact]
    public async Task CheckListingBookings_WithNoBookings_ReturnsTrue()
    {
        var listing = await GivenListingAsync();
        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        var result = await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, null);

        result.Should().BeTrue("no bookings exist yet");
    }

    [Theory]
    [InlineData(2030, 5, 10, 2030, 5, 12)]   // starts before, ends inside
    [InlineData(2030, 5, 13, 2030, 5, 20)]   // entirely inside the request
    [InlineData(2030, 5, 1, 2030, 5, 20)]    // fully contains the request
    public async Task CheckListingBookings_WhenExistingBookingOverlaps_ReturnsFalse(
        int y1, int m1, int d1, int y2, int m2, int d2)
    {
        var host = await SeedUserAsync(isHost: true);
        var guest = await SeedUserAsync();
        var listing = await SeedListingAsync(host.Id);
        await SeedBookingAsync(listing, guest.Id, new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2));

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        var result = await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, null);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(2030, 5, 8, 2030, 5, 10)]    // ends the day the request starts
    [InlineData(2030, 5, 15, 2030, 5, 20)]   // starts the day the request ends
    [InlineData(2030, 5, 16, 2030, 5, 25)]   // entirely after
    [InlineData(2030, 5, 1, 2030, 5, 9)]     // entirely before
    public async Task CheckListingBookings_WhenExistingBookingIsAdjacentOrOutside_ReturnsTrue(
        int y1, int m1, int d1, int y2, int m2, int d2)
    {
        var host = await SeedUserAsync(isHost: true);
        var guest = await SeedUserAsync();
        var listing = await SeedListingAsync(host.Id);
        await SeedBookingAsync(listing, guest.Id, new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2));

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        var result = await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, null);

        result.Should().BeTrue("check-out/check-in days are not occupied nights");
    }

    [Fact]
    public async Task CheckListingBookings_IgnoresCancelledBookings()
    {
        var host = await SeedUserAsync(isHost: true);
        var guest = await SeedUserAsync();
        var listing = await SeedListingAsync(host.Id);
        await SeedBookingAsync(listing, guest.Id, CheckIn, CheckOut, status: Models.Bookings.BookingStatus.Cancelled);

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        var result = await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, null);

        result.Should().BeTrue("a cancelled booking frees the dates");
    }

    [Fact]
    public async Task CheckListingBookings_CountsPendingBookingsAsBlocking()
    {
        var host = await SeedUserAsync(isHost: true);
        var guest = await SeedUserAsync();
        var listing = await SeedListingAsync(host.Id);
        await SeedBookingAsync(listing, guest.Id, CheckIn, CheckOut, status: Models.Bookings.BookingStatus.Pending);

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, null))
            .Should().BeFalse("an unpaid pending booking still holds the dates");
    }

    [Fact]
    public async Task CheckListingBookings_IgnoresBookingsOnOtherListings()
    {
        var host = await SeedUserAsync(isHost: true);
        var guest = await SeedUserAsync();
        var listingA = await SeedListingAsync(host.Id);
        var listingB = await SeedListingAsync(host.Id);
        await SeedBookingAsync(listingB, guest.Id, CheckIn, CheckOut);

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingBookings(listingA.Id, CheckIn, CheckOut, null))
            .Should().BeTrue();
    }

    [Fact]
    public async Task CheckListingBookings_WhenExcludedIdSupplied_IgnoresThatBooking()
    {
        // This is what lets a guest extend their own booking without it conflicting with itself.
        var host = await SeedUserAsync(isHost: true);
        var guest = await SeedUserAsync();
        var listing = await SeedListingAsync(host.Id);
        var own = await SeedBookingAsync(listing, guest.Id, CheckIn, CheckOut);

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, own.Id))
            .Should().BeTrue("the booking being edited excludes itself");

        (await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, null))
            .Should().BeFalse("without the exclusion it conflicts with itself");
    }

    [Fact]
    public async Task CheckListingBookings_WhenExcludedIdSupplied_StillDetectsOtherOverlaps()
    {
        var host = await SeedUserAsync(isHost: true);
        var guestA = await SeedUserAsync();
        var guestB = await SeedUserAsync();
        var listing = await SeedListingAsync(host.Id);
        var own = await SeedBookingAsync(listing, guestA.Id, CheckIn, new DateOnly(2030, 5, 12));
        await SeedBookingAsync(listing, guestB.Id, new DateOnly(2030, 5, 13), CheckOut);

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingBookings(listing.Id, CheckIn, CheckOut, own.Id))
            .Should().BeFalse("the other guest's booking still conflicts");
    }

    // ---------- CheckListingAvailabilityBlocks ----------

    [Fact]
    public async Task CheckListingAvailabilityBlocks_WithNoBlocks_ReturnsTrue()
    {
        var listing = await GivenListingAsync();
        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingAvailabilityBlocks(listing.Id, CheckIn, CheckOut))
            .Should().BeTrue();
    }

    [Fact]
    public async Task CheckListingAvailabilityBlocks_WhenBlockIsStrictlyInsideRequest_ReturnsFalse()
    {
        // Regression guard for the half-open overlap fix: a short block falling inside a
        // longer requested stay must still be detected.
        var listing = await GivenListingAsync();
        await SeedAvailabilityBlockAsync(listing, new DateOnly(2030, 5, 11), new DateOnly(2030, 5, 12));

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingAvailabilityBlocks(listing.Id, CheckIn, CheckOut))
            .Should().BeFalse("a one-day block inside a five-day request blocks the whole stay");
    }

    [Theory]
    [InlineData(2030, 5, 8, 2030, 5, 10)]    // ends the day the request starts
    [InlineData(2030, 5, 15, 2030, 5, 18)]   // starts the day the request ends
    [InlineData(2030, 5, 20, 2030, 5, 25)]   // after
    public async Task CheckListingAvailabilityBlocks_WhenBlockIsAdjacentOrAfter_ReturnsTrue(
        int y1, int m1, int d1, int y2, int m2, int d2)
    {
        var listing = await GivenListingAsync();
        await SeedAvailabilityBlockAsync(listing, new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2));

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingAvailabilityBlocks(listing.Id, CheckIn, CheckOut))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(2030, 5, 1, 2030, 5, 20)]    // fully contains
    [InlineData(2030, 5, 12, 2030, 5, 13)]   // fully inside
    [InlineData(2030, 5, 5, 2030, 5, 18)]    // overlaps the start
    [InlineData(2030, 5, 12, 2030, 5, 18)]   // overlaps the end
    public async Task CheckListingAvailabilityBlocks_WhenBlockOverlaps_ReturnsFalse(
        int y1, int m1, int d1, int y2, int m2, int d2)
    {
        var listing = await GivenListingAsync();
        await SeedAvailabilityBlockAsync(listing, new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2));

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingAvailabilityBlocks(listing.Id, CheckIn, CheckOut))
            .Should().BeFalse();
    }

    [Fact]
    public async Task CheckListingAvailabilityBlocks_IgnoresBlocksOnOtherListings()
    {
        var host = await SeedUserAsync(isHost: true);
        var listingA = await SeedListingAsync(host.Id);
        var listingB = await SeedListingAsync(host.Id);
        await SeedAvailabilityBlockAsync(listingB, CheckIn, CheckOut);

        await using var context = CreateContext();
        var repository = new ListingRepository(context);

        (await repository.CheckListingAvailabilityBlocks(listingA.Id, CheckIn, CheckOut))
            .Should().BeTrue();
    }
}
