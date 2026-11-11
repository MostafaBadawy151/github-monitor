using System.Text.Json.Serialization;

namespace GitHubMonitor.Core.GitHub;

// Minimal shapes of the GitHub "List commits" response; only the fields we use.
// https://docs.github.com/en/rest/commits/commits#list-commits

internal sealed class GitHubCommitResponse
{
    [JsonPropertyName("sha")] public string Sha { get; set; } = string.Empty;
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    [JsonPropertyName("commit")] public GitHubCommitDetail? Commit { get; set; }
    [JsonPropertyName("author")] public GitHubUser? Author { get; set; }
}

internal sealed class GitHubCommitDetail
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("author")] public GitHubGitActor? Author { get; set; }
    [JsonPropertyName("committer")] public GitHubGitActor? Committer { get; set; }
}

internal sealed class GitHubGitActor
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("date")] public DateTimeOffset? Date { get; set; }
}

internal sealed class GitHubUser
{
    [JsonPropertyName("login")] public string? Login { get; set; }
}

internal sealed class GitHubErrorResponse
{
    [JsonPropertyName("message")] public string? Message { get; set; }
}
