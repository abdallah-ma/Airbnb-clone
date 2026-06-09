using AirbnbMVP.BLL.DTOs.Requests.Listing;
using AirbnbMVP.BLL.DTOs.Responses;
using AirbnbMVP.BLL.Specifications.Listings;
using AirbnbMVP.Models.Listings;


namespace AirbnbMVP.BLL.Interfaces
{
    public interface IListingService
    {

        Task<IEnumerable<Listing>?> GetAllListings(ListingFilterSpecification spec);

        Task<Listing?> GetListing(ListingDetailsSpecification spec);

        Task<ListingResponseDto> AddListingForHost(CreateListingRequestDto newListing , Guid userId);

        Task RemoveListing(Guid listingId, Guid userId);

        Task<ListingResponseDto> UpdateListing(UpdateListingRequest updatedListing, Guid userId);

        

    }
}
