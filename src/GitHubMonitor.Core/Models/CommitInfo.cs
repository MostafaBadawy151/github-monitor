namespace GitHubMonitor.Core.Models;

/// <summary>Transport-neutral representation of a single commit.</summary>
public sealed record CommitInfo(
    string Sha,
    string Message,
    string Author,
    string? AuthorLogin,
    DateTimeOffset Date,
    string? HtmlUrl);
