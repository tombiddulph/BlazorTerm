using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BlazorTerm;

public interface IStravaReader
{
    ValueTask<CommandResult> GetRidesAsync(CancellationToken cancellationToken = default);
}

public sealed class StravaReader(StravaOptions options, HttpClient httpClient) : IStravaReader, IDisposable
{
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private CommandResult? _cached;
    private DateTimeOffset _expiresAt;

    public async ValueTask<CommandResult> GetRidesAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return Unavailable("rides: Strava integration is disabled");
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret) || string.IsNullOrWhiteSpace(options.RefreshToken))
            return Unavailable("rides: Strava integration is unavailable");
        if (!IsSecureEndpoint(options.OAuthEndpoint) || !IsSecureEndpoint(options.ActivitiesEndpoint))
            return Unavailable("rides: Strava integration is unavailable");
        if (_cached is not null && _expiresAt > DateTimeOffset.UtcNow)
            return _cached;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null && _expiresAt > DateTimeOffset.UtcNow)
                return _cached;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30)));
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, options.OAuthEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["refresh_token"] = options.RefreshToken,
                    ["grant_type"] = "refresh_token"
                })
            };
            using var tokenResponse = await httpClient.SendAsync(tokenRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (tokenResponse.StatusCode != HttpStatusCode.OK)
                return Unavailable("rides: recent activity is temporarily unavailable");
            await using var tokenStream = await tokenResponse.Content.ReadAsStreamAsync(timeout.Token);
            using var tokenDocument = await JsonDocument.ParseAsync(tokenStream, cancellationToken: timeout.Token);
            if (!tokenDocument.RootElement.TryGetProperty("access_token", out var tokenValue) || tokenValue.ValueKind != JsonValueKind.String)
                return Unavailable("rides: recent activity is temporarily unavailable");
            var accessToken = tokenValue.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
                return Unavailable("rides: recent activity is temporarily unavailable");

            using var activityRequest = new HttpRequestMessage(HttpMethod.Get, options.ActivitiesEndpoint);
            activityRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var activityResponse = await httpClient.SendAsync(activityRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (activityResponse.StatusCode != HttpStatusCode.OK)
                return Unavailable("rides: recent activity is temporarily unavailable");
            await using var activityStream = await activityResponse.Content.ReadAsStreamAsync(timeout.Token);
            using var activityDocument = await JsonDocument.ParseAsync(activityStream, cancellationToken: timeout.Token);
            var result = Format(activityDocument.RootElement);
            _cached = result;
            _expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(options.CacheMinutes, 5, 15));
            return result;
        }
        catch
        {
            return Unavailable("rides: recent activity is temporarily unavailable");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
        _cacheLock.Dispose();
    }

    private static CommandResult Format(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            return Unavailable("rides: recent activity is temporarily unavailable");

        var rides = root.EnumerateArray()
            .Where(item => GetString(item, "type") is "Ride" or "VirtualRide")
            .Select(item => new Ride(
                DateTimeOffset.TryParse(GetString(item, "start_date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var started) ? started : (DateTimeOffset?)null,
                item.TryGetProperty("distance", out var distance) && distance.TryGetDouble(out var metres) ? Math.Max(0, metres / 1000) : 0,
                item.TryGetProperty("moving_time", out var duration) && duration.TryGetInt32(out var seconds) ? Math.Max(0, seconds) : 0,
                GetString(item, "type") == "VirtualRide"))
            .Take(8)
            .ToArray();

        List<OutputLine> lines = [new TextLine("RECENT RIDES / STRAVA") { Style = "heading" }];
        if (rides.Length == 0)
        {
            lines.Add(new TextLine("No recent rides are available."));
            return new(lines);
        }

        var maximum = Math.Max(1, rides.Max(ride => ride.Kilometres));
        for (var index = 0; index < rides.Length; index++)
        {
            var ride = rides[index];
            var width = Math.Max(1, (int)Math.Round(ride.Kilometres / maximum * 20));
            var label = ride.StartedAt?.ToString("dd MMM", CultureInfo.InvariantCulture) ?? $"ride {index + 1}";
            var value = $"{ride.Kilometres:F1} km / {TimeSpan.FromSeconds(ride.Seconds):h\\:mm} / {(ride.Virtual ? "virtual" : "outdoor")}";
            lines.Add(new ChartLine(label, new string('#', width), value, $"{label}: {value}"));
        }
        return new(lines);
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool IsSecureEndpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static CommandResult Unavailable(string message) => new([new TextLine(message), new TextLine("Configure server-side credentials to enable this command.")], 1);
    private sealed record Ride(DateTimeOffset? StartedAt, double Kilometres, int Seconds, bool Virtual);
}
