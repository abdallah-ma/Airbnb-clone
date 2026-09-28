using AirbnbMVP.BLL.DTOs.Requests.Listing;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Services;
using AirbnbMVP.BLL.Specifications;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AirbnbMVP.Tests.Unit.Services;

public class ListingServiceTests
{
    private readonly Guid _hostId = Guid.NewGuid();

    private readonly Mock<IGenericRepository<Listing>> _listingRepository = new();
    private readonly Mock<IGenericRepository<Amenity>> _amenityRepository = new();
    private readonly Mock<IGenericRepository<Booking>> _bookingRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();

    private ListingService BuildService() => new(
        _listingRepository.Object,
        _amenityRepository.Object,
        _userRepository.Object,
        _bookingRepository.Object);

    private void GivenUser(bool? isHost = null, Guid? id = null) =>
        _userRepository
            .Setup(r => r.GetUserByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new User { Id = id ?? _hostId, IsHost = isHost ?? true });

    private void GivenAmenities(params Guid[] ids) =>
        _amenityRepository
            .Setup(r => r.GetAllAsync(It.IsAny<GetAmenitiesByIdSpecification>()))
            .ReturnsAsync(ids.Select(i => new Amenity { Id = i }).ToList());

    private static UpdateListingRequest UpdateRequest(Guid listingId, string? title = "New title") => new()
    {
        ListingId = listingId,
        Title = title!,
        Description = "d",
        Address = "a",
        City = "c",
        Country = "co",
        PropertyType = "Apartment",
        AmenityIds = []
    };

    private static Listing CreateListing(Guid hostId) => new()
    {
        Id = Guid.NewGuid(),
        HostId = hostId,
        Title = "Original title",
        Description = "d",
        Address = "a",
        City = "c",
        Country = "co",
        PropertyType = "Apartment",
        MaxGuests = 4,
        Bedrooms = 2,
        Bathrooms = 1,
        PricePerNight = 100m
    };

    // ---------- AddListingForHost ----------

