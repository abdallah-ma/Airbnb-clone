using AirbnbMVP.BLL.Specifications.Bookings;
using AirbnbMVP.BLL.Specifications.Listings;
using AirbnbMVP.BLL.Specifications.Transactions;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Listings;

namespace AirbnbMVP.Tests.Unit.Specifications;

/// <summary>
/// Compiles a specification's criteria expression and applies it to a sample instance, so the
/// filter logic is verified without a database.
/// </summary>
internal static class CriteriaEvaluator
{
    public static bool Matches<T>(System.Linq.Expressions.Expression<Func<T, bool>> criteria, T entity) =>
        criteria.Compile()(entity);
}

public class OverlappingBookingsSpecTests
{
    private static readonly Guid ListingId = Guid.NewGuid();
    private static readonly DateOnly CheckIn = new(2030, 5, 10);
    private static readonly DateOnly CheckOut = new(2030, 5, 15);

    private static Booking Booking(
        Guid? listingId = null,
        BookingStatus status = BookingStatus.Confirmed,
        DateOnly? checkIn = null,
        DateOnly? checkOut = null,
        Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            ListingId = listingId ?? ListingId,
            Status = status,
            CheckIn = checkIn ?? CheckIn,
            CheckOut = checkOut ?? CheckOut
        };

    [Theory]
    // request is 2030-05-10 -> 2030-05-15
    [InlineData(2030, 5, 10, 2030, 5, 12, true)]  // starts before, ends inside
    [InlineData(2030, 5, 13, 2030, 5, 20, true)]  // entirely inside
    [InlineData(2030, 5, 1, 2030, 5, 20, true)]   // fully contains the request
    [InlineData(2030, 5, 12, 2030, 5, 15, true)]  // overlaps the check-out night
    [InlineData(2030, 5, 8, 2030, 5, 10, false)]  // ends exactly on check-in (adjacent)
    [InlineData(2030, 5, 15, 2030, 5, 20, false)] // starts exactly on check-out (adjacent)
    [InlineData(2030, 5, 16, 2030, 5, 20, false)] // entirely after
    [InlineData(2030, 5, 1, 2030, 5, 9, false)]   // entirely before
    public void OverlappingBookingsSpec_AppliesHalfOpenOverlap(
        int y1, int m1, int d1, int y2, int m2, int d2, bool expectedOverlap)
    {
        var spec = new OverlappingBookingsSpec(ListingId, CheckIn, CheckOut);
        var existing = Booking(checkIn: new DateOnly(y1, m1, d1), checkOut: new DateOnly(y2, m2, d2));

        CriteriaEvaluator.Matches(spec.Criteria!, existing).Should().Be(expectedOverlap);
    }

    [Fact]
    public void OverlappingBookingsSpec_IgnoresAdjacentStaysTouchingEitherBoundary()
    {
        var spec = new OverlappingBookingsSpec(ListingId, CheckIn, CheckOut);

        // Ends the day the new booking starts.
        CriteriaEvaluator.Matches(spec.Criteria!, Booking(
            checkIn: new DateOnly(2030, 5, 6), checkOut: CheckIn)).Should().BeFalse();

        // Starts the day the new booking ends.
        CriteriaEvaluator.Matches(spec.Criteria!, Booking(
            checkIn: CheckOut, checkOut: new DateOnly(2030, 5, 22))).Should().BeFalse();
    }

    [Fact]
    public void OverlappingBookingsSpec_IgnoresCancelledBookings()
    {
        var spec = new OverlappingBookingsSpec(ListingId, CheckIn, CheckOut);
        var cancelled = Booking(status: BookingStatus.Cancelled);

        CriteriaEvaluator.Matches(spec.Criteria!, cancelled).Should().BeFalse();
    }

    [Fact]
    public void OverlappingBookingsSpec_MatchesConfirmedAndPendingAndCompletedBookings()
    {
        var spec = new OverlappingBookingsSpec(ListingId, CheckIn, CheckOut);

        foreach (var status in new[] { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.Completed })
        {
            CriteriaEvaluator.Matches(spec.Criteria!, Booking(status: status))
                .Should().BeTrue($"a {status} booking still occupies the listing");
        }
    }

    [Fact]
    public void OverlappingBookingsSpec_IgnoresOtherListings()
    {
        var spec = new OverlappingBookingsSpec(ListingId, CheckIn, CheckOut);
        var otherListing = Booking(listingId: Guid.NewGuid());

        CriteriaEvaluator.Matches(spec.Criteria!, otherListing).Should().BeFalse();
    }

    [Fact]
    public void OverlappingBookingsSpec_WhenExclusionIdSupplied_IgnoresThatBooking()
    {
        var self = Booking();
        var spec = new OverlappingBookingsSpec(ListingId, CheckIn, CheckOut, self.Id);

        CriteriaEvaluator.Matches(spec.Criteria!, self).Should().BeFalse();
    }

    [Fact]
    public void OverlappingBookingsSpec_WhenExclusionIdSupplied_StillMatchesOtherBookings()
    {
        var self = Booking();
        var other = Booking(id: Guid.NewGuid());
        var spec = new OverlappingBookingsSpec(ListingId, CheckIn, CheckOut, self.Id);

        CriteriaEvaluator.Matches(spec.Criteria!, self).Should().BeFalse();
        CriteriaEvaluator.Matches(spec.Criteria!, other).Should().BeTrue();
    }
}

