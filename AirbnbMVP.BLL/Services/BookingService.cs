using AirbnbMVP.BLL.DTOs.Requests.Booking;
using AirbnbMVP.BLL.DTOs.Responses;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Specifications;
using AirbnbMVP.BLL.Specifications.Bookings;
using AirbnbMVP.BLL.Specifications.Transactions;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;



namespace AirbnbMVP.BLL.Services
{
    public class BookingService : IBookingService
    {

        private readonly IListingRepository ListingRepository;

        private readonly IGenericRepository<Booking> BookingRepository;

        private readonly IGenericRepository<Transaction> TransactionRepository;

        private readonly IUserRepository UserRepository;

        private readonly IPaymentService PaymentService;

        public BookingService(IListingRepository listingRepository, IGenericRepository<Booking> bookingRepository, IUserRepository userRepository, IGenericRepository<Transaction> transactionRepository , IPaymentService paymentService)
        {
            ListingRepository = listingRepository;
            BookingRepository = bookingRepository;
            UserRepository = userRepository;
            TransactionRepository = transactionRepository;
            PaymentService = paymentService;
        }

        public async Task<BookingResponseDto> BookListingAsync(BookingRequestDto newBooking, Guid userId)
        {

            if (newBooking.CheckIn >= newBooking.CheckOut)
                throw new BadRequestException("Check-out date must be after check-in date.");

            if (newBooking.CheckIn < DateOnly.FromDateTime(DateTime.UtcNow))
                throw new BadRequestException("Check-in date cannot be in the past.");

            await using var dbTransaction = await BookingRepository.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var listing = await ListingRepository.GetAsync(new GetByIdSpecification<Listing>(newBooking.ListingId)) ??
                              throw new NotFoundException("Listing not found.");

                if (!await ListingRepository.CheckListingAvailabilityBlocks(newBooking.ListingId, newBooking.CheckIn, newBooking.CheckOut))
                {
                    throw new ConflictException("The listing is not available on the requested dates.");
                }

                if (!await ListingRepository.CheckListingBookings(newBooking.ListingId, newBooking.CheckIn, newBooking.CheckOut, null))
                {
                    throw new ConflictException("The listing is booked on the requested dates.");
                }

                if (newBooking.NumGuests > listing.MaxGuests)
                    throw new BadRequestException($"This listing only allows up to {listing.MaxGuests} guests.");

                if (listing.HostId == userId)
                    throw new ForbiddenException("You cannot book your own listing.");

                var nights = (newBooking.CheckOut.DayNumber - newBooking.CheckIn.DayNumber);
                var totalPrice = nights * listing.PricePerNight;

                var booking = new Booking()
                {
                    Id = Guid.NewGuid(),
                    ListingId = listing.Id,
                    GuestId = userId,
                    CheckIn = newBooking.CheckIn,
                    CheckOut = newBooking.CheckOut,
                    NumGuests = newBooking.NumGuests,
                    TotalPrice = totalPrice,
                    Status = BookingStatus.Pending,
                    CreatedAt = DateTime.Now.Date
                };

                var transaction = new Transaction()
                {
                    BookingId = booking.Id,
                    PayerId = userId,
                    PayeeId = listing.HostId,
                    Amount = totalPrice,
                    Status = TransactionStatus.Pending,
                };

                await BookingRepository.AddAsync(booking, saveChanges: false);
                await TransactionRepository.AddAsync(transaction, saveChanges: false);

                await BookingRepository.SaveAsync();
                await dbTransaction.CommitAsync();

                var clientSecret = await PaymentService.CreatePaymentIntentAsync(booking.TotalPrice, booking.Id);

                return new BookingResponseDto()
                {
                    CheckIn = booking.CheckIn,
                    CheckOut = booking.CheckOut,
                    NumGuests = booking.NumGuests,
                    TotalPrice = booking.TotalPrice,
                    Status = BookingStatus.Pending,
                    CreatedAt = DateTime.UtcNow.Date,
                    ClientSecret = clientSecret.ClientSecret
                };
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 })
            {
                throw new ConflictException("The booking could not be completed at this time. Please try again.");
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                throw new ConflictException("The booking could not be completed at this time. Please try again.");
            }
        }

        public async Task CancelBookingAsync(Guid bookingId, Guid userId)
        {

            var user = await UserRepository.GetUserByIdAsync(userId) ??
                throw new NotFoundException("User not found.");

            var booking = await BookingRepository.GetAsync(new GetByIdSpecification<Booking>(bookingId)) ??
                throw new NotFoundException("There's no booking with this id.");

            if (booking.GuestId != userId)
            {
                throw new ForbiddenException("You are not the user who made this booking.");
            }

            if (booking.Status == BookingStatus.Completed)
            {
                throw new BadRequestException("Cannot cancel a completed booking.");
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                throw new BadRequestException("Booking is already cancelled");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (booking.CheckIn <= today.AddDays(1))
                throw new BadRequestException("Cannot cancel a booking within 24 hours of check-in.");

            booking.Status = BookingStatus.Cancelled;
            await BookingRepository.UpdateAsync(booking);

            var transaction = await TransactionRepository.GetAsync(
                new TransactionSpecification(bookingId, TransactionType.Payment, TransactionStatus.Completed));

            if (transaction != null)
            {
                await PaymentService.CreateRefundAsync(transaction.PaymentReference!, booking.TotalPrice);

                var refund = new Transaction
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    PayerId = booking.GuestId,
                    PayeeId = booking.GuestId, // money goes back to guest
                    Amount = booking.TotalPrice,
                    Type = TransactionType.Refund,
                    Status = TransactionStatus.Completed,
                    ProcessedAt = DateTime.UtcNow
                };
                await TransactionRepository.AddAsync(refund);

            }


        }

        public async Task<BookingResponseDto> GetBookingAsync(Guid bookingId)
        {



            var booking = await BookingRepository.GetAsync(new BookingWithDetailsSpecification(bookingId));

            return new BookingResponseDto()
            {
                CheckIn = booking.CheckIn,
                CheckOut = booking.CheckOut,
                NumGuests = booking.NumGuests,
                CreatedAt = booking.CreatedAt,
                Status = booking.Status,
                TotalPrice = booking.TotalPrice,
                Guest = booking.Guest,
                Listing = booking.Listing,
                Review = booking.Review
            };

        }

        public async Task<IEnumerable<BookingResponseDto>> GetBookingsForUserAsync(Guid userId)
        {



            var bookings = await BookingRepository.GetAllAsync(new BookingsByGuestSpecification(userId));

            return bookings.Select(b => new BookingResponseDto
            {
                CheckIn = b.CheckIn,
                CheckOut = b.CheckOut,
                NumGuests = b.NumGuests,
                CreatedAt = b.CreatedAt,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                Guest = b.Guest,
                Listing = b.Listing,
                Review = b.Review
            });

        }

        public async Task<IEnumerable<BookingResponseDto>> GetBookingsForListingAsync(Guid listingId , Guid userId)
        {

            var listing = await ListingRepository.GetAsync(new GetByIdSpecification<Listing>(listingId))
                ?? throw new NotFoundException("Listing not found.");

            var user = await UserRepository.GetUserByIdAsync(userId)
                ?? throw new NotFoundException("User not found.");

            if(userId != listing.HostId)
            {
                throw new ForbiddenException("User is not owner of listing.");
            }

            var bookings = await BookingRepository.GetAllAsync(new BookingsByListingSpecification(listingId));

            var result = bookings.Select(b => new BookingResponseDto
            {
                 CheckIn = b.CheckIn,
                 CheckOut= b.CheckOut,
                 CreatedAt= b.CreatedAt,
                 NumGuests= b.NumGuests,
                 Status = b.Status,
                 TotalPrice= b.TotalPrice
            });

            return result;

        }

        public async Task<BookingResponseDto> UpdateBookingAsync(UpdateBookingRequest updatedBooking, Guid userId)
        {

            var user = await UserRepository.GetUserByIdAsync(userId) ??
                        throw new NotFoundException("User not found.");




            var booking = await BookingRepository.GetAsync(new BookingWithListingSpecification(updatedBooking.BookingId)) ??
                throw new NotFoundException("There's no booking with this id.");

            if (booking.GuestId != userId)
                throw new ForbiddenException("You do not own this booking.");


            var checkIn = updatedBooking.CheckIn ?? booking.CheckIn;
            var checkOut = updatedBooking.CheckOut ?? booking.CheckOut;
            var numGuests = updatedBooking.NumGuests ?? booking.NumGuests;


            
            
            if (booking.Status != BookingStatus.Pending)
                throw new BadRequestException("Only pending bookings can be modified.");


            if (numGuests > booking.Listing.MaxGuests)
                throw new BadRequestException($"This listing only allows up to {booking.Listing.MaxGuests} guests.");

            if (checkIn >= checkOut)
                throw new BadRequestException("Check-out date must be after check-in date.");


            if (!await ListingRepository.CheckListingAvailabilityBlocks(booking.ListingId, checkIn, checkOut))
            {
                throw new ConflictException("The listing is not available on the requested dates.");
            }

            if (!await ListingRepository.CheckListingBookings(booking.ListingId, checkIn, checkOut, booking.Id))
            {
                throw new ConflictException("The listing is booked on the requested dates.");

            }

            var nights = checkOut.DayNumber - checkIn.DayNumber;

            booking.CheckIn = checkIn;
            booking.CheckOut = checkOut;
            booking.NumGuests = numGuests;
            booking.TotalPrice = nights * booking.Listing.PricePerNight;

            await BookingRepository.UpdateAsync(booking);


            return new BookingResponseDto
            {
                
                CheckIn = booking.CheckIn,
                CheckOut = booking.CheckOut,
                NumGuests = booking.NumGuests,
                TotalPrice = booking.TotalPrice,
                Status = booking.Status,
                CreatedAt = booking.CreatedAt
            };

        }


        public async Task ConfirmBookingPaymentAsync(Guid bookingId, string paymentReference)
        {
            var booking = await BookingRepository.GetAsync(new GetByIdSpecification<Booking>(bookingId) ) ?? throw new NotFoundException("Booking not found.");

            booking.Status = BookingStatus.Confirmed;
            await BookingRepository.UpdateAsync(booking);

            var transaction = await TransactionRepository.GetAsync(new TransactionSpecification(bookingId , TransactionType.Payment , TransactionStatus.Pending))
                ?? throw new NotFoundException("Transaction not found.");

            transaction.Status = TransactionStatus.Completed;
            transaction.PaymentReference = paymentReference;
            await TransactionRepository.UpdateAsync(transaction);
        }

        public async Task FailBookingPaymentAsync(Guid bookingId)
        {
            var booking = await BookingRepository.GetAsync(new GetByIdSpecification<Booking>(bookingId)) ?? throw new NotFoundException("Booking not found.");

            booking.Status = BookingStatus.Cancelled;
            await BookingRepository.UpdateAsync(booking);

            var transaction = await TransactionRepository.GetAsync(new TransactionSpecification(bookingId, TransactionType.Payment, TransactionStatus.Pending))
                ?? throw new NotFoundException("Transaction not found.");

            transaction.Status = TransactionStatus.Failed;
            await TransactionRepository.UpdateAsync(transaction);
        }

    }
}