    [Fact]
    public async Task AddListingForHost_WhenUserIsNotAHost_ThrowsForbidden()
    {
        GivenUser(isHost: false);

        var act = () => BuildService().AddListingForHost(new CreateListingRequestDto(), _hostId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("User is not registered as a host.");

        _listingRepository.Verify(r => r.AddAsync(It.IsAny<Listing>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task AddListingForHost_WhenUserMissing_ThrowsPlainException()
    {
        // Documents current behaviour: this path throws a bare Exception rather than
        // NotFoundException, so it escapes the AppException hierarchy and cannot be
        // mapped to an HTTP status by a future exception filter.
        _userRepository
            .Setup(r => r.GetUserByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((User?)null);

        var act = () => BuildService().AddListingForHost(new CreateListingRequestDto(), _hostId);

        var thrown = await act.Should().ThrowAsync<Exception>();
        thrown.Which.Should().BeOfType<Exception>();
        thrown.Which.Should().NotBeAssignableTo<AppException>();
    }

    [Fact]
    public async Task AddListingForHost_WhenSomeAmenityIdsAreUnknown_ThrowsBadRequest()
    {
        GivenUser();
        var request = new CreateListingRequestDto
        {
            Title = "T",
            Description = "D",
            Address = "A",
            City = "C",
            Country = "CO",
            PropertyType = "Apartment",
            AmenityIds = [Guid.NewGuid(), Guid.NewGuid()]
        };
        GivenAmenities(Guid.NewGuid()); // only one of the two resolves

        var act = () => BuildService().AddListingForHost(request, _hostId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("One or more amenity IDs are invalid.");

        _listingRepository.Verify(r => r.AddAsync(It.IsAny<Listing>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task AddListingForHost_PersistsListingWithActiveFlag()
    {
        GivenUser();
        GivenAmenities();
        var request = new CreateListingRequestDto
        {
            Title = "T",
            Description = "D",
            Address = "A",
            City = "C",
            Country = "CO",
            PropertyType = "Apartment",
            MaxGuests = 5,
            PricePerNight = 220m,
            AmenityIds = []
        };

        await BuildService().AddListingForHost(request, _hostId);

        _listingRepository.Verify(r => r.AddAsync(It.Is<Listing>(l =>
            l.HostId == _hostId &&
            l.Title == "T" &&
            l.IsActive &&
            l.MaxGuests == 5 &&
            l.PricePerNight == 220m), true), Times.Once);
    }

    [Fact]
    public async Task AddListingForHost_ReturnsIdThatIsNotThePersistedListingId()
    {
        // Documents a defect: the response DTO is built with a fresh Guid.NewGuid() instead of
        // the entity's Id, so the client receives an id that does not exist in the database.
        GivenUser();
        GivenAmenities();
        Guid? persistedId = null;
        _listingRepository
            .Setup(r => r.AddAsync(It.IsAny<Listing>(), It.IsAny<bool>()))
            .Callback<Listing, bool>((l, _) => persistedId = l.Id)
            .ReturnsAsync(1);

        var result = await BuildService().AddListingForHost(
            new CreateListingRequestDto
            {
                Title = "T", Description = "D", Address = "A", City = "C",
                Country = "CO", PropertyType = "Apartment", AmenityIds = []
            },
            _hostId);

        persistedId.Should().NotBeNull();
        result.Id.Should().NotBe(persistedId.Value);
    }

    // ---------- RemoveListing ----------

    [Fact]
    public async Task RemoveListing_WhenUserIsHost_ThrowsForbidden()
    {
        // Documents a defect: the host check is inverted. A host - the only user who should be
        // able to delete their own listing - is rejected, while non-hosts are allowed through.
        GivenUser(isHost: true);

        var act = () => BuildService().RemoveListing(Guid.NewGuid(), _hostId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Can't delete listing");

        _listingRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RemoveListing_WhenUserIsNotHost_DeletesTheListing()
    {
        // Documents the other half of the same defect: a non-host is allowed to delete.
        GivenUser(isHost: false);
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(CreateListing(_hostId));
        _bookingRepository
            .Setup(r => r.GetAsync(It.IsAny<AirbnbMVP.BLL.Specifications.Bookings.BookingsByListingSpecification>()))
            .ReturnsAsync((Booking?)null);

        await BuildService().RemoveListing(Guid.NewGuid(), _hostId);

        _listingRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task RemoveListing_WhenUserMissing_ThrowsNotFound()
    {
        _userRepository
            .Setup(r => r.GetUserByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((User?)null);

        var act = () => BuildService().RemoveListing(Guid.NewGuid(), _hostId);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("User not found.");
    }

    // ---------- UpdateListing ----------

    [Fact]
    public async Task UpdateListing_WhenUserIsNotHost_ThrowsForbidden()
    {
        GivenUser(isHost: false);

        var act = () => BuildService().UpdateListing(UpdateRequest(Guid.NewGuid()), _hostId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Can't update listing");
    }

    [Fact]
    public async Task UpdateListing_WhenListingMissing_ThrowsNullReferenceException()
    {
        // Documents a defect: listing.HostId is dereferenced before the null check, so a missing
        // listing produces a NullReferenceException instead of NotFoundException.
        GivenUser();
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync((Listing?)null);

        var act = () => BuildService().UpdateListing(UpdateRequest(Guid.NewGuid()), _hostId);

        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task UpdateListing_WhenCallerDoesNotOwnListing_ThrowsForbidden()
    {
        GivenUser();
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(CreateListing(Guid.NewGuid())); // owned by somebody else

        var act = () => BuildService().UpdateListing(UpdateRequest(Guid.NewGuid()), _hostId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You do not own this listing.");
    }

    [Fact]
    public async Task UpdateListing_WhenSomeAmenityIdsAreUnknown_ThrowsBadRequest()
    {
        GivenUser();
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(CreateListing(_hostId));
        var request = UpdateRequest(Guid.NewGuid());
        request.AmenityIds = [Guid.NewGuid()];
        GivenAmenities();

        var act = () => BuildService().UpdateListing(request, _hostId);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("One or more amenity IDs are invalid.");

        _listingRepository.Verify(r => r.UpdateAsync(It.IsAny<Listing>()), Times.Never);
    }

    [Fact]
    public async Task UpdateListing_WhenAmenityIdsOmitted_ThrowsNullReferenceException()
    {
        // Documents a defect: AmenityIds is declared nullable, but the service dereferences it
        // unconditionally to compare counts, so any partial update that omits amenities crashes
        // with a NullReferenceException instead of leaving the existing amenities untouched.
        GivenUser();
        var listing = CreateListing(_hostId);
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(listing);

        var request = new UpdateListingRequest { ListingId = listing.Id, Title = "Renamed" };
        request.AmenityIds.Should().BeNull();

        var act = () => BuildService().UpdateListing(request, _hostId);

        await act.Should().ThrowAsync<NullReferenceException>();

        _listingRepository.Verify(r => r.UpdateAsync(It.IsAny<Listing>()), Times.Never);
    }

    [Fact]
    public async Task UpdateListing_KeepsExistingValuesForOmittedFields()
    {
        GivenUser();
        var listing = CreateListing(_hostId);
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(listing);
        GivenAmenities();

        var request = new UpdateListingRequest { ListingId = listing.Id, AmenityIds = [] };
        await BuildService().UpdateListing(request, _hostId);

        listing.Title.Should().Be("Original title");
        listing.MaxGuests.Should().Be(4);
        listing.PricePerNight.Should().Be(100m);
        _listingRepository.Verify(r => r.UpdateAsync(listing), Times.Once);
    }

    [Fact]
    public async Task UpdateListing_AppliesSuppliedValues()
    {
        GivenUser();
        var listing = CreateListing(_hostId);
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(listing);
        GivenAmenities();

        var request = UpdateRequest(listing.Id, title: "Renamed");
        request.PricePerNight = 333m;
        request.MaxGuests = 9;

        var result = await BuildService().UpdateListing(request, _hostId);

        listing.Title.Should().Be("Renamed");
        listing.PricePerNight.Should().Be(333m);
        listing.MaxGuests.Should().Be(9);
        result.Title.Should().Be("Renamed");
    }

    [Fact]
    public async Task UpdateListing_WhenUpdateThrowsConcurrencyException_TranslatesToConflict()
    {
        GivenUser();
        var listing = CreateListing(_hostId);
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(listing);
        GivenAmenities();
        _listingRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Listing>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("stale rowversion"));

        var act = () => BuildService().UpdateListing(UpdateRequest(listing.Id), _hostId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*modified by someone else*");
    }

    [Fact]
    public async Task UpdateListing_ReturnsIdThatIsNotThePersistedListingId()
    {
        // Same defect as AddListingForHost: the response carries a fresh Guid.
        GivenUser();
        var listing = CreateListing(_hostId);
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(listing);
        GivenAmenities();

        var result = await BuildService().UpdateListing(UpdateRequest(listing.Id), _hostId);

        result.Id.Should().NotBe(listing.Id);
    }

    [Fact]
    public async Task UpdateListing_ReplacesAmenityAssociations()
    {
        GivenUser();
        var listing = CreateListing(_hostId);
        _listingRepository
            .Setup(r => r.GetAsync(It.IsAny<GetByIdSpecification<Listing>>()))
            .ReturnsAsync(listing);

        var amenityA = Guid.NewGuid();
        var amenityB = Guid.NewGuid();
        GivenAmenities(amenityA, amenityB);

        var request = UpdateRequest(listing.Id);
        request.AmenityIds = [amenityA, amenityB];

        await BuildService().UpdateListing(request, _hostId);

        listing.ListingAmenities.Should().HaveCount(2);
        listing.ListingAmenities.Select(a => a.AmenityId).Should().BeEquivalentTo([amenityA, amenityB]);
    }
}
