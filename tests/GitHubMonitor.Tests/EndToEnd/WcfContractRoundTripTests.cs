using System.Net;
using System.Net.Sockets;
using CoreWCF.Configuration;
using GitHubMonitor.Api.Configuration;
using GitHubMonitor.Api.Wcf;
using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;
using GitHubMonitor.Tests.TestSupport;
using GitHubMonitor.WcfHost.Contracts;
using GitHubMonitor.WcfHost.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GitHubMonitor.Tests.EndToEnd;

/// <summary>
/// Starts a real CoreWCF host on a random port and calls it with the API's
/// System.ServiceModel client. This proves the server contract and the client
/// contract (two separate copies) are wire-compatible, which unit tests cannot.
/// </summary>
public sealed class WcfContractRoundTripTests : IAsyncLifetime
{
    private readonly IGitHubCommitsClient _gitHub = Substitute.For<IGitHubCommitsClient>();
    private readonly ICommitCache _cache = Substitute.For<ICommitCache>();
    private WebApplication? _host;
    private WcfGitHubCommitServiceClient? _client;

    public async Task InitializeAsync()
    {
        var port = GetFreePort();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();
        builder.Services.AddServiceModelServices();
        builder.Services.AddSingleton(_gitHub);
        builder.Services.AddSingleton(_cache);
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(DateTimeOffset.Parse("2025-06-01T12:00:00Z")));
        builder.Services.AddTransient<GitHubCommitService>();

        _host = builder.Build();
        ((IApplicationBuilder)_host).UseServiceModel(sb =>
        {
            sb.AddService<GitHubCommitService>();
            sb.AddServiceEndpoint<GitHubCommitService, IGitHubCommitService>(new CoreWCF.BasicHttpBinding(), "/GitHubCommitService.svc");
        });
        await _host.StartAsync();

        _client = new WcfGitHubCommitServiceClient(
            Options.Create(new WcfClientOptions { EndpointUrl = $"http://127.0.0.1:{port}/GitHubCommitService.svc" }),
            NullLogger<WcfGitHubCommitServiceClient>.Instance);
    }

    [Fact]
    public async Task Live_commits_round_trip_over_soap()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new CommitInfo("abc123", "Line 1\nLine 2", "Jane Doe", "jane", DateTimeOffset.Parse("2025-01-02T03:04:05Z"), "https://example/commit/abc123")
        ]);

        var result = await _client!.GetLiveCommitsAsync();

        Assert.True(result.Success);
        Assert.Equal("Live", result.Source);
        var commit = Assert.Single(result.Commits);
        Assert.Equal("abc123", commit.Sha);
        Assert.Equal("Line 1\nLine 2", commit.Message);
        Assert.Equal("Jane Doe", commit.Author);
        Assert.Equal("jane", commit.AuthorLogin);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), commit.Date.ToUniversalTime());
        Assert.Equal("https://example/commit/abc123", commit.Url);
    }

    [Fact]
    public async Task Error_codes_round_trip_over_soap()
    {
        _gitHub.GetLatestCommitsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new GitHubApiException(GitHubErrorKind.Unauthorized, "GitHub rejected the credentials (401)."));

        var result = await _client!.GetLiveCommitsAsync();

        Assert.False(result.Success);
        Assert.Equal("Unauthorized", result.ErrorCode);
        Assert.Equal("GitHub rejected the credentials (401).", result.Message);
    }

    [Fact]
    public async Task Scheduled_commits_and_last_sync_time_round_trip_over_soap()
    {
        var syncTime = DateTimeOffset.Parse("2025-06-01T11:59:00Z");
        _cache.Current.Returns(new CommitCacheSnapshot
        {
            Commits = [new CommitInfo("def456", "Cached", "Bob", null, syncTime.AddDays(-1), null)],
            LastSyncTime = syncTime,
            LastAttemptTime = syncTime
        });

        var result = await _client!.GetScheduledCommitsAsync();

        Assert.True(result.Success);
        Assert.Equal("Scheduled", result.Source);
        Assert.Equal(syncTime.UtcDateTime, result.LastSyncTime!.Value.ToUniversalTime());
        Assert.Equal("Scheduled", Assert.Single(result.Commits).Source);
    }

    [Fact]
    public async Task Unreachable_service_raises_unavailable()
    {
        using var client = new WcfGitHubCommitServiceClient(
            Options.Create(new WcfClientOptions { EndpointUrl = $"http://127.0.0.1:{GetFreePort()}/GitHubCommitService.svc" }),
            NullLogger<WcfGitHubCommitServiceClient>.Instance);

        var ex = await Assert.ThrowsAsync<WcfServiceException>(() => client.GetLiveCommitsAsync());
        Assert.Equal(WcfFailureKind.Unavailable, ex.Kind);
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null) await _host.DisposeAsync();
    }
}
