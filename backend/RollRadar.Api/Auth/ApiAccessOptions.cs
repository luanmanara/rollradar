namespace RollRadar.Api.Auth;

public class ApiAccessOptions
{
    public const string Section = "ApiAccess";

    /// <summary>Requests per minute each token may make to /api/v1/dados.</summary>
    public int TokenRequestsPerMinute { get; set; } = 60;

    /// <summary>Requests per minute each IP address may make to the site's open search endpoints.</summary>
    public int SiteRequestsPerMinute { get; set; } = 120;

    /// <summary>
    /// Videos the site's search returns when no technique is given: the most recent ones, so the open endpoint
    /// cannot hand out every moment at once.
    /// </summary>
    public int UnfilteredSearchMaxVideos { get; set; } = 50;
}
