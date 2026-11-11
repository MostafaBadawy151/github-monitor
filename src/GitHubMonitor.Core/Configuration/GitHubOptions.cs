using System.ComponentModel.DataAnnotations;

namespace GitHubMonitor.Core.Configuration;

/// <summary>
/// Settings for the GitHub REST API integration.
/// Bound from the "GitHub" configuration section.
/// </summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    [Required, Url]
    public string ApiBaseUrl { get; set; } = "https://api.github.com/";

    [Required]
    public string Owner { get; set; } = "company";

    [Required]
    public string Repository { get; set; } = "integration-task-private-repo";

    /// <summary>
    /// Personal Access Token. Never commit a real value. Supply it via
    /// user-secrets, the <c>GitHub__Token</c> environment variable, or <c>GITHUB_TOKEN</c>.
    /// </summary>
    public string? Token { get; set; }

    [Required]
    public string UserAgent { get; set; } = "IntegrationTaskApp";

    [Range(1, 100)]
    public int CommitCount { get; set; } = 10;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 15;

    public bool HasToken => !string.IsNullOrWhiteSpace(Token);
}