public class TransactionSpecificationTests
{
    private static Transaction Transaction(
        Guid? bookingId = null,
        TransactionType type = TransactionType.Payment,
        TransactionStatus status = TransactionStatus.Completed) => new()
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId ?? Guid.NewGuid(),
            Type = type,
            Status = status
        };

    [Fact]
    public void TransactionSpecification_FiltersByBookingTypeAndStatus()
    {
        var bookingId = Guid.NewGuid();
        var spec = new TransactionSpecification(bookingId, TransactionType.Payment, TransactionStatus.Completed);

        CriteriaEvaluator.Matches(spec.Criteria!, Transaction(bookingId, TransactionType.Payment, TransactionStatus.Completed))
            .Should().BeTrue();

        CriteriaEvaluator.Matches(spec.Criteria!, Transaction(bookingId, TransactionType.Refund, TransactionStatus.Completed))
            .Should().BeFalse();

        CriteriaEvaluator.Matches(spec.Criteria!, Transaction(bookingId, TransactionType.Payment, TransactionStatus.Failed))
            .Should().BeFalse();

        CriteriaEvaluator.Matches(spec.Criteria!, Transaction(Guid.NewGuid(), TransactionType.Payment, TransactionStatus.Completed))
            .Should().BeFalse();
    }

    [Fact]
    public void TransactionSpecification_NullArgumentsActAsWildcards()
    {
        var spec = new TransactionSpecification(null, null, null);

        CriteriaEvaluator.Matches(spec.Criteria!, Transaction()).Should().BeTrue();
        CriteriaEvaluator.Matches(spec.Criteria!, Transaction(type: TransactionType.Payout, status: TransactionStatus.Failed))
            .Should().BeTrue();
    }
}

public class ListingFilterSpecificationTests
{
    private static Listing Listing(string city = "Cairo", string country = "Egypt", decimal price = 100m, int maxGuests = 4) => new()
    {
        Id = Guid.NewGuid(),
        City = city,
        Country = country,
        PricePerNight = price,
        MaxGuests = maxGuests
    };

    [Fact]
    public void ListingFilterSpecification_DefaultPageSizeIsTenAndIsWrittenBackToParams()
    {
        var parameters = new ListingFilterParams { Page = 1, PageSize = 0 };

        var spec = new ListingFilterSpecification(parameters);

        parameters.PageSize.Should().Be(10, "the spec mutates the caller's params object");
        spec.Take.Should().Be(10);
        spec.IsPagingEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(2, 10, 10)]
    [InlineData(3, 10, 20)]
    [InlineData(0, 20, 0)]   // page 0 is clamped to the first page
    [InlineData(-5, 10, 0)]  // negative pages are clamped too
    public void ListingFilterSpecification_ComputesSkipFromPageAndPageSize(int page, int pageSize, int expectedSkip)
    {
        var spec = new ListingFilterSpecification(new ListingFilterParams { Page = page, PageSize = pageSize });

        spec.Skip.Should().Be(expectedSkip);
        spec.Take.Should().Be(pageSize);
    }

    [Fact]
    public void ListingFilterSpecification_IncludesPhotosAndAmenities()
    {
        var spec = new ListingFilterSpecification(new ListingFilterParams());

        spec.Includes.Should().HaveCount(2);
        spec.OrderBy.Should().NotBeNull();
    }

    [Fact]
    public void ListingFilterSpecification_EmptyFiltersMatchEverything()
    {
        var spec = new ListingFilterSpecification(new ListingFilterParams());

        CriteriaEvaluator.Matches(spec.Criteria!, Listing()).Should().BeTrue();
    }

    [Fact]
    public void ListingFilterSpecification_FiltersByCityAndCountry()
    {
        var spec = new ListingFilterSpecification(new ListingFilterParams
        {
            City = "Cairo",
            Country = "Egypt"
        });

        CriteriaEvaluator.Matches(spec.Criteria!, Listing(city: "Cairo", country: "Egypt")).Should().BeTrue();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing(city: "Giza", country: "Egypt")).Should().BeFalse();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing(city: "Cairo", country: "Jordan")).Should().BeFalse();
    }

    [Fact]
    public void ListingFilterSpecification_MaxPriceIsInclusive()
    {
        var spec = new ListingFilterSpecification(new ListingFilterParams { MaxPrice = 100 });

        CriteriaEvaluator.Matches(spec.Criteria!, Listing(price: 100m)).Should().BeTrue();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing(price: 101m)).Should().BeFalse();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing(price: 50m)).Should().BeTrue();
    }

    [Fact]
    public void ListingFilterSpecification_MinGuestsIsInclusive()
    {
        var spec = new ListingFilterSpecification(new ListingFilterParams { MinGuests = 4 });

        CriteriaEvaluator.Matches(spec.Criteria!, Listing(maxGuests: 4)).Should().BeTrue();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing(maxGuests: 5)).Should().BeTrue();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing(maxGuests: 3)).Should().BeFalse();
    }

    [Fact]
    public void ListingFilterSpecification_CombinesAllFiltersWithAnd()
    {
        var spec = new ListingFilterSpecification(new ListingFilterParams
        {
            City = "Cairo",
            Country = "Egypt",
            MaxPrice = 200,
            MinGuests = 2
        });

        CriteriaEvaluator.Matches(spec.Criteria!, Listing("Cairo", "Egypt", 150m, 4)).Should().BeTrue();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing("Cairo", "Egypt", 250m, 4)).Should().BeFalse();
        CriteriaEvaluator.Matches(spec.Criteria!, Listing("Cairo", "Egypt", 150m, 1)).Should().BeFalse();
    }
}
