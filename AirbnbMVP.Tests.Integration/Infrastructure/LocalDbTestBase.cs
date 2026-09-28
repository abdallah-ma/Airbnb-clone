using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.BLL.Services;
using AirbnbMVP.Data;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.DAL.Repositories;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using AirbnbMVP.Models.Listings;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AirbnbMVP.Tests.Integration.Infrastructure;

/// <summary>
/// Gives each test class its own throwaway database on the local SQL Server instance
/// (LocalDB), migrated from the real migration history.
///
/// A real server is required: the booking flow relies on SERIALIZABLE isolation, the
/// UPDLOCK/HOLDLOCK table hints and rowversion columns, none of which the EF in-memory
/// provider can emulate.
/// </summary>
public abstract class LocalDbTestBase : IAsyncLifetime
{
    private const string DataSource = @"(localdb)\MSSQLLocalDB";

    private string _databaseName = string.Empty;

    protected string ConnectionString { get; private set; } = string.Empty;

    public virtual async Task InitializeAsync()
    {
        _databaseName = $"AirbnbTests_{Guid.NewGuid():N}";

        await ExecuteOnMasterAsync($"CREATE DATABASE [{_databaseName}]");

        ConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = DataSource,
            InitialCatalog = _databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
            ConnectTimeout = 30
        }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_databaseName))
        {
            return;
        }

        try
        {
            // SINGLE_USER + ROLLBACK IMMEDIATE evicts any pooled connections still holding
            // the database open.
            await ExecuteOnMasterAsync($"""
                IF DB_ID('{_databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_databaseName}];
                END
                """);
        }
        catch (SqlException)
        {
            // A leaked test database is not worth failing the run over.
        }
    }

    private async Task ExecuteOnMasterAsync(string sql)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = DataSource,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Fresh context, so callers can use one per concurrent operation.</summary>
    protected AirbnbDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AirbnbDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.CommandTimeout(60))
            .Options;

        return new AirbnbDbContext(options);
    }

    // ---------- seeding helpers ----------

    protected async Task<User> SeedUserAsync(bool isHost = false)
    {
        await using var context = CreateContext();

        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"user{suffix}",
            NormalizedUserName = $"USER{suffix}".ToUpperInvariant(),
            Email = $"user{suffix}@example.com",
            NormalizedEmail = $"USER{suffix}@EXAMPLE.COM".ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User",
            IsHost = isHost,
            EmailConfirmed = true,
            PasswordHash = "AQAAAAIAAYagplaceholderhash",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    protected async Task<Listing> SeedListingAsync(
        Guid hostId,
        int maxGuests = 4,
        decimal pricePerNight = 100m)
    {
        await using var context = CreateContext();

        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            HostId = hostId,
            Title = "Integration test listing",
            Description = "Used by the automated test suite",
            Address = "1 Test Street",
            City = "Cairo",
            Country = "Egypt",
            PropertyType = "Apartment",
            MaxGuests = maxGuests,
            Bedrooms = 2,
            Bathrooms = 1,
            PricePerNight = pricePerNight,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Listings.Add(listing);
        await context.SaveChangesAsync();
        return listing;
    }

    protected async Task<Booking> SeedBookingAsync(
        Listing listing,
        Guid guestId,
        DateOnly checkIn,
        DateOnly checkOut,
        BookingStatus status = BookingStatus.Confirmed,
        int numGuests = 2,
        decimal totalPrice = 300m)
    {
        await using var context = CreateContext();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            GuestId = guestId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            NumGuests = numGuests,
            TotalPrice = totalPrice,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return booking;
    }

    protected async Task<AvailabilityBlock> SeedAvailabilityBlockAsync(
        Listing listing, DateOnly start, DateOnly end, string reason = "blocked")
    {
        await using var context = CreateContext();

        var block = new AvailabilityBlock
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            StartDate = start,
            EndDate = end,
            Reason = reason
        };

        context.AvailabilityBlocks.Add(block);
        await context.SaveChangesAsync();
        return block;
    }

    /// <summary>Builds a BookingService whose repositories all share one context.</summary>
    protected BookingService CreateBookingService(AirbnbDbContext context, IPaymentService paymentService) =>
        new(
            new ListingRepository(context),
            new GenericRepository<Booking>(context),
            new UserRepository(context),
            new GenericRepository<Transaction>(context),
            paymentService);
}
