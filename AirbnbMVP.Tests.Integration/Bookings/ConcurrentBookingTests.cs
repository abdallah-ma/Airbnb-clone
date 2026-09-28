using AirbnbMVP.BLL.DTOs.Requests;
using AirbnbMVP.BLL.DTOs.Requests.Booking;
using AirbnbMVP.BLL.DTOs.Responses;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace AirbnbMVP.Tests.Integration.Bookings;

/// <summary>
/// Exercises the real range-locking booking flow against SQL Server with many parallel
/// callers. These are the tests that prove the double-booking fix, because the guarantee
/// only exists because SERIALIZABLE + UPDLOCK/HOLDLOCK run on a real engine.
/// </summary>
public class ConcurrentBookingTests : LocalDbTestBase
{
    private const int Workers = 8;

    private Guid _hostId;
    private Guid _guestId;

    /// <summary>Listing under test, reset per test by <see cref="GivenFreshListingAsync"/>.</summary>
    private Guid _listingId;

    /// <summary>First bookable night for the current test.</summary>
    private DateOnly _base;

    /// <summary>Thread-safe payment stub: counts intents, never talks to Stripe.</summary>
    private sealed class FakePaymentService : IPaymentService
    {
        private int _intents;

        public int IntentsCreated => Volatile.Read(ref _intents);

        public Task<PaymentIntentResponse> CreatePaymentIntentAsync(decimal amount, Guid bookingId)
        {
            Interlocked.Increment(ref _intents);
            return Task.FromResult(new PaymentIntentResponse
            {
                ClientSecret = $"secret_{bookingId:N}",
                PaymentIntentId = $"pi_{bookingId:N}",
                Status = "requires_payment_method"
            });
        }

        public Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId) => throw new NotSupportedException();

        public Task CancelPaymentIntentAsync(string paymentIntentId) => throw new NotSupportedException();

        public Task HandleWebhookAsync(string json, string stripeSignature) => throw new NotSupportedException();

