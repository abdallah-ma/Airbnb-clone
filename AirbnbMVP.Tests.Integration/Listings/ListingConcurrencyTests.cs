using AirbnbMVP.Data;
using AirbnbMVP.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AirbnbMVP.Tests.Integration.Listings;

/// <summary>
/// Covers the Listing.RowVersion optimistic-concurrency column against a real SQL Server.
/// The service-level translation into ConflictException is covered by ListingServiceTests;
/// what is only observable here is that the rowversion is actually enforced by the engine.
/// </summary>
public class ListingConcurrencyTests : SqlServerTestBase
{
    private Guid _hostId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _hostId = (await SeedUserAsync(isHost: true)).Id;
    }

    [Fact]
    public async Task RowVersion_IsPopulatedOnInsert()
    {
        var listing = await SeedListingAsync(_hostId);

        listing.RowVersion.Should().NotBeNull();
        listing.RowVersion!.Should().HaveCount(8, "a SQL Server timestamp column is always 8 bytes");
    }

    [Fact]
    public async Task RowVersion_ChangesWhenTheRowIsUpdated()
    {
        var listing = await SeedListingAsync(_hostId);
        var original = listing.RowVersion.ToArray();

        await using var context = CreateContext();
        var tracked = await context.Listings.SingleAsync(l => l.Id == listing.Id);
        tracked.Title = "Renamed once";
        await context.SaveChangesAsync();

        tracked.RowVersion.Should().NotEqual(original, "SQL Server must stamp a new rowversion");
    }

    [Fact]
    public async Task RowVersion_ChangesOnEverySuccessiveUpdate()
    {
        var listing = await SeedListingAsync(_hostId);
        var seen = new List<string> { Convert.ToHexString(listing.RowVersion) };

        for (var i = 0; i < 3; i++)
        {
            await using var context = CreateContext();
            var tracked = await context.Listings.SingleAsync(l => l.Id == listing.Id);
            tracked.Title = $"Revision {i}";
            await context.SaveChangesAsync();
            seen.Add(Convert.ToHexString(tracked.RowVersion));
        }

        seen.Should().OnlyHaveUniqueItems("each update stamps a fresh rowversion");
    }

    [Fact]
    public async Task StaleUpdate_ThrowsDbUpdateConcurrencyException()
    {
        // Two contexts load the same row and therefore hold the same rowversion. The first
        // write bumps it; the second write still expects the old value and is rejected.
        var listing = await SeedListingAsync(_hostId);

        await using var first = CreateContext();
        await using var second = CreateContext();

        var winner = await first.Listings.SingleAsync(l => l.Id == listing.Id);
        var loser = await second.Listings.SingleAsync(l => l.Id == listing.Id);

        winner.Title = "Winner";
        await first.SaveChangesAsync();

        loser.Title = "Loser";
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task StaleUpdate_LeavesTheWinningValueInPlace()
    {
        var listing = await SeedListingAsync(_hostId);

        await using (var first = CreateContext())
        await using (var second = CreateContext())
        {
            var winner = await first.Listings.SingleAsync(l => l.Id == listing.Id);
            var loser = await second.Listings.SingleAsync(l => l.Id == listing.Id);

            winner.Title = "Winner";
            await first.SaveChangesAsync();

            loser.Title = "Loser";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }

        await using var verify = CreateContext();
        (await verify.Listings.SingleAsync(l => l.Id == listing.Id)).Title.Should().Be("Winner");
    }

    [Fact]
    public async Task ConcurrentListingUpdates_NoLostOrTornWrites()
    {
        // Four writers hammer the same row at once. With optimistic concurrency the outcome is
        // not "exactly one winner": a writer whose SELECT happens after a rival has already
        // committed reads the new rowversion and legitimately succeeds too. What must hold is
        // that every rejection is a concurrency conflict and that the stored value is always
        // one writer's value in full - never a blend of two.
        var listing = await SeedListingAsync(_hostId);
        const int writers = 4;

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, writers).Select(i => Task.Run(async () =>
        {
            await gate.Task.ConfigureAwait(false);

            await using var context = CreateContext();
            var tracked = await context.Listings.SingleAsync(l => l.Id == listing.Id);
            tracked.Title = $"Writer {i}";

            try
            {
                await context.SaveChangesAsync();
                return (Succeeded: true, Error: (Exception?)null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return (Succeeded: false, Error: (Exception?)ex);
            }
        })).ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        results.Count(r => r.Succeeded).Should().BeGreaterThan(0);
        results.Where(r => !r.Succeeded).Should().OnlyContain(r => r.Error is DbUpdateConcurrencyException);

        await using var verify = CreateContext();
        var finalTitle = (await verify.Listings.SingleAsync(l => l.Id == listing.Id)).Title;

        finalTitle.Should().MatchRegex("^Writer [0-3]$", "the row must hold exactly one writer's value in full");
    }
}
