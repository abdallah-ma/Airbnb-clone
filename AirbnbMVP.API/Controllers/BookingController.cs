using AirbnbMVP.BLL.DTOs.Requests.Booking;
using AirbnbMVP.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AirbnbMVP.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("/[controller]")]

    public class BookingController : ControllerBase
    {
        private readonly IBookingService BookingService;
        

        public BookingController(IBookingService bookingService)
        {
            BookingService = bookingService;
        }

        [HttpGet("Bookings")]
        public async Task<IActionResult> GetBookings()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var bookings = await BookingService.GetBookingsForUserAsync( userId );

            if(bookings == null)
            {
                return NotFound();
            }

            return Ok(bookings);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBooking(Guid bookingId)
        {

            var booking = await BookingService.GetBookingAsync(bookingId);

            if(booking == null)
            {
                return NotFound();
            }

            return Ok(booking);
        }

        [HttpDelete("ListingBookings/{id}")]
        public async Task<IActionResult> GetBookingsForListing(Guid listingId)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var bookings = await BookingService.GetBookingsForListingAsync(listingId , userId);

            if (bookings == null)
            {
                return NotFound();
            }

            return Ok(bookings);
        }

        [HttpPost("BookListing")]
        public async Task<IActionResult> BookListing([FromBody] BookingRequestDto request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var result = await BookingService.BookListingAsync(request, userId);

            return Ok(result);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelBooking(Guid bookingId)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            await BookingService.CancelBookingAsync(bookingId, userId);

            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateBooking(UpdateBookingRequest request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var result = await BookingService.UpdateBookingAsync(request, userId);

            return Ok(result);
        }

    }
}
