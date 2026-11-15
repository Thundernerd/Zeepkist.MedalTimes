using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using TNRD.Zeepkist.MedalTimes.GraphQL;
using TNRD.Zeepkist.MedalTimes.Medals;
using Xunit;

namespace TNRD.Zeepkist.MedalTimes.Tests;

public class HttpClientTests
{
    private const string Item = """
        {"__typename":"LevelItem","validationTimeBronze":60,"validationTimeSilver":50,
        "validationTimeGold":45,"validationTimeAuthor":40,"validationTimeExpert":38,
        "validationTimeApex":36,"medalDueAt":null,"medalCalculatedAt":"2026-11-14T00:00:00Z"}
        """;
    private static readonly LevelGraphqlIdentity Identity = LevelGraphqlIdentity.FromValues("XXHASH", "LEGACY", false, false);

    [Fact]
    public async Task ExecutesGeneratedQueryUsingHttpOnly()
    {
        using var handler = new ResponseHandler { Json = Response(Item) };
        using var services = Services(handler);
        var source = new MedalTargetSource(services.GetRequiredService<IMedalTimesClient>());
        var targets = await source.FetchAsync(Identity, default);
        Assert.Equal(36, targets[MedalTier.Apex]);
        Assert.Equal(38, targets[MedalTier.Expert]);
        Assert.Equal(60, targets[MedalTier.Bronze]);
        Assert.Equal(1, handler.Requests);
        Assert.Contains("XXHASH", handler.Body);
        Assert.Contains(LevelGraphqlIdentity.NoLegacyHashSentinel, handler.Body);
        Assert.DoesNotContain("LEGACY\"", handler.Body);
        Assert.Contains("medalDueAt", handler.Body);
        Assert.Contains("medalCalculatedAt", handler.Body);
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly =>
            assembly.GetName().Name == "StrawberryShake.Transport.WebSockets");
        // NetworkOnly must issue another HTTP request rather than returning stored data.
        await source.FetchAsync(Identity, default);
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task GraphqlErrorsRejectPartialTargets()
    {
        using var handler = new ResponseHandler { Json = Response(Item).TrimEnd('}') + ",\"errors\":[{\"message\":\"Denied\"}]}" };
        using var services = Services(handler);
        var source = new MedalTargetSource(services.GetRequiredService<IMedalTimesClient>());
        await Assert.ThrowsAsync<GraphQLClientException>(() => source.FetchAsync(Identity, default));
    }

    [Theory]
    [InlineData("{\"data\":{\"levels\":{\"__typename\":\"LevelsConnection\",\"nodes\":[]}}}")]
    [InlineData("{\"data\":{\"levels\":{\"__typename\":\"LevelsConnection\",\"nodes\":[{\"__typename\":\"Level\",\"levelItems\":{\"__typename\":\"LevelItemsConnection\",\"nodes\":[]}}]}}}")]
    public async Task MissingLevelOrItemReturnsNoTargets(string json)
    {
        using var handler = new ResponseHandler { Json = json };
        using var services = Services(handler);
        Assert.Null(await new MedalTargetSource(services.GetRequiredService<IMedalTimesClient>()).FetchAsync(Identity, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpFailureAndTimeoutPreserveLocalTargets(bool timeout)
    {
        using var handler = new ResponseHandler { Json = "{}", Status = HttpStatusCode.ServiceUnavailable, Delay = timeout };
        using var services = Services(handler, TimeSpan.FromMilliseconds(50));
        using var session = new LevelMedalSession();
        var local = new MedalTargets(60, 50, 45, 40);
        session.Begin(new object(), Identity.CacheKey, local);
        var source = new MedalTargetSource(services.GetRequiredService<IMedalTimesClient>());
        // StrawberryShake can report transport failures as GraphQLClientException.
        Assert.NotNull(await Record.ExceptionAsync(() => source.FetchAsync(Identity, session.Token)));
        Assert.Same(local, session.Targets);
        if (timeout) Assert.True(handler.Cancelled);
    }

    [Fact]
    public async Task MissingIdentityMakesNoRequest()
    {
        using var handler = new ResponseHandler();
        using var services = Services(handler);
        Assert.Null(await new MedalTargetSource(services.GetRequiredService<IMedalTimesClient>())
            .FetchAsync(LevelGraphqlIdentity.Unavailable, default));
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task LevelExitCancelsHttpRequest()
    {
        using var handler = new ResponseHandler { Delay = true };
        using var services = Services(handler);
        using var session = new LevelMedalSession();
        session.Begin(new object(), Identity.CacheKey, new MedalTargets(60, 50, 45, 40));
        var pending = new MedalTargetSource(services.GetRequiredService<IMedalTimesClient>())
            .FetchAsync(Identity, session.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        session.Dispose();
        Assert.NotNull(await Record.ExceptionAsync(() => pending));
        Assert.True(handler.Cancelled);
        Assert.Null(session.Targets);
    }

    private static ServiceProvider Services(ResponseHandler handler, TimeSpan? timeout = null)
    {
        var collection = new ServiceCollection();
        collection.AddMedalTimesClient(ExecutionStrategy.NetworkOnly).ConfigureHttpClient(client =>
        {
            client.BaseAddress = new Uri("https://graphql.example/");
            client.Timeout = timeout ?? TimeSpan.FromSeconds(15);
        }, builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
        return collection.BuildServiceProvider();
    }

    private static string Response(string item) =>
        "{\"data\":{\"levels\":{\"__typename\":\"LevelsConnection\",\"nodes\":[{\"__typename\":\"Level\",\"levelItems\":{\"__typename\":\"LevelItemsConnection\",\"nodes\":[" + item + "]}}]}}}";

    private sealed class ResponseHandler : HttpMessageHandler
    {
        public string Json = "{}";
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool Delay;
        public bool Cancelled;
        public int Requests;
        public string Body;
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Body = await request.Content.ReadAsStringAsync(token);
            Started.TrySetResult();
            if (Delay)
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) { Cancelled = true; throw; }
            }
            return new HttpResponseMessage(Status) { Content = new StringContent(Json, Encoding.UTF8, "application/json") };
        }
    }
}
