using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Services;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.DAL.Repositories;
using AirbnbMVP.Models.Bookings;

using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using System.Transactions;

using Transaction = AirbnbMVP.DAL.Models.Transaction;

namespace AirbnbMVP.API.Extenstions
{
    public static class AddServices
    {

        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddScoped<IGenericRepository<Listing>, GenericRepository<Listing>>();
            services.AddScoped<IGenericRepository<Booking>, GenericRepository<Booking>>();
            services.AddScoped<IGenericRepository<Review>, GenericRepository<Review>>();
            services.AddScoped<IGenericRepository<Amenity>, GenericRepository<Amenity>>();

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IListingRepository, ListingRepository>();

            services.AddScoped<IGenericRepository<Transaction>, GenericRepository<Transaction>>();

            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IListingService, ListingService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IUserService, UserService>();

            return services;
        }

    }
}
