using System.Net;
using System.Text;

namespace BlazorTerm.Tests;

public sealed class LiveDataReaderTests
{
    [Fact]
    public async Task Kubernetes_SanitizesSnapshotsAndCachesByResource()
    {
        const string json = """
            {"items":[{"metadata":{"name":"payments-prod-7f9c","uid":"raw-id","creationTimestamp":"2026-07-27T12:00:00Z","labels":{"host":"secret.internal"}},"status":{"phase":"Running","podIP":"10.0.0.2","containerStatuses":[{"name":"api","ready":true,"restartCount":3}]}}]}
            """;
        var handler = new FakeHandler(_ => Json(json));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://kubernetes.test") };
        using var reader = new KubernetesReader(
            new KubernetesOptions { Enabled = true, CacheSeconds = 45 },
            client,
            _ => ValueTask.FromResult("service-account-token"));

        var first = await reader.GetAsync("pods");
        var second = await reader.GetAsync("pods");
        var output = string.Join('\n', first.Lines.Select(line => line.ToPlainText()));

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("pod-01", output);
        Assert.Contains("Running", output);
        Assert.Contains("ready 1/1  restarts 3", output);
        Assert.DoesNotContain("payments", output);
        Assert.DoesNotContain("raw-id", output);
        Assert.DoesNotContain("10.0.0.2", output);
        Assert.DoesNotContain("secret.internal", output);
        Assert.DoesNotContain("service-account-token", output);
    }

    [Theory]
    [InlineData("secrets")]
    [InlineData("deployments")]
    public async Task Kubernetes_RejectsResourcesWithoutCallingTheApi(string resource)
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException());
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://kubernetes.test") };
        using var reader = new KubernetesReader(new KubernetesOptions { Enabled = true }, client, _ => ValueTask.FromResult("token"));

        var result = await reader.GetAsync(resource);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("only 'get pods'", result.Lines[0].ToPlainText());
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Kubernetes_FailsClosedWithoutLeakingServerErrors()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("token=secret; host=node.internal")
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://kubernetes.test") };
        using var reader = new KubernetesReader(new KubernetesOptions { Enabled = true }, client, _ => ValueTask.FromResult("token"));

        var result = await reader.GetAsync("nodes");
        var output = string.Join('\n', result.Lines.Select(line => line.ToPlainText()));

        Assert.Contains("temporarily unavailable", output);
        Assert.DoesNotContain("secret", output);
        Assert.DoesNotContain("node.internal", output);
    }

    [Fact]
    public async Task Strava_RefreshesFiltersAndCachesRides()
    {
        const string activities = """
            [{"name":"Private route name","type":"Ride","start_date":"2026-07-27T08:00:00Z","distance":42500,"moving_time":5400},{"type":"Run","distance":10000,"moving_time":3000},{"type":"VirtualRide","start_date":"2026-07-26T08:00:00Z","distance":20000,"moving_time":3600}]
            """;
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath == "/oauth/token"
            ? Json("{\"access_token\":\"short-lived-secret\"}")
            : Json(activities));
        using var reader = new StravaReader(new StravaOptions
        {
            Enabled = true,
            ClientId = "client-id-secret",
            ClientSecret = "client-secret",
            RefreshToken = "refresh-secret",
            OAuthEndpoint = "https://strava.test/oauth/token",
            ActivitiesEndpoint = "https://strava.test/activities",
            CacheMinutes = 10
        }, new HttpClient(handler));

        var first = await reader.GetRidesAsync();
        var second = await reader.GetRidesAsync();
        var output = string.Join('\n', first.Lines.Select(line => line.ToPlainText()));

        Assert.Equal(first, second);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(2, first.Lines.OfType<ChartLine>().Count());
        Assert.Contains("42.5 km", output);
        Assert.Contains("virtual", output);
        Assert.DoesNotContain("Private route name", output);
        Assert.DoesNotContain("secret", output);
    }

    [Fact]
    public async Task Strava_FailsClosedWithoutCredentialsOrRemoteErrors()
    {
        using var disabled = new StravaReader(new StravaOptions(), new HttpClient(new FakeHandler(_ => throw new InvalidOperationException())));

        var result = await disabled.GetRidesAsync();

        Assert.Contains("disabled", result.Lines[0].ToPlainText());
    }

    [Fact]
    public async Task Strava_RejectsInsecureEndpointsWithoutSendingCredentials()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException());
        using var reader = new StravaReader(new StravaOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RefreshToken = "refresh-token",
            OAuthEndpoint = "http://strava.test/oauth/token"
        }, new HttpClient(handler));

        var result = await reader.GetRidesAsync();

        Assert.Contains("unavailable", result.Lines[0].ToPlainText());
        Assert.Equal(0, handler.RequestCount);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }
}
