using System.Text.Json;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;
using Microsoft.Extensions.Logging;

namespace GitHubMonitor.Core.Caching;

/// <summary>
/// Two-level cache: an in-memory snapshot serves reads, and a JSON file
/// keeps the data across restarts. Writes go to a temp file and are then
/// swapped in, so a crash mid-write cannot corrupt the cache.
/// </summary>
public sealed class JsonFileCommitCache : ICommitCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<JsonFileCommitCache> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile CommitCacheSnapshot _current;

    public JsonFileCommitCache(string filePath, ILogger<JsonFileCommitCache> logger)
    {
        _filePath = Path.GetFullPath(filePath);
        _logger = logger;
        _current = LoadFromDisk();
    }

    public CommitCacheSnapshot Current => _current;

    public string FilePath => _filePath;

    public Task StoreAsync(IReadOnlyList<CommitInfo> commits, DateTimeOffset syncTime, CancellationToken cancellationToken = default) =>
        UpdateAsync(_ => new CommitCacheSnapshot
        {
            Commits = commits.ToList(),
            LastSyncTime = syncTime,
            LastAttemptTime = syncTime,
            LastErrorKind = GitHubErrorKind.None,
            LastErrorMessage = null
        }, cancellationToken);

    public Task RecordFailureAsync(GitHubErrorKind kind, string message, DateTimeOffset attemptTime, CancellationToken cancellationToken = default) =>
        UpdateAsync(previous => previous with
        {
            LastAttemptTime = attemptTime,
            LastErrorKind = kind,
            LastErrorMessage = message
        }, cancellationToken);

    private async Task UpdateAsync(Func<CommitCacheSnapshot, CommitCacheSnapshot> mutate, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            var next = mutate(_current);
            _current = next; // Publish in memory first so readers get fresh data even if disk I/O fails.
            await PersistAsync(next, ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task PersistAsync(CommitCacheSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, CacheFile.From(snapshot), SerializerOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The in-memory cache still works; disk persistence is best-effort.
            _logger.LogError(ex, "Failed to persist commit cache to {Path}", _filePath);
        }
    }

    private CommitCacheSnapshot LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            return CommitCacheSnapshot.Empty;
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            var file = JsonSerializer.Deserialize<CacheFile>(stream, SerializerOptions);
            var snapshot = file?.ToSnapshot() ?? CommitCacheSnapshot.Empty;
            _logger.LogInformation("Loaded {Count} cached commits from {Path} (LastSyncTime: {LastSyncTime})",
                snapshot.Commits.Count, _filePath, snapshot.LastSyncTime);
            return snapshot;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Ignoring unreadable cache file {Path}", _filePath);
            return CommitCacheSnapshot.Empty;
        }
    }

    /// <summary>On-disk format, kept separate from the domain snapshot so the file can be versioned.</summary>
    private sealed class CacheFile
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset? LastSyncTime { get; set; }
        public DateTimeOffset? LastAttemptTime { get; set; }
        public GitHubErrorKind LastErrorKind { get; set; }
        public string? LastErrorMessage { get; set; }
        public List<CommitInfo> Commits { get; set; } = [];

        public static CacheFile From(CommitCacheSnapshot s) => new()
        {
            LastSyncTime = s.LastSyncTime,
            LastAttemptTime = s.LastAttemptTime,
            LastErrorKind = s.LastErrorKind,
            LastErrorMessage = s.LastErrorMessage,
            Commits = s.Commits.ToList()
        };

        public CommitCacheSnapshot ToSnapshot() => new()
        {
            LastSyncTime = LastSyncTime,
            LastAttemptTime = LastAttemptTime,
            LastErrorKind = LastErrorKind,
            LastErrorMessage = LastErrorMessage,
            Commits = Commits
        };
    }
}
