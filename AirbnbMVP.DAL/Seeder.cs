using AirbnbMVP.Data;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirbnbMVP.DAL
{
    public static class Seeder
    {




        public static  void SeedListings(AirbnbDbContext Context)
        {
            if(Context.Listings.IsNullOrEmpty())
            {
                var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "Listings.json");


                string json = File.ReadAllText(path);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

                var listings = JsonSerializer.Deserialize<Listing[]>(json, options);

                Context.Listings.AddRange(listings);
                Context.SaveChanges();
            }
        }

        public static  void SeedUsers(AirbnbDbContext Context)
        {
            if (Context.Users.IsNullOrEmpty())
            {

                var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "Users.json");

                string json = File.ReadAllText(path);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

                var users = JsonSerializer.Deserialize<User[]>(json, options);

                Context.Users.AddRange(users);
                Context.SaveChanges();

            }
        }

        public static  void SeedAmenities(AirbnbDbContext Context)
        {
            if (Context.Amenities.IsNullOrEmpty())
            {
                var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "Amenities.json");

                string json = File.ReadAllText(path);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

                var amenities = JsonSerializer.Deserialize<Amenity[]>(json, options);

                Context.Amenities.AddRange(amenities);
                Context.SaveChanges();

            }
        }

        public static  void SeedBookings(AirbnbDbContext Context)
        {
            if (Context.Bookings.IsNullOrEmpty())
            {
                var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "Bookings.json");
                
                string json = File.ReadAllText(path);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true , Converters = { new JsonStringEnumConverter() } };

                var bookings = JsonSerializer.Deserialize<Booking[]>(json, options);

                Context.Bookings.AddRange(bookings);
                Context.SaveChanges();

            }
        }


        public static  void SeedReviews(AirbnbDbContext Context)
        {
            if (Context.Reviews.IsNullOrEmpty())
            {

                var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "Reviews.json");

                string json = File.ReadAllText(path);


                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true , Converters = { new JsonStringEnumConverter() } };

                var reviews = JsonSerializer.Deserialize<Review[]>(json, options);

                Context.Reviews.AddRange(reviews);
                Context.SaveChanges();

            }
        }

        public static void SeedListingsPhotos(AirbnbDbContext Context)
        {
            if (Context.ListingPhotos.IsNullOrEmpty())
            {
                var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "ListingsPhotos.json");


                string json = File.ReadAllText(path);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

                var listingsPhotos = JsonSerializer.Deserialize<ListingPhoto[]>(json, options);

                Context.ListingPhotos.AddRange(listingsPhotos);
                Context.SaveChanges();
            }
        }

    }
}
