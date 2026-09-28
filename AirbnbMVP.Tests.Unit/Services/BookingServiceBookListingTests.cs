using AirbnbMVP.BLL.DTOs.Requests.Booking;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Services;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.Tests.Unit.Common;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore;

namespace AirbnbMVP.Tests.Unit.Services;

public class BookingServiceBookListingTests
{
    // Fixed future dates so the "check-in cannot be in the past" guard never interferes,
    // and both dates sit in the same year (see Bug_PeriodComputationBreaksAcrossYearBoundary).
    private static readonly DateOnly CheckIn = new(2030, 6, 10);
    private static readonly DateOnly CheckOut = new(2030, 6, 15);

    private readonly Guid _hostId = Guid.NewGuid();
    private readonly Guid _guestId = Guid.NewGuid();

    private static BookingRequestDto Request(Guid listingId, DateOnly checkIn, DateOnly checkOut, int guests = 2) => new()
    {
        ListingId = listingId,
        CheckIn = checkIn,
        CheckOut = checkOut,
        NumGuests = guests
    };

    private (BookingServiceHarness Harness, BookingService Service) Setup(Listing? listing = null)
    {
        var harness = new BookingServiceHarness();
        harness.GivenListing(listing ?? BookingServiceHarness.CreateListing(_hostId));
        harness.GivenAvailabilityBlocksClear();
        harness.GivenNoConflictingBookings();
        return (harness, harness.Build());
    }

