using AirbnbMVP.BLL.DTOs.Requests;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Specifications;
using AirbnbMVP.BLL.Specifications.Transactions;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Bookings;
using Microsoft.Extensions.Configuration;
using Stripe;


namespace AirbnbMVP.BLL.Services
{
    public class PaymentService : IPaymentService
    {

        private readonly IGenericRepository<Transaction> TransactionRepository;
        private readonly IConfiguration Configuration;
        private readonly IGenericRepository<Booking> BookingRepository;

        public PaymentService(
            IGenericRepository<Transaction> transactionRepository,
            IGenericRepository<Booking> bookingService,
            IConfiguration configuration)
        {
            TransactionRepository = transactionRepository;
            BookingRepository = bookingService;
            Configuration = configuration;
        }


        public async Task CancelPaymentIntentAsync(string paymentIntentId)
        {
            var service = new PaymentIntentService();
            await service.CancelAsync(paymentIntentId);
        }


        public async Task<PaymentIntentResponse> CreatePaymentIntentAsync(decimal amount , Guid bookingId)
        {
            
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = "usd",
               
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
                ,Metadata = new Dictionary<string, string>()
                {
                    ["bookingId"]  = bookingId.ToString()
                }
                
            };

            var PaymentIntentService = new PaymentIntentService();

            var paymentIntent = await PaymentIntentService.CreateAsync(options);

            

            return new PaymentIntentResponse
            {
                ClientSecret = paymentIntent.ClientSecret,
                PaymentIntentId = paymentIntent.Id,
                Status = paymentIntent.Status
            };
        }

        public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId)
        {
            var PaymentIntentService = new PaymentIntentService();

            return await PaymentIntentService.GetAsync(paymentIntentId);
        }

        public async Task CreateRefundAsync(string paymentReference, decimal amount)
        {
            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentReference,
                Amount = (long)(amount * 100)
            };

            var service = new RefundService();
            await service.CreateAsync(options);
        }

        public async Task HandleWebhookAsync(string json, string signature)
        {
            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    Configuration["Stripe:WebhookSecret"],
                    throwOnApiVersionMismatch: false);
            }
            catch (StripeException)
            {
                throw new BadRequestException("Invalid Stripe webhook signature.");
            }

            // 1. Wrap your PaymentIntent logic safely so other events don't crash it
            if (stripeEvent.Type.StartsWith("payment_intent."))
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;

                if (intent == null)
                {
                    Console.WriteLine($"[Webhook Warning] Failed to cast event object to PaymentIntent for type: {stripeEvent.Type}");
                    return; // Exit early safely
                }

                // Ensure metadata key exists before parsing to avoid key-not-found crashes
                if (!intent.Metadata.TryGetValue("bookingId", out var bookingIdStr) || !Guid.TryParse(bookingIdStr, out var bookingId))
                {
                    Console.WriteLine($"[Webhook Warning] No valid bookingId metadata found on PaymentIntent: {intent.Id}");
                    return;
                }

                switch (stripeEvent.Type)
                {
                    case EventTypes.PaymentIntentSucceeded:
                        await ConfirmPaymentAsync(bookingId, intent.Id);
                        break;

                    case EventTypes.PaymentIntentPaymentFailed:
                    case EventTypes.PaymentIntentCanceled:
                        await FailPaymentAsync(bookingId);
                        break;
                }
            }
            else
            {
                // 2. Safely log and acknowledge other events (like charge.updated) without crashing
                Console.WriteLine($"[Webhook Info] Received unhandled event type: {stripeEvent.Type}");
            }
        }

        private async Task ConfirmPaymentAsync(Guid bookingId, string paymentReference)
        {
            var booking = await BookingRepository.GetAsync(new GetByIdSpecification<Booking>(bookingId)) ?? throw new NotFoundException("Booking not found.");


            booking.Status = BookingStatus.Confirmed;
            await BookingRepository.UpdateAsync(booking);

            var transaction = await TransactionRepository.GetAsync(
                 new TransactionSpecification(bookingId, TransactionType.Payment, TransactionStatus.Pending))
                     ?? throw new NotFoundException("Transaction not found.");

            transaction.Status = TransactionStatus.Completed;
            transaction.PaymentReference = paymentReference;
            await TransactionRepository.UpdateAsync(transaction);
        }

        private async Task FailPaymentAsync(Guid bookingId)
        {
            var booking = await BookingRepository.GetAsync(new GetByIdSpecification<Booking>(bookingId)) ?? throw new NotFoundException("Booking not found.");

            booking.Status = BookingStatus.Cancelled;
            await BookingRepository.UpdateAsync(booking);

            var transaction = await TransactionRepository.GetAsync(
                new TransactionSpecification(bookingId , TransactionType.Payment , TransactionStatus.Pending)) 
                    ?? throw new NotFoundException("Transaction not found.");

            transaction.Status = TransactionStatus.Failed;
            await TransactionRepository.UpdateAsync(transaction);
        }
    }

}

