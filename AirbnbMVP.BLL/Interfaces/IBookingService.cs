using AirbnbMVP.BLL.DTOs.Requests.Booking;
using AirbnbMVP.BLL.DTOs.Responses;


namespace AirbnbMVP.BLL.Interfaces
{
    public interface IBookingService
    {


        Task<IEnumerable<BookingResponseDto>> GetBookingsForUserAsync(Guid userId);

        Task<BookingResponseDto> GetBookingAsync(Guid bookingId);

        Task<IEnumerable<BookingResponseDto>> GetBookingsForListingAsync(Guid listingId, Guid userId);

        Task<BookingResponseDto> BookListingAsync(BookingRequestDto newBooking, Guid userId);

        Task<BookingResponseDto> UpdateBookingAsync(UpdateBookingRequest updatedBooking, Guid userId);

        Task CancelBookingAsync(Guid bookingId , Guid userId);

        Task ConfirmBookingPaymentAsync(Guid bookingId, string intentId);

        Task FailBookingPaymentAsync(Guid bookingId);



    }
}
