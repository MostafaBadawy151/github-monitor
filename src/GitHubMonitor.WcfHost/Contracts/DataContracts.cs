using System.Runtime.Serialization;

namespace GitHubMonitor.WcfHost.Contracts;

/// <summary>
/// Result envelope for both operations. Expected failures (401, timeout, rate limit, ...)
/// come back as data, not as SOAP faults, so the client can tell them apart
/// without parsing fault strings.
/// </summary>
[DataContract(Name = "CommitQueryResult", Namespace = ContractNamespaces.Data)]
public sealed class CommitQueryResult
{
    [DataMember(Order = 1)] public bool Success { get; set; }

    /// <summary>One of <see cref="ServiceErrorCodes"/>, or "None".</summary>
    [DataMember(Order = 2)] public string ErrorCode { get; set; } = ServiceErrorCodes.None;

    [DataMember(Order = 3)] public string Message { get; set; } = string.Empty;

    /// <summary>"Live" or "Scheduled".</summary>
    [DataMember(Order = 4)] public string Source { get; set; } = string.Empty;

    [DataMember(Order = 5)] public List<CommitData> Commits { get; set; } = [];

    /// <summary>UTC. For Scheduled, when the cache was last refreshed; for Live, when GitHub was called.</summary>
    [DataMember(Order = 6)] public DateTime? LastSyncTime { get; set; }

    /// <summary>UTC. Last time the scheduler attempted a sync (Scheduled only).</summary>
    [DataMember(Order = 7)] public DateTime? LastAttemptTime { get; set; }

    /// <summary>True when the cache is served but the latest sync attempt failed (Scheduled only).</summary>
    [DataMember(Order = 8)] public bool IsStale { get; set; }
}

[DataContract(Name = "CommitData", Namespace = ContractNamespaces.Data)]
public sealed class CommitData
{
    [DataMember(Order = 1)] public string Sha { get; set; } = string.Empty;
    [DataMember(Order = 2)] public string Message { get; set; } = string.Empty;
    [DataMember(Order = 3)] public string Author { get; set; } = string.Empty;
    [DataMember(Order = 4)] public string? AuthorLogin { get; set; }

    /// <summary>UTC commit author date.</summary>
    [DataMember(Order = 5)] public DateTime Date { get; set; }

    [DataMember(Order = 6)] public string? Url { get; set; }
    [DataMember(Order = 7)] public string Source { get; set; } = string.Empty;
}

public static class CommitSources
{
    public const string Live = "Live";
    public const string Scheduled = "Scheduled";
}

public static class ServiceErrorCodes
{
    public const string None = "None";
    public const string Unauthorized = "Unauthorized";
    public const string Forbidden = "Forbidden";
    public const string RateLimited = "RateLimited";
    public const string NotFound = "NotFound";
    public const string Timeout = "Timeout";
    public const string Network = "Network";
    public const string Upstream = "Upstream";
    public const string NotConfigured = "NotConfigured";
    public const string NotYetSynced = "NotYetSynced";
    public const string InternalError = "InternalError";
}
