using AirbnbMVP.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AirbnbMVP.API.Controllers
{

    [ApiController]
    [Route("api/webhooks/stripe")]
    [AllowAnonymous]
    [DisableCors]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IPaymentService PaymentService;

        public StripeWebhookController(IPaymentService paymentService)
        {
            PaymentService = paymentService;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"];

            await PaymentService.HandleWebhookAsync(json, signature);

            return Ok();
        }
    }

}
