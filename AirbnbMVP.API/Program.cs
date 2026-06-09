
using AirbnbMVP.API.Extenstions;
using AirbnbMVP.DAL;
using AirbnbMVP.Data;
using AirbnbMVP.Models.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System.Text;

namespace AirbnbMvp.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            builder.Services.AddControllers();

            builder.Services.AddSqlServer<AirbnbDbContext>(builder.Configuration.GetConnectionString("DefaultConnection"));

            builder.Services.AddIdentity<User,IdentityRole<Guid>>().AddEntityFrameworkStores<AirbnbDbContext>();

            StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

            builder.Services.AddOpenApi();

            builder.Services.AddCustomServices();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngular", policy =>
                {
                    policy.WithOrigins("http://localhost:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // needed for HttpOnly cookies
                });
            });


            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.ContainsKey("token"))
                        {
                            context.Token = context.Request.Cookies["token"];
                        }
                        return Task.CompletedTask;
                    }
                };
            });



            var app = builder.Build();






            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                using (var scope = app.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;
                    try
                    {
                        var context = services.GetRequiredService<AirbnbDbContext>();



                        // Call your seeding function
                        Seeder.SeedAmenities(context);
                        Seeder.SeedUsers(context);
                        Seeder.SeedListings(context);
                        Seeder.SeedBookings(context);
                        Seeder.SeedReviews(context);
                        Seeder.SeedListingsPhotos(context);
                    }
                    catch (Exception ex)
                    {
                        var logger = services.GetRequiredService<ILogger<Program>>();
                        logger.LogError(ex, "An error occurred while seeding the database.");
                    }
                }

                app.MapOpenApi();
            }
            app.UseRouting();

            app.UseCors("AllowAngular");

            app.UseStaticFiles();

            app.UseHttpsRedirection();

            app.UseAuthentication();


            app.UseAuthorization();


            app.MapControllers();


            app.Run();
        }
    }
}