    [Fact]
    public async Task BookListingAsync_HappyPath_StagesBothEntitiesAndSavesOnce()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId, pricePerNight: 100m);
        var (harness, service) = Setup(listing);

        var result = await service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut, 2), _guestId);

        harness.BookingRepository.Verify(r => r.SaveAsync(), Times.Once);
        harness.DbTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        Booking? savedBooking = null;
        harness.BookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>(), false), Times.Once);
        harness.BookingRepository
            .Verify(r => r.AddAsync(It.Is<Booking>(b =>
                b.ListingId == listing.Id &&
                b.GuestId == _guestId &&
                b.Status == BookingStatus.Pending), false), Times.Once);

        _ = savedBooking;

        // 5 nights x 100
        result.TotalPrice.Should().Be(500m);
        result.Status.Should().Be(BookingStatus.Pending);
        result.ClientSecret.Should().Be("pi_client_secret");
        result.NumGuests.Should().Be(2);
    }

    [Fact]
    public async Task BookListingAsync_HappyPath_StagesPendingTransactionWithCorrectParties()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId, pricePerNight: 250m);
        var (harness, service) = Setup(listing);

        await service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        harness.TransactionRepository
            .Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.PayerId == _guestId &&
                t.PayeeId == _hostId &&
                t.Amount == 1250m &&
                t.Status == TransactionStatus.Pending), false), Times.Once);
    }

    [Fact]
    public async Task BookListingAsync_BookingAndTransactionShareTheSameGeneratedBookingId()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);

        await service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        Guid? bookingIdFromBooking = null;
        Guid? bookingIdFromTransaction = null;

        harness.BookingRepository
            .Verify(r => r.AddAsync(It.Is<Booking>(b => Capture(b.Id, ref bookingIdFromBooking)), false), Times.Once);
        harness.TransactionRepository
            .Verify(r => r.AddAsync(It.Is<Transaction>(t => Capture(t.BookingId, ref bookingIdFromTransaction)), false), Times.Once);

        bookingIdFromBooking.Should().NotBe(Guid.Empty);
        bookingIdFromTransaction.Should().Be(bookingIdFromBooking);
    }

    private static bool Capture(Guid value, ref Guid? slot)
    {
        slot = value;
        return true;
    }

    [Fact]
    public async Task BookListingAsync_CommitsBeforeContactingPaymentGateway()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);

        var order = new List<string>();
        harness.DbTransaction
            .Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .Returns(Task.CompletedTask);
        harness.BookingRepository
            .Setup(r => r.SaveAsync())
            .Callback(() => order.Add("save"))
            .ReturnsAsync(1);
        harness.PaymentService
            .Setup(p => p.CreatePaymentIntentAsync(It.IsAny<decimal>(), It.IsAny<Guid>()))
            .Callback(() => order.Add("payment"))
            .ReturnsAsync(new AirbnbMVP.BLL.DTOs.Requests.PaymentIntentResponse { ClientSecret = "s" });

        await service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        order.Should().ContainInOrder("save", "commit", "payment");
    }

    [Fact]
    public async Task BookListingAsync_PassesTotalPriceAndBookingIdToPaymentGateway()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId, pricePerNight: 80m);
        var (harness, service) = Setup(listing);

        Guid? chargedBookingId = null;
        harness.PaymentService
            .Setup(p => p.CreatePaymentIntentAsync(It.IsAny<decimal>(), It.IsAny<Guid>()))
            .Callback((decimal amount, Guid bookingId) =>
            {
                chargedBookingId = bookingId;
                amount.Should().Be(400m);
            })
            .ReturnsAsync(new AirbnbMVP.BLL.DTOs.Requests.PaymentIntentResponse { ClientSecret = "s" });

        await service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        chargedBookingId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task BookListingAsync_OpensTransactionAtSerializableIsolationLevel()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);

        await service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        harness.BookingRepository.Verify(
            r => r.BeginTransactionAsync(System.Data.IsolationLevel.Serializable), Times.Once);
    }

    [Fact]
    public async Task BookListingAsync_WhenCheckOutIsNotAfterCheckIn_ThrowsBadRequest_AndNeverOpensTransaction()
    {
        var (harness, service) = Setup();

        var act = () => service.BookListingAsync(Request(Guid.NewGuid(), CheckOut, CheckIn), _guestId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*after check-in*");

        harness.BookingRepository.Verify(
            r => r.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>()), Times.Never);
    }

    [Fact]
    public async Task BookListingAsync_WhenCheckInEqualsCheckOut_ThrowsBadRequest()
    {
        var (harness, service) = Setup();

        var act = () => service.BookListingAsync(Request(Guid.NewGuid(), CheckIn, CheckIn), _guestId);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task BookListingAsync_WhenCheckInIsInThePast_ThrowsBadRequest()
    {
        var (harness, service) = Setup();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var act = () => service.BookListingAsync(
            Request(Guid.NewGuid(), yesterday, yesterday.AddDays(3)), _guestId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*cannot be in the past*");

        harness.BookingRepository.Verify(
            r => r.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>()), Times.Never);
    }

    [Fact]
    public async Task BookListingAsync_WhenListingMissing_ThrowsNotFound()
    {
        var harness = new BookingServiceHarness();
        harness.GivenNoListing();
        var service = harness.Build();

        var act = () => service.BookListingAsync(Request(Guid.NewGuid(), CheckIn, CheckOut), _guestId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Listing not found.");
    }

    [Fact]
    public async Task BookListingAsync_WhenAvailabilityBlockOverlaps_ThrowsConflict()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);
        harness.GivenAvailabilityBlocksClear(false);

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not available on the requested dates*");

        harness.BookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<bool>()), Times.Never);
        harness.DbTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookListingAsync_WhenDatesConflictWithExistingBooking_ThrowsConflict()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);
        harness.GivenNoConflictingBookings(false);

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*booked on the requested dates*");

        harness.BookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<bool>()), Times.Never);
        harness.DbTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookListingAsync_WhenGuestsExceedCapacity_ThrowsBadRequest()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId, maxGuests: 2);
        var (harness, service) = Setup(listing);

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut, guests: 3), _guestId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*only allows up to 2 guests*");

        harness.BookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task BookListingAsync_WhenGuestsEqualCapacity_IsAllowed()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId, maxGuests: 2);
        var (_, service) = Setup(listing);

        var result = await service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut, guests: 2), _guestId);

        result.NumGuests.Should().Be(2);
    }

    [Fact]
    public async Task BookListingAsync_WhenHostBooksOwnListing_ThrowsForbidden()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _hostId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*cannot book your own listing*");

        harness.BookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task BookListingAsync_WhenSaveHitsDeadlockAsDbUpdateException_ThrowsConflict()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);
        harness.BookingRepository
            .Setup(r => r.SaveAsync())
            .ThrowsAsync(new DbUpdateException("deadlock", SqlExceptionFactory.Create(1205)));

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*could not be completed at this time*");

        harness.DbTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookListingAsync_WhenSaveThrowsBareDeadlockSqlException_ThrowsConflict()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);
        harness.BookingRepository
            .Setup(r => r.SaveAsync())
            .ThrowsAsync(SqlExceptionFactory.Create(1205));

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task BookListingAsync_WhenSaveThrowsNonDeadlockError_DoesNotMaskItAsConflict()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);
        harness.BookingRepository
            .Setup(r => r.SaveAsync())
            .ThrowsAsync(new DbUpdateException("constraint violation", new InvalidOperationException("fk")));

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        // Only deadlock (1205) is translated; anything else must surface unchanged.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task BookListingAsync_WhenSaveThrowsNonDeadlockSqlException_DoesNotMaskItAsConflict()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId);
        var (harness, service) = Setup(listing);
        harness.BookingRepository
            .Setup(r => r.SaveAsync())
            .ThrowsAsync(SqlExceptionFactory.Create(2627)); // unique key violation

        var act = () => service.BookListingAsync(Request(listing.Id, CheckIn, CheckOut), _guestId);

        await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>();
    }

    [Fact]
    public async Task BookListingAsync_StaySpanningNewYear_ChargesCorrectNumberOfNights()
    {
        // DateOnly.DayNumber is a linear day count (not day-of-year), so subtracting it
        // stays correct across a year boundary. Guards the pricing calculation against a
        // future "optimisation" to DayOfYear.
        var listing = BookingServiceHarness.CreateListing(_hostId, pricePerNight: 100m);
        var (harness, service) = Setup(listing);

        var result = await service.BookListingAsync(
            Request(listing.Id, new DateOnly(2030, 12, 30), new DateOnly(2031, 1, 2)), _guestId);

        result.TotalPrice.Should().Be(300m, "3 nights were requested across the year boundary");
    }

    [Fact]
    public async Task BookListingAsync_StaySpanningLeapDay_ChargesCorrectNumberOfNights()
    {
        var listing = BookingServiceHarness.CreateListing(_hostId, pricePerNight: 100m);
        var (harness, service) = Setup(listing);

        var result = await service.BookListingAsync(
            Request(listing.Id, new DateOnly(2032, 2, 27), new DateOnly(2032, 3, 1)), _guestId);

        // 2032 is a leap year: 27 Feb -> 28 Feb -> 29 Feb -> 1 Mar = 3 nights.
        result.TotalPrice.Should().Be(300m);
    }
}
