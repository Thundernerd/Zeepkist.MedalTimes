using TNRD.Zeepkist.MedalTimes.Medals;
using Xunit;

namespace TNRD.Zeepkist.MedalTimes.Tests;

public class MedalTargetsTests
{
    private const string Calculated = "2026-11-14T00:00:00Z";
    private static readonly MedalTargets Local = new(60, 50, 45, 40);
    private static readonly MedalTargets Frozen = new(60, 50, 45, 40, 38, 36, calculatedAt: Calculated);

    [Theory]
    [InlineData(0, "")]
    [InlineData(63, "Apex,Expert,Author,Gold,Silver,Bronze")]
    [InlineData(32, "Apex")]
    [InlineData(16, "Expert")]
    [InlineData(8, "Author")]
    [InlineData(4, "Gold")]
    [InlineData(2, "Silver")]
    [InlineData(1, "Bronze")]
    [InlineData(48, "Apex,Expert")]
    public void FrozenSelection(int mask, string expected) => Assert.Equal(expected, Select(Frozen, mask));

    [Theory]
    [InlineData(0, "")]
    [InlineData(63, "Author,Gold,Silver,Bronze")]
    [InlineData(32, "Author")]
    [InlineData(16, "Author")]
    [InlineData(48, "")]
    [InlineData(40, "Author")]
    [InlineData(33, "Bronze")]
    public void UnavailableSelection(int mask, string expected) => Assert.Equal(expected, Select(Local, mask));

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData(38d, null, null, Calculated)]
    [InlineData(null, 36d, null, Calculated)]
    [InlineData(38d, 36d, null, null)]
    [InlineData(38d, 36d, "2026-11-18T00:00:00Z", Calculated)]
    [InlineData(38d, 36d, "2026-11-01T00:00:00Z", Calculated)]
    [InlineData(38d, 38d, null, Calculated)]
    [InlineData(41d, 36d, null, Calculated)]
    [InlineData(38d, 36d, "invalid", Calculated)]
    [InlineData(38d, 36d, null, "invalid")]
    public void RequiresCompletedValidPair(double? expert, double? apex, string due, string calculated)
    {
        var targets = new MedalTargets(60, 50, 45, 40, expert, apex, due, calculated);
        Assert.Null(targets[MedalTier.Apex]);
        Assert.Null(targets[MedalTier.Expert]);
        Assert.Equal("Author", Select(targets, 32));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e12)]
    public void InvalidValuesUseLocalFallback(double value)
    {
        var targets = new MedalTargets(value, value, value, value, value, value, calculatedAt: Calculated).WithFallback(Local);
        Assert.Equal(40, targets[MedalTier.Author]);
        Assert.Null(targets[MedalTier.Apex]);
    }

    [Fact]
    public void ApiVanillaValuesOverrideLocalValues()
    {
        var targets = new MedalTargets(61, null, 46, 41).WithFallback(Local);
        Assert.Equal(61, targets[MedalTier.Bronze]);
        Assert.Equal(50, targets[MedalTier.Silver]);
        Assert.Equal(46, targets[MedalTier.Gold]);
        Assert.Equal(41, targets[MedalTier.Author]);
    }

    [Fact]
    public void CompetitiveTargetsCanUseLocalAuthorFallback()
    {
        var targets = new MedalTargets(null, null, null, null, 38, 36, calculatedAt: Calculated).WithFallback(Local);
        Assert.Equal(40, targets[MedalTier.Author]);
        Assert.Equal(36, targets[MedalTier.Apex]);
        Assert.Equal(DateTimeOffset.Parse(Calculated), targets.CalculatedAt);
    }

    [Fact]
    public void CommandsUseResolvedTimesAndDeadline()
    {
        var now = DateTimeOffset.Parse(Calculated);
        var settling = new MedalTargets(60, 50, 45, 40, dueAt: now.AddDays(4).AddHours(3).AddMinutes(28).ToString("O"));
        Assert.Equal("Apex time settles in 4d 3h 28m", settling.Message(MedalTier.Apex, now));
        Assert.Equal("Expert time settles in 4d 3h 28m", settling.Message(MedalTier.Expert, now));
        Assert.Equal("Apex time unavailable", settling.Message(MedalTier.Apex, now.AddDays(5)));
        Assert.Equal("Author time: 00:40.000", settling.Message(MedalTier.Author, now));
        Assert.Equal("Apex time: 00:36.000", Frozen.Message(MedalTier.Apex, now));
        Assert.Equal("Expert time: 00:38.000", Frozen.Message(MedalTier.Expert, now));
    }

    [Fact]
    public void All64ConfigurationsKeepSelectedFrozenRowsOrderedAndUnique()
    {
        for (int mask = 0; mask < 64; mask++)
        {
            var rows = Frozen.Select((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0,
                (mask & 8) != 0, (mask & 16) != 0, (mask & 32) != 0);
            Assert.Equal(rows.Count, rows.Distinct().Count());
            Assert.Equal(rows.OrderByDescending(tier => tier), rows);
            foreach (MedalTier tier in Enum.GetValues<MedalTier>())
                Assert.Equal((mask & (1 << (int)tier)) != 0, rows.Contains(tier));
        }
    }

    private static string Select(MedalTargets targets, int mask) => string.Join(",", targets.Select(
        (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0, (mask & 16) != 0, (mask & 32) != 0));
}
