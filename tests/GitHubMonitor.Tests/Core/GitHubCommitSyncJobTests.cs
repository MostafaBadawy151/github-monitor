using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;
using GitHubMonitor.Core.Scheduling;
using GitHubMonitor.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace GitHubMonitor.Tests.Core;

public class GitHubCommitSyncJobTests
{
    private readonly IGitHubCommitsClient _gitHub = Substitute.For<IGitHubCommitsClient>();
    private readonly ICommitCache _cache = Substitute.For<ICommitCache>();
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.Parse("2025-06-01T12:00:00Z"));
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();

    private GitHubCommitSyncJob CreateJob() => new(_gitHub, _cache, _clock, NullLogger<GitHubCommitSyncJob>.Instance);

    [Fact]
    public async Task Stores_commits_with_last_sync_time_on_success()
    {
        IReadOnlyList<CommitInfo> commits = [new("sha", "msg", "a", null, DateTimeOffset.UtcNow, null)];
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>()).Returns(commits);

        await CreateJob().Execute(_context);

        await _cache.Received(1).StoreAsync(commits, _clock.Now, Arg.Any<CancellationToken>());
        await _cache.DidNotReceiveWithAnyArgs().RecordFailureAsync(default, default!, default);
    }

    [Fact]
    public async Task Records_failure_and_does_not_throw_on_github_error()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new GitHubApiException(GitHubErrorKind.Unauthorized, "401!"));

        await CreateJob().Execute(_context); // must not throw, so Quartz keeps the trigger healthy

        await _cache.Received(1).RecordFailureAsync(GitHubErrorKind.Unauthorized, "401!", _clock.Now, Arg.Any<CancellationToken>());
        await _cache.DidNotReceiveWithAnyArgs().StoreAsync(default!, default, default);
    }

    [Fact]
    public async Task Records_failure_on_unexpected_exception()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await CreateJob().Execute(_context);

        await _cache.Received(1).RecordFailureAsync(GitHubErrorKind.Upstream, Arg.Any<string>(), _clock.Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Job_disallows_concurrent_execution()
    {
        Assert.True(Attribute.IsDefined(typeof(GitHubCommitSyncJob), typeof(DisallowConcurrentExecutionAttribute)));
    }
}
