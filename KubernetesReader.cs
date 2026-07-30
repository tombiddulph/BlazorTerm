using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace BlazorTerm;

public interface IKubernetesReader
{
    ValueTask<CommandResult> GetAsync(string resource, CancellationToken cancellationToken = default);
}

public sealed class KubernetesReader : IKubernetesReader, IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> ResourcePaths = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pods"] = "/api/v1/pods?limit=200",
        ["nodes"] = "/api/v1/nodes?limit=200",
        ["namespaces"] = "/api/v1/namespaces?limit=200"
    };

    private readonly KubernetesOptions _options;
    private readonly HttpClient? _httpClient;
    private readonly Func<CancellationToken, ValueTask<string>>? _tokenProvider;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    public KubernetesReader(
        KubernetesOptions options,
        HttpClient? httpClient,
        Func<CancellationToken, ValueTask<string>>? tokenProvider)
    {
        _options = options;
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    public static KubernetesReader Create(KubernetesOptions options)
    {
        if (!options.Enabled)
            return new(options, null, null);

        try
        {
            if (!Uri.TryCreate(options.ApiServer, UriKind.Absolute, out var apiServer) || apiServer.Scheme != Uri.UriSchemeHttps)
                return new(options, null, null);
            var ca = X509Certificate2.CreateFromPemFile(options.CaFile);
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
                    ValidateServerCertificate(ca, certificate, chain, errors)
            };
            var client = new HttpClient(handler) { BaseAddress = apiServer };
            return new(options, client, async cancellationToken =>
                (await File.ReadAllTextAsync(options.TokenFile, cancellationToken)).Trim());
        }
        catch
        {
            return new(options, null, null);
        }
    }

    public async ValueTask<CommandResult> GetAsync(string resource, CancellationToken cancellationToken = default)
    {
        if (!ResourcePaths.TryGetValue(resource, out var path))
            return Error("kubectl: only 'get pods', 'get nodes', and 'get namespaces' are available", 2);
        if (!_options.Enabled)
            return Unavailable("kubectl: live cluster view is disabled");
        if (_httpClient is null || _tokenProvider is null)
            return Unavailable("kubectl: live cluster view is unavailable");

        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(resource, out var cached) && cached.ExpiresAt > now)
            return cached.Result;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_cache.TryGetValue(resource, out cached) && cached.ExpiresAt > now)
                return cached.Result;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 30)));
            var token = await _tokenProvider(timeout.Token);
            if (string.IsNullOrWhiteSpace(token))
                return Unavailable("kubectl: live cluster view is unavailable");

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                return Unavailable("kubectl: live cluster view is temporarily unavailable");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var result = Format(resource, document.RootElement);
            _cache[resource] = new(result, now.AddSeconds(Math.Clamp(_options.CacheSeconds, 30, 60)));
            return result;
        }
        catch
        {
            return Unavailable("kubectl: live cluster view is temporarily unavailable");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _cacheLock.Dispose();
    }

    private static CommandResult Format(string resource, JsonElement root)
    {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Unavailable("kubectl: live cluster view returned no usable data");

        var snapshots = items.EnumerateArray()
            .Select(item => ReadSnapshot(resource, item))
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .OrderBy(snapshot => snapshot.PrivateName, StringComparer.Ordinal)
            .Take(200)
            .ToArray();

        List<OutputLine> lines =
        [
            new TextLine($"KUBERNETES / {resource.ToUpperInvariant()} (sanitized)") { Style = "heading" }
        ];
        for (var index = 0; index < snapshots.Length; index++)
        {
            var snapshot = snapshots[index];
            var alias = $"{resource.TrimEnd('s')}-{index + 1:00}";
            lines.Add(new TableLine([alias, snapshot.Status, FormatAge(snapshot.CreatedAt), snapshot.Detail]));
        }
        if (snapshots.Length == 0)
            lines.Add(new TextLine("No resources are currently visible."));
        lines.Add(new TextLine("Names and identifying cluster metadata are intentionally hidden.") { Style = "muted" });
        return new(lines);
    }

    private static ResourceSnapshot? ReadSnapshot(string resource, JsonElement item)
    {
        var metadata = item.TryGetProperty("metadata", out var metadataValue) ? metadataValue : default;
        var privateName = GetString(metadata, "name");
        if (string.IsNullOrEmpty(privateName))
            return null;
        var createdAt = DateTimeOffset.TryParse(GetString(metadata, "creationTimestamp"), out var parsed) ? parsed : (DateTimeOffset?)null;
        var status = item.TryGetProperty("status", out var statusValue) ? statusValue : default;

        if (resource == "pods")
        {
            var phase = SafeStatus(GetString(status, "phase"));
            var ready = 0;
            var total = 0;
            var restarts = 0;
            if (status.ValueKind == JsonValueKind.Object && status.TryGetProperty("containerStatuses", out var containers) && containers.ValueKind == JsonValueKind.Array)
            {
                foreach (var container in containers.EnumerateArray())
                {
                    total++;
                    if (container.TryGetProperty("ready", out var readyValue) && readyValue.ValueKind == JsonValueKind.True)
                        ready++;
                    if (container.TryGetProperty("restartCount", out var restartValue) && restartValue.TryGetInt32(out var count))
                        restarts += Math.Max(0, count);
                }
            }
            return new(privateName, phase, createdAt, $"ready {ready}/{total}  restarts {restarts}");
        }

        if (resource == "nodes")
        {
            var ready = "Unknown";
            if (status.ValueKind == JsonValueKind.Object && status.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
            {
                var condition = conditions.EnumerateArray().FirstOrDefault(value => GetString(value, "type") == "Ready");
                ready = GetString(condition, "status") == "True" ? "Ready" : "NotReady";
            }
            return new(privateName, ready, createdAt, "role hidden");
        }

        return new(privateName, SafeStatus(GetString(status, "phase")), createdAt, "metadata hidden");
    }

    private static string GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string SafeStatus(string value) => value switch
    {
        "Active" or "Bound" or "Failed" or "Pending" or "Running" or "Succeeded" or "Terminating" or "Unknown" => value,
        _ => "Unknown"
    };

    private static string FormatAge(DateTimeOffset? createdAt)
    {
        if (createdAt is null)
            return "age unknown";
        var age = DateTimeOffset.UtcNow - createdAt.Value;
        return age.TotalDays >= 1 ? $"{(int)age.TotalDays}d" : age.TotalHours >= 1 ? $"{(int)age.TotalHours}h" : $"{Math.Max(0, (int)age.TotalMinutes)}m";
    }

    private static bool ValidateServerCertificate(
        X509Certificate2 ca,
        X509Certificate2? certificate,
        X509Chain? serverChain,
        SslPolicyErrors errors)
    {
        if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
            return false;
        using var customChain = new X509Chain();
        customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        customChain.ChainPolicy.CustomTrustStore.Add(ca);
        customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (serverChain is not null)
        {
            foreach (var element in serverChain.ChainElements.Skip(1))
                customChain.ChainPolicy.ExtraStore.Add(element.Certificate);
        }
        return customChain.Build(certificate);
    }

    private static CommandResult Unavailable(string message) => new([new TextLine(message), new TextLine("No cluster details were disclosed.")], 1);
    private static CommandResult Error(string message, int exitCode) => new([new TextLine(message) { Style = "error" }], exitCode);
    private sealed record CacheEntry(CommandResult Result, DateTimeOffset ExpiresAt);
    private sealed record ResourceSnapshot(string PrivateName, string Status, DateTimeOffset? CreatedAt, string Detail);
}
