namespace GitHubMonitor.Api.Models;

/// <summary>One commit row, as rendered by the Kendo Grid.</summary>
public sealed record CommitDto
{
    public required string Sha { get; init; }
    public required string ShortSha { get; init; }

    /// <summary>Full commit message.</summary>
    public required string Message { get; init; }

    /// <summary>First line of the message (the "subject"), handy for compact display.</summary>
    public required string MessageHeadline { get; init; }

    public required string Author { get; init; }
    public string? AuthorLogin { get; init; }
    public required DateTimeOffset Date { get; init; }
    public string? Url { get; init; }

    /// <summary>"Live" or "Scheduled".</summary>
    public required string Source { get; init; }
}

/// <summary>Payload of both endpoints.</summary>
public sealed record CommitListDto
{
    public required string Source { get; init; }
    public required int Count { get; init; }

    /// <summary>
    /// Scheduled: when the Quartz job last refreshed the cache ("LastSyncTime").
    /// Live: when GitHub was queried.
    /// </summary>
    public DateTimeOffset? LastSyncTime { get; init; }

    /// <summary>Scheduled only: when the job last ran, successful or not.</summary>
    public DateTimeOffset? LastAttemptTime { get; init; }

    /// <summary>Scheduled only: true when the latest sync failed and older cached data is served.</summary>
    public bool IsStale { get; init; }

    public required IReadOnlyList<CommitDto> Commits { get; init; }
}
