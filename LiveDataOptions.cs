namespace BlazorTerm;

public sealed class KubernetesOptions
{
    public const string SectionName = "Kubernetes";
    public bool Enabled { get; set; }
    public string ApiServer { get; set; } = "https://kubernetes.default.svc";
    public string TokenFile { get; set; } = "/var/run/secrets/kubernetes.io/serviceaccount/token";
    public string CaFile { get; set; } = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";
    public int CacheSeconds { get; set; } = 45;
    public int TimeoutSeconds { get; set; } = 5;
}

public sealed class StravaOptions
{
    public const string SectionName = "Strava";
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string OAuthEndpoint { get; set; } = "https://www.strava.com/oauth/token";
    public string ActivitiesEndpoint { get; set; } = "https://www.strava.com/api/v3/athlete/activities?per_page=20";
    public int CacheMinutes { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 8;
}
