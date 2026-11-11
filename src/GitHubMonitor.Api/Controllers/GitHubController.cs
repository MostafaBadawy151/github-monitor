using GitHubMonitor.Api.Models;
using GitHubMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitHubMonitor.Api.Controllers;

[ApiController]
[Route("api/github")]
[Produces("application/json")]
public sealed class GitHubController : ControllerBase
{
    private readonly IGitHubCommitsService _service;

    public GitHubController(IGitHubCommitsService service) => _service = service;

    /// <summary>Latest 10 commits fetched in real time (WCF, then GetLiveCommits, then GitHub).</summary>
    [HttpGet("live")]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ApiResponse<CommitListDto>>> GetLive(CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetLiveAsync(cancellationToken));

    /// <summary>Commits cached by the Quartz job (WCF, then GetScheduledCommits), including LastSyncTime.</summary>
    [HttpGet("scheduled")]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ApiResponse<CommitListDto>>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ApiResponse<CommitListDto>>> GetScheduled(CancellationToken cancellationToken) =>
        ToActionResult(await _service.GetScheduledAsync(cancellationToken));

    private ObjectResult ToActionResult<T>(ServiceResult<T> result) =>
        StatusCode((int)result.StatusCode, new ApiResponse<T>(result.Success, result.Message, result.Data));
}
