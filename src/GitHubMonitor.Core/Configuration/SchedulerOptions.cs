using System.ComponentModel.DataAnnotations;

namespace GitHubMonitor.Core.Configuration;

/// <summary>
/// Settings for the Quartz sync job and its cache. Bound from the "Scheduler" section.
/// </summary>
public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>How often the sync job runs. The task requires 1 minute.</summary>
    [Range(10, 86_400)]
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>JSON cache file. Relative paths are resolved against the host's content root.</summary>
    [Required]
    public string CacheFilePath { get; set; } = "App_Data/commits-cache.json";
}
