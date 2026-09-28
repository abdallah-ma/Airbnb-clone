using AirbnbMVP.BLL.DTOs.Requests.Booking;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Services;
using AirbnbMVP.BLL.Specifications.Bookings;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using Moq;

namespace AirbnbMVP.Tests.Unit.Services;

public class BookingServiceUpdateTests
{
    private static readonly DateOnly OriginalCheckIn = new(2030, 7, 1);
    private static readonly DateOnly OriginalCheckOut = new(2030, 7, 4);

    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IGenericRepository<Booking>> _bookingRepository = new();
    private readonly Mock<IGenericRepository<AirbnbMVP.DAL.Models.Transaction>> _transactionRepository = new();
    private readonly Mock<IListingRepository> _listingRepository = new();
    private readonly Mock<AirbnbMVP.BLL.Interfaces.IPaymentService> _paymentService = new();

    private BookingService BuildService() => new(
        _listingRepository.Object,
        _bookingRepository.Object,
        _userRepository.Object,
        _transactionRepository.Object,
        _paymentService.Object);

    private Booking CreateBooking(
        BookingStatus status = BookingStatus.Pending,
        int numGuests = 2,
        int maxGuests = 4,
        decimal pricePerNight = 150m,
        Guid? guestId = null)
    {
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            HostId = Guid.NewGuid(),
            MaxGuests = maxGuests,
            PricePerNight = pricePerNight
        };

