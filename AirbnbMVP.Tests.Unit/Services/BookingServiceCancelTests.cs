using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Services;
using AirbnbMVP.BLL.Specifications;
using AirbnbMVP.BLL.Specifications.Transactions;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using Moq;

namespace AirbnbMVP.Tests.Unit.Services;

public class BookingServiceCancelTests
{
    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IGenericRepository<Booking>> _bookingRepository = new();
    private readonly Mock<IGenericRepository<Transaction>> _transactionRepository = new();
    private readonly Mock<IPaymentService> _paymentService = new();

    private BookingService BuildService() => new(
        new Mock<IListingRepository>().Object,
        _bookingRepository.Object,
        _userRepository.Object,
        _transactionRepository.Object,
        _paymentService.Object);

    private static Booking CreateBooking(
        Guid guestId,
        BookingStatus status = BookingStatus.Confirmed,
        DateOnly? checkIn = null,
        decimal totalPrice = 300m) => new()
        {
            Id = Guid.NewGuid(),
            GuestId = guestId,
            Status = status,
            TotalPrice = totalPrice,
            CheckIn = checkIn ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            CheckOut = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(33)
        };

    private void GivenUserExists(Guid userId) =>
        _userRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(new User { Id = userId });

    private void GivenBooking(Booking? booking) =>
        _bookingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Booking>>()))
            .ReturnsAsync(booking);

    private void GivenNoCompletedPayment() =>
        _transactionRepository
            .Setup(r => r.GetAsync(It.IsAny<TransactionSpecification>()))
            .ReturnsAsync((Transaction?)null);

    [Fact]
    public async Task CancelBookingAsync_WhenUserMissing_ThrowsNotFound()
    {
        _userRepository.Setup(r => r.GetUserByIdAsync(_guestId)).ReturnsAsync((User?)null);

        var act = () => BuildService().CancelBookingAsync(Guid.NewGuid(), _guestId);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found.");
    }

    [Fact]
    public async Task CancelBookingAsync_WhenBookingMissing_ThrowsNotFound()
    {
        GivenUserExists(_guestId);
        GivenBooking(null);

        var act = () => BuildService().CancelBookingAsync(Guid.NewGuid(), _guestId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("There's no booking with this id.");
    }

    [Fact]
    public async Task CancelBookingAsync_WhenCallerIsNotTheGuest_ThrowsForbidden()
    {
        GivenUserExists(_otherUserId);
        GivenBooking(CreateBooking(_guestId));

        var act = () => BuildService().CancelBookingAsync(Guid.NewGuid(), _otherUserId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*not the user who made this booking*");

        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Theory]
    [InlineData(BookingStatus.Completed, "*completed booking*")]
    [InlineData(BookingStatus.Cancelled, "*already cancelled*")]
    public async Task CancelBookingAsync_WhenBookingIsNotCancellable_ThrowsBadRequest(
        BookingStatus status, string message)
    {
        GivenUserExists(_guestId);
        GivenBooking(CreateBooking(_guestId, status: status));

        var act = () => BuildService().CancelBookingAsync(Guid.NewGuid(), _guestId);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage(message);

        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]   // starts today
    [InlineData(1)]   // starts tomorrow - inside the 24h window
    [InlineData(-3)]  // already started
    public async Task CancelBookingAsync_WhenCheckInIsWithin24Hours_ThrowsBadRequest(int offsetDays)
    {
        GivenUserExists(_guestId);
        GivenBooking(CreateBooking(
            _guestId, checkIn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offsetDays)));

        var act = () => BuildService().CancelBookingAsync(Guid.NewGuid(), _guestId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*within 24 hours of check-in*");

        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenCheckInIsTwoDaysAway_Succeeds()
    {
        GivenUserExists(_guestId);
        var booking = CreateBooking(_guestId, checkIn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2));
        GivenBooking(booking);
        GivenNoCompletedPayment();

        await BuildService().CancelBookingAsync(booking.Id, _guestId);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        _bookingRepository.Verify(r => r.UpdateAsync(booking), Times.Once);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenNoCompletedPayment_IssuesNoRefund()
    {
        GivenUserExists(_guestId);
        var booking = CreateBooking(_guestId);
        GivenBooking(booking);
        GivenNoCompletedPayment();

        await BuildService().CancelBookingAsync(booking.Id, _guestId);

        _paymentService.Verify(p => p.CreateRefundAsync(It.IsAny<string>(), It.IsAny<decimal>()), Times.Never);
        _transactionRepository.Verify(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenCompletedPaymentExists_RefundsAndRecordsRefundTransaction()
    {
        GivenUserExists(_guestId);
        var booking = CreateBooking(_guestId, totalPrice: 420m);
        GivenBooking(booking);

        _transactionRepository
            .Setup(r => r.GetAsync(It.IsAny<TransactionSpecification>()))
            .ReturnsAsync(new Transaction
            {
                BookingId = booking.Id,
                Type = TransactionType.Payment,
                Status = TransactionStatus.Completed,
                PaymentReference = "pi_abc123",
                Amount = 420m
            });

        await BuildService().CancelBookingAsync(booking.Id, _guestId);

        _paymentService.Verify(p => p.CreateRefundAsync("pi_abc123", 420m), Times.Once);

        _transactionRepository.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
            t.Type == TransactionType.Refund &&
            t.Status == TransactionStatus.Completed &&
            t.Amount == 420m &&
            t.BookingId == booking.Id &&
            t.PayerId == booking.GuestId &&
            t.PayeeId == booking.GuestId), true), Times.Once);
    }

    [Fact]
    public async Task CancelBookingAsync_LooksUpTheTransactionsForThatBooking()
    {
        GivenUserExists(_guestId);
        var booking = CreateBooking(_guestId);
        GivenBooking(booking);
        GivenNoCompletedPayment();

        await BuildService().CancelBookingAsync(booking.Id, _guestId);

        // The filter itself lives in TransactionSpecification and is covered by
        // TransactionSpecificationTests, which evaluates the criteria expression directly.
        _transactionRepository.Verify(r => r.GetAsync(It.IsAny<TransactionSpecification>()), Times.Once);
    }

    [Fact]
    public void TransactionSpecification_ExposesUnpopulatedFilterProperties()
    {
        // Documents a latent trap: the constructor only builds the criteria expression and
        // never assigns the public filter properties, so reading them yields null even when
        // a value was supplied. Callers must rely on Criteria, not these properties.
        var spec = new TransactionSpecification(Guid.NewGuid(), TransactionType.Payment, TransactionStatus.Completed);

        spec.BookingId.Should().BeNull();
        spec.Type.Should().BeNull();
        spec.Status.Should().BeNull();
        spec.Criteria.Should().NotBeNull();
    }
}