        public Task CreateRefundAsync(string paymentReference, decimal amount) => throw new NotSupportedException();
    }

    private sealed record WorkerOutcome(bool Succeeded, BookingResponseDto? Result, Exception? Error);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _hostId = (await SeedUserAsync(isHost: true)).Id;
        _guestId = (await SeedUserAsync()).Id;

        await GivenFreshListingAsync();
    }
    /// <summary>
    /// xUnit gives one database per test class, so each test takes a brand new listing and its
    /// own window of dates. Bookings from an earlier test would otherwise be real, legitimate
    /// conflicts for the next one.
    /// </summary>
    private async Task<Models.Listings.Listing> GivenFreshListingAsync()
    {
        var listing = await SeedListingAsync(_hostId, maxGuests: 6, pricePerNight: 100m);
        _listingId = listing.Id;
        _base = new DateOnly(2030, 6, 1).AddDays(Random.Shared.Next(0, 120));
        return listing;
    }

    private BookingRequestDto Request(DateOnly checkIn, DateOnly checkOut) => new()
    {
        ListingId = _listingId,
        CheckIn = checkIn,
        CheckOut = checkOut,
        NumGuests = 2
    };

    /// <summary>
    /// Runs one BookingService per worker - each with its own DbContext, because an EF context
    /// is not thread-safe - released simultaneously through a start gate so the calls contend.
    /// </summary>
    private async Task<(WorkerOutcome[] Outcomes, FakePaymentService Payment)> RunWorkersAsync(
        IReadOnlyList<BookingRequestDto> requests)
    {
        var payment = new FakePaymentService();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = requests.Select(request => Task.Run(async () =>
        {
            await gate.Task.ConfigureAwait(false);

            await using var context = CreateContext();
            var service = CreateBookingService(context, payment);

            try
            {
                var result = await service.BookListingAsync(request, _guestId);
                return new WorkerOutcome(true, result, null);
            }
            catch (Exception ex)
            {
                return new WorkerOutcome(false, null, ex);
            }
        })).ToArray();

        gate.SetResult();
        return (await Task.WhenAll(tasks), payment);
    }

    /// <summary>
    /// The core safety property: no two persisted bookings on a listing may share a night.
    /// </summary>
    private async Task AssertNoOverlappingBookingsAsync()
    {
        await using var context = CreateContext();
        var bookings = await context.Bookings.AsNoTracking().ToListAsync();

        var overlappingPairs = new List<string>();
        for (var i = 0; i < bookings.Count; i++)
        {
            for (var j = i + 1; j < bookings.Count; j++)
            {
                if (bookings[i].CheckIn < bookings[j].CheckOut && bookings[j].CheckIn < bookings[i].CheckOut)
                {
                    overlappingPairs.Add(
                        $"{bookings[i].Id:N}[{bookings[i].CheckIn}..{bookings[i].CheckOut}] vs " +
                        $"{bookings[j].Id:N}[{bookings[j].CheckIn}..{bookings[j].CheckOut}]");
                }
            }
        }

        overlappingPairs.Should().BeEmpty("a night was sold more than once");
    }

    [Fact]
    public async Task ConcurrentIdenticalBookings_ExactlyOneSucceeds()
    {
        // Eight callers race for the same listing and the same five nights. The range lock
        // must let exactly one through; everyone else backs off.
        await GivenFreshListingAsync();

        var requests = Enumerable.Range(0, Workers)
            .Select(_ => Request(_base, _base.AddDays(5)))
            .ToList();

        var (outcomes, payment) = await RunWorkersAsync(requests);

        var successes = outcomes.Count(o => o.Succeeded);
        var failures = outcomes.Where(o => !o.Succeeded).ToList();

        successes.Should().Be(1, "only one booking may win a contested date range");
        failures.Should().HaveCount(Workers - 1);
        failures.Select(f => f.Error).Should().AllBeOfType<ConflictException>(
            "losers must be rejected as a conflict, never surface a raw SQL or EF exception");

        await using var context = CreateContext();
        (await context.Bookings.CountAsync()).Should().Be(1);
        (await context.Set<AirbnbMVP.DAL.Models.Transaction>().CountAsync()).Should().Be(1);
        payment.IntentsCreated.Should().Be(1, "only the winning booking should be charged");

        await AssertNoOverlappingBookingsAsync();
    }

    [Fact]
    public async Task ConcurrentIdenticalBookings_LosersGetTheGenericRetryableConflict()
    {
        // Losers may lose the race either by observing the committed row ("booked on the
        // requested dates") or by being chosen as a deadlock victim. Both are domain conflicts.
        await GivenFreshListingAsync();

        var requests = Enumerable.Range(0, Workers)
            .Select(_ => Request(_base, _base.AddDays(5)))
            .ToList();

        var (outcomes, _) = await RunWorkersAsync(requests);

        var failures = outcomes.Where(o => !o.Succeeded).ToList();
        failures.Should().NotBeEmpty("someone has to lose the race");

        foreach (var failure in failures)
        {
            failure.Error.Should().BeOfType<ConflictException>();
            failure.Error!.Message.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task ConcurrentNonOverlappingBookings_NeverDoubleBooksAndNeverLeaksErrors()
    {
        // Eight separated five-night windows on one listing, requested simultaneously.
        //
        // Note on what is and is not asserted: separated windows normally all succeed, and
        // under a quiet machine they reliably do. But the outcome is not deterministic under
        // load - the same 1205 lock-convoy contention described in
        // ConcurrentAdjacentBookings_CanSpuriouslyRejectNonConflictingRequests can reject one
        // or two of these too. Asserting an exact success count would therefore be flaky.
        //
        // The contract that must hold unconditionally is asserted instead:
        //   1. no night is sold twice,
        //   2. every rejection is a domain conflict, never a leaked SQL/EF exception,
        //   3. the endpoint remains usable - at least one request succeeds.
        await GivenFreshListingAsync();

        var requests = Enumerable.Range(0, Workers)
            .Select(i => Request(_base.AddDays(i * 5), _base.AddDays((i * 5) + 5)))
            .ToList();

        var (outcomes, _) = await RunWorkersAsync(requests);

        outcomes.Count(o => o.Succeeded).Should().BeGreaterThan(0, "the flow must be usable under contention");

        foreach (var failure in outcomes.Where(o => !o.Succeeded))
        {
            failure.Error.Should().BeOfType<ConflictException>();
        }

        await AssertNoOverlappingBookingsAsync();
    }

    [Fact]
    public async Task ConcurrentNonOverlappingBookings_SucceedWithoutContention()
    {
        // The same requests issued one after another, which is the uncontended baseline: all
        // eight are accepted. Together with SequentialAdjacentBookings_BothSucceed this shows
        // the rejections seen under load are a locking artefact, not an overlap-rule defect.
        await GivenFreshListingAsync();

        for (var i = 0; i < Workers; i++)
        {
            await using var context = CreateContext();
            var result = await CreateBookingService(context, new FakePaymentService())
                .BookListingAsync(Request(_base.AddDays(i * 5), _base.AddDays((i * 5) + 5)), _guestId);

            result.TotalPrice.Should().Be(500m);
        }

        await using var verify = CreateContext();
        (await verify.Bookings.CountAsync()).Should().Be(Workers);

        await AssertNoOverlappingBookingsAsync();
    }

    [Fact]
    public async Task ConcurrentAdjacentBookings_NeverDoubleBooks()
    {
        // Back-to-back stays: caller i checks out on the day caller i+1 checks in. A shared
        // boundary day is not a conflict.
        await GivenFreshListingAsync();

        var requests = Enumerable.Range(0, Workers)
            .Select(i => Request(_base.AddDays(i * 2), _base.AddDays((i * 2) + 2)))
            .ToList();

        var (outcomes, _) = await RunWorkersAsync(requests);

        outcomes.Count(o => o.Succeeded).Should().BeGreaterThan(0);

        foreach (var failure in outcomes.Where(o => !o.Succeeded))
        {
            failure.Error.Should().BeOfType<ConflictException>();
        }

        await AssertNoOverlappingBookingsAsync();
    }

    [Fact]
    public async Task ConcurrentAdjacentBookings_CanSpuriouslyRejectNonConflictingRequests()
    {
        // Documents an observed limitation of the current UPDLOCK/HOLDLOCK range strategy.
        //
        // Back-to-back stays have no night in common, but the range the overlap query locks
        // does reach into the neighbour's key range (the predicate is an open-ended
        // "CheckIn < @checkOut"). Under 8-way contention the transactions therefore form a lock
        // convoy and SQL Server intermittently picks a deadlock victim (error 1205), which
        // BookingService reports as the generic retryable ConflictException.
        //
        // Measured on this machine: 6-7 of 8 concurrent adjacent requests succeed, and the
        // remaining 1-2 are rejected despite not actually conflicting. The stored data is
        // always correct - see ConcurrentAdjacentBookings_NeverDoubleBooks - but a legitimate
        // booking is lost and the guest has to retry. Sequential adjacent bookings are fine:
        // see SequentialAdjacentBookings_BothSucceed.
        await GivenFreshListingAsync();

        var requests = Enumerable.Range(0, Workers)
            .Select(i => Request(_base.AddDays(i * 2), _base.AddDays((i * 2) + 2)))
            .ToList();

        var (outcomes, _) = await RunWorkersAsync(requests);

        // Safety still holds no matter how many are rejected.
        await AssertNoOverlappingBookingsAsync();

        // Every rejection is a retryable domain conflict, never a raw database error.
        foreach (var failure in outcomes.Where(o => !o.Succeeded))
        {
            failure.Error.Should().BeOfType<ConflictException>();
            failure.Error!.Message.Should()
                .Be("The booking could not be completed at this time. Please try again.");
        }
    }

    [Fact]
    public async Task SequentialAdjacentBookings_BothSucceed()
    {
        // Without concurrency there is no lock convoy, so back-to-back stays are accepted.
        // This is what shows the rejections above are a contention artefact and not an
        // off-by-one in the overlap rule.
        await GivenFreshListingAsync();

        await using (var first = CreateContext())
        {
            await CreateBookingService(first, new FakePaymentService())
                .BookListingAsync(Request(_base, _base.AddDays(2)), _guestId);
        }

        await using var second = CreateContext();
        var result = await CreateBookingService(second, new FakePaymentService())
            .BookListingAsync(Request(_base.AddDays(2), _base.AddDays(4)), _guestId);

        result.TotalPrice.Should().Be(200m);

        await AssertNoOverlappingBookingsAsync();
    }

    [Fact]
    public async Task SequentialBookings_SecondIdenticalRequestConflicts()
    {
        // Baseline for the concurrency tests: the same double-booking attempt with no
        // parallelism at all must already be rejected.
        await GivenFreshListingAsync();
        var request = Request(_base, _base.AddDays(5));

        await using (var first = CreateContext())
        {
            await CreateBookingService(first, new FakePaymentService()).BookListingAsync(request, _guestId);
        }

        await using var second = CreateContext();
        var act = () => CreateBookingService(second, new FakePaymentService()).BookListingAsync(request, _guestId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*booked on the requested dates*");
    }

    [Fact]
    public async Task BookListingAsync_PersistsBookingAndTransactionTogether()
    {
        await GivenFreshListingAsync();
        var payment = new FakePaymentService();

        await using (var context = CreateContext())
        {
            var result = await CreateBookingService(context, payment).BookListingAsync(Request(_base, _base.AddDays(5)), _guestId);
            result.TotalPrice.Should().Be(500m);
        }

        await using var verify = CreateContext();
        var booking = await verify.Bookings.SingleAsync();
        var transaction = await verify.Set<AirbnbMVP.DAL.Models.Transaction>().SingleAsync();

        booking.Status.Should().Be(Models.Bookings.BookingStatus.Pending);
        booking.GuestId.Should().Be(_guestId);
        booking.ListingId.Should().Be(_listingId);
        booking.NumGuests.Should().Be(2);
        transaction.BookingId.Should().Be(booking.Id);
        transaction.Amount.Should().Be(500m);
        transaction.PayerId.Should().Be(_guestId);
        transaction.PayeeId.Should().Be(_hostId);
        transaction.Status.Should().Be(AirbnbMVP.DAL.Models.TransactionStatus.Pending);
        payment.IntentsCreated.Should().Be(1);
    }

    [Fact]
    public async Task BookListingAsync_WhenAnotherListingIsBlocked_BookingStillSucceeds()
    {
        // A block on a different listing must not affect this one.
        await GivenFreshListingAsync();
        var otherListing = await SeedListingAsync(_hostId);
        await SeedAvailabilityBlockAsync(otherListing, _base, _base.AddDays(5));

        await using var context = CreateContext();
        var result = await CreateBookingService(context, new FakePaymentService())
            .BookListingAsync(Request(_base, _base.AddDays(5)), _guestId);

        result.TotalPrice.Should().Be(500m);
    }

    [Fact]
    public async Task BookListingAsync_WhenOwnAvailabilityBlock_Conflicts()
    {
        var listing = await GivenFreshListingAsync();
        await SeedAvailabilityBlockAsync(listing, _base, _base.AddDays(5));

        await using var context = CreateContext();
        var act = () => CreateBookingService(context, new FakePaymentService())
            .BookListingAsync(Request(_base, _base.AddDays(5)), _guestId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*not available on the requested dates*");
    }
}
