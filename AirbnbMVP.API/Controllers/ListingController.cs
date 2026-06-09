using AirbnbMVP.BLL.DTOs.Requests.Listing;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Specifications.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AirbnbMVP.API.Controllers
{
    [ApiController]
    [Route("/[controller]")]

    public class ListingController : ControllerBase
    {

        private readonly IListingService ListingService;

        public ListingController(IListingService listingService)
        {
            ListingService = listingService;
        }

        [HttpGet]
        public async Task<IActionResult> GetListings([FromQuery] ListingFilterParams filters)
        {

            var specs = new ListingFilterSpecification(filters);    
            

            var listings = await ListingService.GetAllListings(specs);


            return Ok(listings);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetListing(Guid id)
        {


            var listing = await ListingService.GetListing(new ListingDetailsSpecification(id));

            return Ok(listing);
        }

        [HttpPut]
        [Authorize]

        public async Task<IActionResult> UpdateListing(UpdateListingRequest request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var result = await ListingService.UpdateListing(request, userId);

            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize]

        public async Task<IActionResult> RemoveListing(Guid listingId)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            await ListingService.RemoveListing(listingId , userId);

            return Ok();
        }

    }
}
