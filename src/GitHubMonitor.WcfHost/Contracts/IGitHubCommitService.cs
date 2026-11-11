using CoreWCF;

namespace GitHubMonitor.WcfHost.Contracts;

/// <summary>
/// WCF contract exposed over BasicHttpBinding (SOAP 1.1).
/// WSDL: http://localhost:5100/GitHubCommitService.svc?wsdl
/// </summary>
[ServiceContract(Name = "IGitHubCommitService", Namespace = ContractNamespaces.Service)]
public interface IGitHubCommitService
{
    /// <summary>Calls the GitHub API directly and returns the latest 10 commits.</summary>
    [OperationContract(Name = "GetLiveCommits")]
    Task<CommitQueryResult> GetLiveCommitsAsync();

    /// <summary>Returns the commits cached by the Quartz job, plus LastSyncTime.</summary>
    [OperationContract(Name = "GetScheduledCommits")]
    Task<CommitQueryResult> GetScheduledCommitsAsync();
}

public static class ContractNamespaces
{
    public const string Service = "urn:github-monitor:service:v1";
    public const string Data = "urn:github-monitor:data:v1";
}
