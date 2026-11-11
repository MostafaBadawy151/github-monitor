using System.Runtime.Serialization;
using System.ServiceModel;

namespace GitHubMonitor.Api.Wcf;

// Client-side WCF contract, equivalent to what `dotnet-svcutil` would generate from
// http://localhost:5100/GitHubCommitService.svc?wsdl. Names and namespaces must match the service.

internal static class WcfNamespaces
{
    public const string Service = "urn:github-monitor:service:v1";
    public const string Data = "urn:github-monitor:data:v1";
}

[ServiceContract(Name = "IGitHubCommitService", Namespace = WcfNamespaces.Service)]
public interface IGitHubCommitServiceContract
{
    [OperationContract(Name = "GetLiveCommits")]
    Task<CommitQueryResult> GetLiveCommitsAsync();

    [OperationContract(Name = "GetScheduledCommits")]
    Task<CommitQueryResult> GetScheduledCommitsAsync();
}

public interface IGitHubCommitServiceChannel : IGitHubCommitServiceContract, IClientChannel;

[DataContract(Name = "CommitQueryResult", Namespace = WcfNamespaces.Data)]
public sealed class CommitQueryResult
{
    [DataMember(Order = 1)] public bool Success { get; set; }
    [DataMember(Order = 2)] public string ErrorCode { get; set; } = "None";
    [DataMember(Order = 3)] public string Message { get; set; } = string.Empty;
    [DataMember(Order = 4)] public string Source { get; set; } = string.Empty;
    [DataMember(Order = 5)] public List<CommitData> Commits { get; set; } = [];
    [DataMember(Order = 6)] public DateTime? LastSyncTime { get; set; }
    [DataMember(Order = 7)] public DateTime? LastAttemptTime { get; set; }
    [DataMember(Order = 8)] public bool IsStale { get; set; }
}

[DataContract(Name = "CommitData", Namespace = WcfNamespaces.Data)]
public sealed class CommitData
{
    [DataMember(Order = 1)] public string Sha { get; set; } = string.Empty;
    [DataMember(Order = 2)] public string Message { get; set; } = string.Empty;
    [DataMember(Order = 3)] public string Author { get; set; } = string.Empty;
    [DataMember(Order = 4)] public string? AuthorLogin { get; set; }
    [DataMember(Order = 5)] public DateTime Date { get; set; }
    [DataMember(Order = 6)] public string? Url { get; set; }
    [DataMember(Order = 7)] public string Source { get; set; } = string.Empty;
}
