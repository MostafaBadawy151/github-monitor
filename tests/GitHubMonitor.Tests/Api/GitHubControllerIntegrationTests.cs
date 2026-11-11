using System.Net;
using System.Text.Json;
using GitHubMonitor.Api.Wcf;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GitHubMonitor.Tests.Api;

/// <summary>Runs the real ASP.NET Core pipeline (routing, DI, serialization, error handling) with a fake WCF client.</summary>
public class GitHubControllerIntegrationTests
{
    private static HttpClient CreateClient(IGitHubCommitServiceClient fake) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton(fake))))
            .CreateClient();

    [Fact]
    public async Task Scheduled_returns_unified_envelope_in_camel_case()
    {
        var fake = Substitute.For<IGitHubCommitServiceClient>();
        fake.GetScheduledCommitsAsync(Arg.Any<CancellationToken>()).Returns(new CommitQueryResult
        {
            Success = true,
            Source = "Scheduled",
            Message = "Returned 1 cached commits.",
            LastSyncTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc),
            Commits = [new CommitData { Sha = "abcdef0123", Message = "Hello", Author = "Jane", Date = DateTime.UtcNow, Source = "Scheduled" }]
        });

        var response = await CreateClient(fake).GetAsync("/api/github/scheduled");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("Returned 1 cached commits.", root.GetProperty("message").GetString());
        var data = root.GetProperty("data");
        Assert.Equal("2025-06-01T12:00:00+00:00", data.GetProperty("lastSyncTime").GetString());
        Assert.Equal("Scheduled", data.GetProperty("commits")[0].GetProperty("source").GetString());
    }

    [Fact]
    public async Task Live_github_401_returns_502_with_envelope()
    {
        var fake = Substitute.For<IGitHubCommitServiceClient>();
        fake.GetLiveCommitsAsync(Arg.Any<CancellationToken>()).Returns(new CommitQueryResult
        {
            Success = false,
            ErrorCode = "Unauthorized",
            Message = "GitHub rejected the credentials (401)."
        });

        var response = await CreateClient(fake).GetAsync("/api/github/live");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("GitHub rejected the credentials (401).", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_with_envelope()
    {
        var fake = Substitute.For<IGitHubCommitServiceClient>();
        fake.GetLiveCommitsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("secret internals"));

        var response = await CreateClient(fake).GetAsync("/api/github/live");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":false", body);
        Assert.DoesNotContain("secret internals", body);
    }
}