        return new Booking
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            Listing = listing,
            GuestId = guestId ?? _guestId,
            Status = status,
            NumGuests = numGuests,
            CheckIn = OriginalCheckIn,
            CheckOut = OriginalCheckOut,
            TotalPrice = 3 * pricePerNight
        };
    }

    private void GivenUserExists(Guid userId) =>
        _userRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(new User { Id = userId });

    private void GivenBooking(Booking? booking) =>
        _bookingRepository
            .Setup(r => r.GetAsync(It.IsAny<BookingWithListingSpecification>()))
            .ReturnsAsync(booking);

    private void GivenListingIsFree(bool free = true)
    {
        _listingRepository
            .Setup(r => r.CheckListingAvailabilityBlocks(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(free);
        _listingRepository
            .Setup(r => r.CheckListingBookings(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>()))
            .ReturnsAsync(free);
    }

    [Fact]
    public async Task UpdateBookingAsync_WhenUserMissing_ThrowsNotFound()
    {
        _userRepository.Setup(r => r.GetUserByIdAsync(_guestId)).ReturnsAsync((User?)null);

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = Guid.NewGuid() }, _guestId);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found.");
    }

    [Fact]
    public async Task UpdateBookingAsync_WhenBookingMissing_ThrowsNotFound()
    {
        GivenUserExists(_guestId);
        GivenBooking(null);

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = Guid.NewGuid() }, _guestId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("There's no booking with this id.");
    }

    [Fact]
    public async Task UpdateBookingAsync_WhenCallerIsNotTheGuest_ThrowsForbidden()
    {
        GivenUserExists(_otherUserId);
        GivenBooking(CreateBooking(guestId: _guestId));

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = Guid.NewGuid() }, _otherUserId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not own this booking.");

        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed)]
    public async Task UpdateBookingAsync_WhenBookingIsNotPending_ThrowsBadRequest(BookingStatus status)
    {
        GivenUserExists(_guestId);
        GivenBooking(CreateBooking(status: status));

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = Guid.NewGuid() }, _guestId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Only pending bookings can be modified.");
    }

    [Fact]
    public async Task UpdateBookingAsync_WhenGuestsExceedListingCapacity_ThrowsBadRequest()
    {
        GivenUserExists(_guestId);
        GivenBooking(CreateBooking(maxGuests: 2));

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = Guid.NewGuid(), NumGuests = 5 }, _guestId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*only allows up to 2 guests*");

        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task UpdateBookingAsync_WhenCheckOutNotAfterCheckIn_ThrowsBadRequest()
    {
        GivenUserExists(_guestId);
        GivenBooking(CreateBooking());

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest
            {
                BookingId = Guid.NewGuid(),
                CheckIn = new DateOnly(2030, 8, 10),
                CheckOut = new DateOnly(2030, 8, 5)
            },
            _guestId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Check-out date must be after check-in date.");
    }

    [Fact]
    public async Task UpdateBookingAsync_WhenAvailabilityBlockOverlaps_ThrowsConflict()
    {
        GivenUserExists(_guestId);
        GivenBooking(CreateBooking());
        _listingRepository
            .Setup(r => r.CheckListingAvailabilityBlocks(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(false);

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = Guid.NewGuid() }, _guestId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not available on the requested dates*");

        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task UpdateBookingAsync_WhenAnotherBookingOverlaps_ThrowsConflict()
    {
        GivenUserExists(_guestId);
        GivenBooking(CreateBooking());
        _listingRepository
            .Setup(r => r.CheckListingAvailabilityBlocks(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(true);
        _listingRepository
            .Setup(r => r.CheckListingBookings(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>()))
            .ReturnsAsync(false);

        var act = () => BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = Guid.NewGuid() }, _guestId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*booked on the requested dates*");
    }

    [Fact]
    public async Task UpdateBookingAsync_ExcludesTheBookingBeingUpdatedFromTheOverlapCheck()
    {
        // Regression guard: passing null here would make every booking conflict with itself,
        // because the booking already occupies the requested range.
        GivenUserExists(_guestId);
        var booking = CreateBooking();
        GivenBooking(booking);
        GivenListingIsFree();

        await BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = booking.Id }, _guestId);

        _listingRepository.Verify(r => r.CheckListingBookings(
            booking.ListingId,
            OriginalCheckIn,
            OriginalCheckOut,
            booking.Id), Times.Once);
    }

    [Fact]
    public async Task UpdateBookingAsync_WithNullFields_KeepsExistingValues()
    {
        GivenUserExists(_guestId);
        var booking = CreateBooking(pricePerNight: 150m);
        GivenBooking(booking);
        GivenListingIsFree();

        var result = await BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = booking.Id }, _guestId);

        result.CheckIn.Should().Be(OriginalCheckIn);
        result.CheckOut.Should().Be(OriginalCheckOut);
        result.NumGuests.Should().Be(2);
        result.TotalPrice.Should().Be(450m);
    }

    [Fact]
    public async Task UpdateBookingAsync_WithNewDates_RecalculatesTotalPrice()
    {
        GivenUserExists(_guestId);
        var booking = CreateBooking(pricePerNight: 150m);
        GivenBooking(booking);
        GivenListingIsFree();

        var result = await BuildService().UpdateBookingAsync(
            new UpdateBookingRequest
            {
                BookingId = booking.Id,
                CheckIn = new DateOnly(2030, 9, 1),
                CheckOut = new DateOnly(2030, 9, 6)
            },
            _guestId);

        // 5 nights x 150
        result.TotalPrice.Should().Be(750m);
        result.CheckIn.Should().Be(new DateOnly(2030, 9, 1));
        result.CheckOut.Should().Be(new DateOnly(2030, 9, 6));

        booking.TotalPrice.Should().Be(750m);
        _bookingRepository.Verify(r => r.UpdateAsync(booking), Times.Once);
    }

    [Fact]
    public async Task UpdateBookingAsync_PreservesPendingStatus()
    {
        GivenUserExists(_guestId);
        var booking = CreateBooking();
        GivenBooking(booking);
        GivenListingIsFree();

        var result = await BuildService().UpdateBookingAsync(
            new UpdateBookingRequest { BookingId = booking.Id, NumGuests = 3 }, _guestId);

        result.Status.Should().Be(BookingStatus.Pending);
    }
}
