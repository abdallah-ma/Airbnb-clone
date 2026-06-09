using AirbnbMVP.BLL.DTOs.Requests.Listing;
using AirbnbMVP.BLL.DTOs.Responses;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Specifications;
using AirbnbMVP.BLL.Specifications.Bookings;
using AirbnbMVP.BLL.Specifications.Listings;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;

namespace AirbnbMVP.BLL.Services
{
    public class ListingService : IListingService
    {

        private readonly IGenericRepository<Listing> ListingRepository;

        private readonly IGenericRepository<Amenity> AmenityRepository;

        private readonly IGenericRepository<Booking> BookingRepository;


        private readonly IUserRepository UserRepository;

        public ListingService(
            IGenericRepository<Listing> listingRepository,
            IGenericRepository<Amenity> amenityRepository,
            IUserRepository userRepository,
            IGenericRepository<Booking> bookingRepository)
        {
            ListingRepository = listingRepository;
            AmenityRepository = amenityRepository;
            UserRepository = userRepository;
            BookingRepository = bookingRepository;
        }


        public async Task<ListingResponseDto> AddListingForHost(CreateListingRequestDto newListing, Guid userId)
        {

            var user = await UserRepository.GetUserByIdAsync(userId)
                    ?? throw new Exception("User not found.");
            if (!user.IsHost)
            {
                throw new ForbiddenException("User is not registered as a host.");
            }

            
            var listing = new Listing
            {
                Id = Guid.NewGuid(),
                HostId = userId,
                Title = newListing.Title,
                Description = newListing.Description,
                Address = newListing.Address,
                City = newListing.City,
                Country = newListing.Country,
                PropertyType = newListing.PropertyType,
                MaxGuests = newListing.MaxGuests,
                Bedrooms = newListing.Bedrooms,
                Bathrooms = newListing.Bathrooms,
                PricePerNight = newListing.PricePerNight,
                Latitude = newListing.Latitude,
                Longitude = newListing.Longitude,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var amenities = await AmenityRepository.GetAllAsync(new GetAmenitiesByIdSpecification(newListing.AmenityIds) );

            if(amenities.Count() != newListing.AmenityIds.Count)
            {
                throw new BadRequestException("One or more amenity IDs are invalid.");
            }

            await ListingRepository.AddAsync(listing);

            return new ListingResponseDto()
            {
                Id = Guid.NewGuid(),
                Title = newListing.Title,
                Description = newListing.Description,
                Address = newListing.Address,
                City = newListing.City,
                Country = newListing.Country,
                PropertyType = newListing.PropertyType,
                MaxGuests = newListing.MaxGuests,
                Bedrooms = newListing.Bedrooms,
                Bathrooms = newListing.Bathrooms,
                PricePerNight = newListing.PricePerNight,
                Latitude = newListing.Latitude,
                Longitude = newListing.Longitude,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public async Task<IEnumerable<Listing>?> GetAllListings(ListingFilterSpecification spec)
        {
            

            return await ListingRepository.GetAllAsync(spec);
        }

        public async Task<Listing?> GetListing(ListingDetailsSpecification spec)
        {
            var listing = await ListingRepository.GetAsync(spec);


            return listing;
        }

        public async Task RemoveListing(Guid listingId , Guid userId)
        {

            var user = await UserRepository.GetUserByIdAsync(userId)
                    ?? throw new NotFoundException("User not found.");
            if (user.IsHost)
            {
                throw new ForbiddenException("Can't delete listing");
            }

            if(await ListingRepository.GetAsync(new GetByIdSpecification<Listing>(listingId) ) is null)
            {
                throw new NotFoundException("Listing not found.");
            }

            

            if(await BookingRepository.GetAsync( new BookingsByListingSpecification(listingId) ) is not null)
            {
                throw new ForbiddenException("Can't delete listing with active bookings");
            }

            await ListingRepository.DeleteAsync(listingId);

        }

        public async Task<ListingResponseDto> UpdateListing(UpdateListingRequest updatedListing , Guid userId)
        {
            var user = await UserRepository.GetUserByIdAsync(userId)
                    ?? throw new NotFoundException("User not found.");

            if (!user.IsHost)
            {
                throw new ForbiddenException("Can't update listing");
            }

            var listing = await ListingRepository.GetAsync(new GetByIdSpecification<Listing>(updatedListing.ListingId));

            if(user.Id != listing.HostId)
            {
                throw new ForbiddenException("You do not own this listing.");
            }

            if (listing is null)
            {
                throw new NotFoundException("Can't find listing");
            }


            var amenities = await AmenityRepository.GetAllAsync(new GetAmenitiesByIdSpecification(updatedListing.AmenityIds));

            if (amenities.Count() != updatedListing.AmenityIds.Count)
            {
                throw new BadRequestException("One or more amenity IDs are invalid.");
            }

            listing.ListingAmenities = amenities.Select(a => new ListingAmenity { AmenityId = a.Id}).ToList();

            listing.Title = updatedListing.Title ?? listing.Title;
            listing.Description = updatedListing.Description ?? listing.Description;
            listing.Address = updatedListing.Address ?? listing.Address;
            listing.City = updatedListing.City ?? listing.City;
            listing.Country = updatedListing.Country ?? listing.Country;
            listing.PropertyType = updatedListing.PropertyType ?? listing.PropertyType;
            listing.PricePerNight = updatedListing.PricePerNight ?? listing.PricePerNight;
            listing.MaxGuests = updatedListing.MaxGuests ?? listing.MaxGuests;
            listing.Bedrooms = updatedListing.Bedrooms ?? listing.Bedrooms;
            listing.Bathrooms = updatedListing.Bathrooms ?? listing.Bathrooms;
            listing.Latitude = updatedListing.Latitude ?? listing.Latitude;
            listing.Longitude = updatedListing.Longitude ?? listing.Longitude;


            await ListingRepository.UpdateAsync(listing);

            return new ListingResponseDto()
            {
                Id = Guid.NewGuid(),
                Title = listing.Title,
                Description = listing.Description,
                Address = listing.Address,
                City = listing.City,
                Country = listing.Country,
                PropertyType = listing.PropertyType,
                MaxGuests = listing.MaxGuests,
                Bedrooms = listing.Bedrooms,
                Bathrooms = listing.Bathrooms,
                PricePerNight = listing.PricePerNight,
                Latitude = listing.Latitude,
                Longitude = listing.Longitude,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

        }

    }
}
