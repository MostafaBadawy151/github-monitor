using System.ComponentModel.DataAnnotations;

namespace GitHubMonitor.Api.Configuration;

/// <summary>Settings for the WCF client. Bound from the "WcfService" section.</summary>
public sealed class WcfClientOptions
{
    public const string SectionName = "WcfService";

    [Required, Url]
    public string EndpointUrl { get; set; } = "http://localhost:5100/GitHubCommitService.svc";

    /// <summary>Connection timeout.</summary>
    [Range(1, 120)]
    public int OpenTimeoutSeconds { get; set; } = 10;

    /// <summary>Total time allowed for a single call (request + response).</summary>
    [Range(1, 300)]
    public int SendTimeoutSeconds { get; set; } = 30;
}
