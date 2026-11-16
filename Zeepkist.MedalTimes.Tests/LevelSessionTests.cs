using TNRD.Zeepkist.MedalTimes.GraphQL;
using TNRD.Zeepkist.MedalTimes.Medals;
using Xunit;

namespace TNRD.Zeepkist.MedalTimes.Tests;

public class LevelSessionTests
{
    private static readonly MedalTargets Local = new(60, 50, 45, 40);
    private static readonly MedalTargets Remote = new(61, 51, 46, 41);

    [Fact]
    public void CoalescesDuplicateLoadAndResetsOnNewEntry()
    {
        using var session = new LevelMedalSession();
        var level = new object();
        Assert.True(session.Begin(level, "one", Local));
        int first = session.Generation;
        var token = session.Token;
        Assert.False(session.Begin(level, "one", Local));
        Assert.Equal(first, session.Generation);
        Assert.True(session.Apply(first, "one", Remote));
        Assert.True(session.Begin(new object(), "two", Local));
        Assert.True(token.IsCancellationRequested);
        Assert.Same(Local, session.Targets);
        Assert.False(session.Apply(first, "one", Remote));
        Assert.False(session.Apply(session.Generation, "one", Remote));
    }

    [Fact]
    public async Task LateResponseCannotReplaceReenteredLevelOrSurviveExit()
    {
        using var session = new LevelMedalSession();
        var level = new object();
        var pending = new TaskCompletionSource<MedalTargets>();
        session.Begin(level, "same", Local);
        int first = session.Generation;
        var token = session.Token;
        session.Dispose();
        Assert.True(token.IsCancellationRequested);
        Assert.Null(session.Targets);
        session.Begin(level, "same", Local);
        pending.SetResult(Remote);
        Assert.False(session.Apply(first, "same", await pending.Task));
        int second = session.Generation;
        session.Dispose();
        Assert.False(session.Apply(second, "same", Remote));
    }

    [Fact]
    public void MissingResultPreservesLocalTargets()
    {
        using var session = new LevelMedalSession();
        session.Begin(new object(), null, Local);
        Assert.False(session.Apply(session.Generation, null, null));
        Assert.Same(Local, session.Targets);
    }

    [Fact]
    public void ResponseDoesNotChangeVisibility()
    {
        var visibility = new MedalVisibility { Active = true, UserVisible = false };
        Assert.False(visibility.IsVisible(true));
        visibility.ChatOpen = true;
        visibility.UserVisible = true;
        Assert.False(visibility.IsVisible(true));
        visibility.ChatOpen = false;
        visibility.TestLevel = true;
        Assert.False(visibility.IsVisible(true));
        visibility.TestLevel = false;
        Assert.True(visibility.IsVisible(true));
        Assert.False(visibility.IsVisible(false));
        visibility.Active = false;
        Assert.False(visibility.IsVisible(true));
    }

    [Fact]
    public void CommunityLevelsNeverUseLegacyIdentity()
    {
        var identity = LevelGraphqlIdentity.FromValues("XXHASH", "LEGACY", false, false);
        Assert.Equal("XXHASH", identity.XxHash);
        Assert.Equal(LevelGraphqlIdentity.NoLegacyHashSentinel, identity.Hash);
        Assert.False(LevelGraphqlIdentity.FromValues(null, "LEGACY", false, false).IsAvailable);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AdventureLevelsKeepLegacyFallback(bool adventure, bool avonturen)
    {
        var identity = LevelGraphqlIdentity.FromValues(null, "UID", adventure, avonturen);
        Assert.True(identity.IsAvailable);
        Assert.Equal("UID", identity.Hash);
        Assert.Equal(LevelGraphqlIdentity.NoXxHashSentinel, identity.XxHash);
        identity = LevelGraphqlIdentity.FromValues("XXHASH", "UID", adventure, avonturen);
        Assert.Equal("XXHASH", identity.XxHash);
        Assert.Equal("UID", identity.Hash);
    }
}
