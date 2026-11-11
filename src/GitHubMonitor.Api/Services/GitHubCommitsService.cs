using System.Net;
using GitHubMonitor.Api.Models;
using GitHubMonitor.Api.Wcf;

namespace GitHubMonitor.Api.Services;

/// <summary>
/// Calls the WCF service, maps its data contracts to API DTOs, and turns both
/// business errors (GitHub 401, timeout, ...) and transport errors (WCF down) into
/// clear HTTP results.
/// </summary>
public sealed class GitHubCommitsService : IGitHubCommitsService
{
    private readonly IGitHubCommitServiceClient _wcf;
    private readonly ILogger<GitHubCommitsService> _logger;

    public GitHubCommitsService(IGitHubCommitServiceClient wcf, ILogger<GitHubCommitsService> logger)
    {
        _wcf = wcf;
        _logger = logger;
    }

    public Task<ServiceResult<CommitListDto>> GetLiveAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("live", ct => _wcf.GetLiveCommitsAsync(ct), cancellationToken);

    public Task<ServiceResult<CommitListDto>> GetScheduledAsync(CancellationToken cancellationToken) =>
        ExecuteAsync("scheduled", ct => _wcf.GetScheduledCommitsAsync(ct), cancellationToken);

    private async Task<ServiceResult<CommitListDto>> ExecuteAsync(
        string name,
        Func<CancellationToken, Task<CommitQueryResult>> call,
        CancellationToken cancellationToken)
    {
        CommitQueryResult result;
        try
        {
            result = await call(cancellationToken);
        }
        catch (WcfServiceException ex)
        {
            var status = ex.Kind switch
            {
                WcfFailureKind.Timeout => HttpStatusCode.GatewayTimeout,
                WcfFailureKind.Unavailable => HttpStatusCode.ServiceUnavailable,
                _ => HttpStatusCode.BadGateway
            };
            return ServiceResult<CommitListDto>.Fail(status, ex.Message);
        }

        if (!result.Success)
        {
            _logger.LogWarning("WCF returned failure for {Name} commits: {ErrorCode} - {Message}",
                name, result.ErrorCode, result.Message);
            return ServiceResult<CommitListDto>.Fail(MapErrorCode(result.ErrorCode), result.Message);
        }

        var dto = ToDto(result);
        return ServiceResult<CommitListDto>.Ok(dto, result.Message);
    }

    /// <summary>
    /// GitHub-side failures map to gateway-class statuses (502/503/504), not to 401/403.
    /// A 401 from <i>this</i> API would tell the browser that <i>its</i> credentials were
    /// rejected, which is wrong: the server-side GitHub token failed. The response message
    /// still says exactly what happened (e.g. "GitHub rejected the credentials (401)").
    /// </summary>
    internal static HttpStatusCode MapErrorCode(string errorCode) => errorCode switch
    {
        "Unauthorized" or "Forbidden" or "NotFound" or "Upstream" or "Network" => HttpStatusCode.BadGateway,
        "Timeout" => HttpStatusCode.GatewayTimeout,
        "RateLimited" or "NotConfigured" => HttpStatusCode.ServiceUnavailable,
        _ => HttpStatusCode.InternalServerError
    };

    internal static CommitListDto ToDto(CommitQueryResult result) => new()
    {
        Source = result.Source,
        Count = result.Commits.Count,
        LastSyncTime = ToUtcOffset(result.LastSyncTime),
        LastAttemptTime = ToUtcOffset(result.LastAttemptTime),
        IsStale = result.IsStale,
        Commits = result.Commits.Select(c => new CommitDto
        {
            Sha = c.Sha,
            ShortSha = c.Sha.Length > 7 ? c.Sha[..7] : c.Sha,
            Message = c.Message,
            MessageHeadline = c.Message.Split('\n', 2)[0].TrimEnd('\r'),
            Author = c.Author,
            AuthorLogin = c.AuthorLogin,
            Date = new DateTimeOffset(DateTime.SpecifyKind(c.Date, DateTimeKind.Utc)),
            Url = c.Url,
            Source = string.IsNullOrEmpty(c.Source) ? result.Source : c.Source
        }).ToList()
    };

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
