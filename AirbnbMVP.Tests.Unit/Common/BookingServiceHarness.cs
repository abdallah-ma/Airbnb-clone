using AirbnbMVP.BLL.DTOs.Requests;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Services;
using AirbnbMVP.BLL.Specifications;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace AirbnbMVP.Tests.Unit.Common;

/// <summary>
/// Wires <see cref="BookingService"/> up with mocked repositories and a mocked payment gateway.
/// Defaults represent a bookable listing with no conflicts; each test overrides only what it exercises.
/// </summary>
internal sealed class BookingServiceHarness
{
    public Mock<IListingRepository> ListingRepository { get; } = new();
    public Mock<IGenericRepository<Booking>> BookingRepository { get; } = new();
    public Mock<IUserRepository> UserRepository { get; } = new();
    public Mock<IGenericRepository<Transaction>> TransactionRepository { get; } = new();
    public Mock<IPaymentService> PaymentService { get; } = new();
    public Mock<IDbContextTransaction> DbTransaction { get; } = new();

    public BookingServiceHarness()
    {
        BookingRepository
            .Setup(r => r.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>()))
            .ReturnsAsync(DbTransaction.Object);

        PaymentService
            .Setup(p => p.CreatePaymentIntentAsync(It.IsAny<decimal>(), It.IsAny<Guid>()))
            .ReturnsAsync(new PaymentIntentResponse
            {
                ClientSecret = "pi_client_secret",
                PaymentIntentId = "pi_123",
                Status = "requires_payment_method"
            });
    }

    public BookingService Build() => new(
        ListingRepository.Object,
        BookingRepository.Object,
        UserRepository.Object,
        TransactionRepository.Object,
        PaymentService.Object);

    public static Listing CreateListing(
        Guid hostId,
        int maxGuests = 4,
        decimal pricePerNight = 100m,
        Guid? listingId = null) => new()
        {
            Id = listingId ?? Guid.NewGuid(),
            HostId = hostId,
            Title = "Test listing",
            MaxGuests = maxGuests,
            PricePerNight = pricePerNight,
            IsActive = true
        };

    public void GivenListing(Listing listing) =>
        ListingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(listing);

    public void GivenNoListing() =>
        ListingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync((Listing?)null);

    public void GivenAvailabilityBlocksClear(bool clear = true) =>
        ListingRepository
            .Setup(r => r.CheckListingAvailabilityBlocks(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(clear);

    public void GivenNoConflictingBookings(bool clear = true) =>
        ListingRepository
            .Setup(r => r.CheckListingBookings(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>()))
            .ReturnsAsync(clear);

    public void GivenUser(bool? isHost = null)
    {
        UserRepository
            .Setup(r => r.GetUserByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new AirbnbMVP.Models.Identity.User
            {
                Id = Guid.NewGuid(),
                IsHost = isHost ?? false
            });
    }
}
