using AirbnbMVP.BLL.DTOs.Requests;
using Stripe;

namespace AirbnbMVP.BLL.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentIntentResponse> CreatePaymentIntentAsync(decimal amount, Guid bookingId);
        Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId);
        Task CancelPaymentIntentAsync(string paymentIntentId);
        Task HandleWebhookAsync(string json, string stripeSignature);

        Task CreateRefundAsync(string paymentReference, decimal amount);

    }
}
