using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;
using GitHubMonitor.Tests.TestSupport;
using GitHubMonitor.WcfHost.Contracts;
using GitHubMonitor.WcfHost.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GitHubMonitor.Tests.Wcf;

public class GitHubCommitServiceTests
{
    private readonly IGitHubCommitsClient _gitHub = Substitute.For<IGitHubCommitsClient>();
    private readonly ICommitCache _cache = Substitute.For<ICommitCache>();
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.Parse("2025-06-01T12:00:00Z"));

    private GitHubCommitService CreateService() => new(_gitHub, _cache, _clock, NullLogger<GitHubCommitService>.Instance);

    private static readonly CommitInfo Sample = new("abc", "msg", "Jane", "jane", DateTimeOffset.Parse("2025-01-01T00:00:00Z"), "u");

    [Fact]
    public async Task Live_returns_commits_tagged_as_live()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>()).Returns([Sample]);

        var result = await CreateService().GetLiveCommitsAsync();

        Assert.True(result.Success);
        Assert.Equal(CommitSources.Live, result.Source);
        Assert.All(result.Commits, c => Assert.Equal(CommitSources.Live, c.Source));
        Assert.Equal(_clock.Now.UtcDateTime, result.LastSyncTime);
    }

    [Fact]
    public async Task Live_returns_error_code_instead_of_fault_on_401()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new GitHubApiException(GitHubErrorKind.Unauthorized, "bad token"));

        var result = await CreateService().GetLiveCommitsAsync();

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorCodes.Unauthorized, result.ErrorCode);
        Assert.Equal("bad token", result.Message);
    }

    [Fact]
    public async Task Scheduled_returns_cached_commits_and_last_sync_time()
    {
        var syncTime = DateTimeOffset.Parse("2025-06-01T11:59:00Z");
        _cache.Current.Returns(new CommitCacheSnapshot { Commits = [Sample], LastSyncTime = syncTime, LastAttemptTime = syncTime });

        var result = await CreateService().GetScheduledCommitsAsync();

        Assert.True(result.Success);
        Assert.False(result.IsStale);
        Assert.Equal(syncTime.UtcDateTime, result.LastSyncTime);
        Assert.Equal(CommitSources.Scheduled, Assert.Single(result.Commits).Source);
        await _gitHub.DidNotReceiveWithAnyArgs().GetLatestCommitsAsync(default); // never calls GitHub
    }

    [Fact]
    public async Task Scheduled_flags_stale_data_when_latest_sync_failed()
    {
        _cache.Current.Returns(new CommitCacheSnapshot
        {
            Commits = [Sample],
            LastSyncTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastAttemptTime = DateTimeOffset.UtcNow,
            LastErrorKind = GitHubErrorKind.Timeout,
            LastErrorMessage = "timed out"
        });

        var result = await CreateService().GetScheduledCommitsAsync();

        Assert.True(result.Success);
        Assert.True(result.IsStale);
        Assert.Contains("timed out", result.Message);
    }

    [Fact]
    public async Task Scheduled_before_first_sync_is_successful_but_empty()
    {
        _cache.Current.Returns(CommitCacheSnapshot.Empty);

        var result = await CreateService().GetScheduledCommitsAsync();

        Assert.True(result.Success);
        Assert.Equal(ServiceErrorCodes.NotYetSynced, result.ErrorCode);
        Assert.Empty(result.Commits);
    }

    [Fact]
    public async Task Scheduled_fails_when_no_sync_has_ever_succeeded()
    {
        _cache.Current.Returns(new CommitCacheSnapshot
        {
            LastAttemptTime = DateTimeOffset.UtcNow,
            LastErrorKind = GitHubErrorKind.Unauthorized,
            LastErrorMessage = "401"
        });

        var result = await CreateService().GetScheduledCommitsAsync();

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorCodes.Unauthorized, result.ErrorCode);
    }
}
