using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.Configuration;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace GitHubMonitor.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the GitHub typed HttpClient and its options.</summary>
    public static IServiceCollection AddGitHubIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GitHubOptions>()
            .Bind(configuration.GetSection(GitHubOptions.SectionName))
            .PostConfigure(o =>
            {
                // Also accept the conventional GITHUB_TOKEN environment variable.
                if (!o.HasToken)
                {
                    o.Token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddTimeProvider();

        services.AddHttpClient<IGitHubCommitsClient, GitHubCommitsClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
            http.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }

    /// <summary>Registers the cache, the Quartz scheduler and the 1-minute sync job.</summary>
    public static IServiceCollection AddCommitSyncScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SchedulerOptions>()
            .Bind(configuration.GetSection(SchedulerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ICommitCache>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SchedulerOptions>>().Value;
            var env = sp.GetRequiredService<IHostEnvironment>();
            var path = Path.IsPathRooted(options.CacheFilePath)
                ? options.CacheFilePath
                : Path.Combine(env.ContentRootPath, options.CacheFilePath);
            return new JsonFileCommitCache(path, sp.GetRequiredService<ILogger<JsonFileCommitCache>>());
        });

        var intervalSeconds = configuration.GetSection(SchedulerOptions.SectionName)
            .GetValue(nameof(SchedulerOptions.IntervalSeconds), 60);

        services.AddQuartz(q =>
        {
            q.SchedulerName = "GitHubMonitorScheduler";
            q.AddJob<GitHubCommitSyncJob>(job => job
                .WithIdentity(GitHubCommitSyncJob.Key)
                .WithDescription("Fetches the latest commits from GitHub and caches them"));

            q.AddTrigger(trigger => trigger
                .ForJob(GitHubCommitSyncJob.Key)
                .WithIdentity("github-commit-sync-trigger", "github")
                .StartNow() // Fill the cache right away, then every interval.
                .WithSimpleSchedule(s => s
                    .WithIntervalInSeconds(intervalSeconds)
                    .RepeatForever()
                    .WithMisfireHandlingInstructionNextWithRemainingCount()));
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
