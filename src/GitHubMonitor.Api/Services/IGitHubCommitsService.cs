using GitHubMonitor.Api.Models;

namespace GitHubMonitor.Api.Services;

public interface IGitHubCommitsService
{
    Task<ServiceResult<CommitListDto>> GetLiveAsync(CancellationToken cancellationToken);

    Task<ServiceResult<CommitListDto>> GetScheduledAsync(CancellationToken cancellationToken);
}
