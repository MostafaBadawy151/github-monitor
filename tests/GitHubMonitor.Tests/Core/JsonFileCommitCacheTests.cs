using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitHubMonitor.Tests.Core;

public sealed class JsonFileCommitCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ghm-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "nested", "cache.json");

    private JsonFileCommitCache NewCache() => new(FilePath, NullLogger<JsonFileCommitCache>.Instance);

    private static readonly CommitInfo Sample = new("abc123", "Initial commit", "Jane", "jane", DateTimeOffset.Parse("2025-01-01T00:00:00Z"), "https://x");

    [Fact]
    public void Starts_empty_when_no_file_exists()
    {
        var cache = NewCache();
        Assert.False(cache.Current.HasEverSynced);
        Assert.Empty(cache.Current.Commits);
    }

    [Fact]
    public async Task Store_updates_memory_and_survives_restart()
    {
        var syncTime = DateTimeOffset.Parse("2025-06-01T12:00:00Z");
        await NewCache().StoreAsync([Sample], syncTime);

        var reloaded = NewCache(); // simulates a process restart

        Assert.Equal(syncTime, reloaded.Current.LastSyncTime);
        Assert.Equal(Sample, Assert.Single(reloaded.Current.Commits));
        Assert.False(reloaded.Current.LastAttemptFailed);
    }

    [Fact]
    public async Task Failure_keeps_previous_commits_and_last_sync_time()
    {
        var cache = NewCache();
        var syncTime = DateTimeOffset.Parse("2025-06-01T12:00:00Z");
        var failTime = syncTime.AddMinutes(1);
        await cache.StoreAsync([Sample], syncTime);

        await cache.RecordFailureAsync(GitHubErrorKind.Unauthorized, "401", failTime);

        Assert.Single(cache.Current.Commits);
        Assert.Equal(syncTime, cache.Current.LastSyncTime);
        Assert.Equal(failTime, cache.Current.LastAttemptTime);
        Assert.Equal(GitHubErrorKind.Unauthorized, cache.Current.LastErrorKind);
        Assert.Equal(GitHubErrorKind.Unauthorized, NewCache().Current.LastErrorKind); // persisted too
    }

    [Fact]
    public async Task Success_after_failure_clears_error()
    {
        var cache = NewCache();
        await cache.RecordFailureAsync(GitHubErrorKind.Timeout, "timeout", DateTimeOffset.UtcNow);
        await cache.StoreAsync([Sample], DateTimeOffset.UtcNow);

        Assert.False(cache.Current.LastAttemptFailed);
        Assert.Null(cache.Current.LastErrorMessage);
    }

    [Fact]
    public void Corrupt_file_is_ignored()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ this is not json");

        Assert.Empty(NewCache().Current.Commits);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
