using System.Net;
using GitHubMonitor.Core.Configuration;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitHubMonitor.Tests.Core;

public class GitHubCommitsClientTests
{
    private static GitHubCommitsClient CreateClient(StubHttpMessageHandler handler, GitHubOptions? options = null)
    {
        options ??= GitHubFixtures.Options();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        return new GitHubCommitsClient(http, new StaticOptionsMonitor<GitHubOptions>(options), NullLogger<GitHubCommitsClient>.Instance);
    }

    [Fact]
    public async Task Sends_required_headers_and_maps_commits()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, GitHubFixtures.CommitsJson(10));
        var client = CreateClient(handler);

        var commits = await client.GetLatestCommitsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/company/integration-task-private-repo/commits?per_page=10", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization.Parameter);
        Assert.Equal("IntegrationTaskApp", request.Headers.UserAgent.ToString());

        Assert.Equal(10, commits.Count);
        var first = commits[0];
        Assert.Equal("01aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", first.Sha);
        Assert.Equal("Commit 1 subject\n\nBody line", first.Message);
        Assert.Equal("Author 1", first.Author);
        Assert.Equal("user1", first.AuthorLogin);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero), first.Date);
    }

    [Fact]
    public async Task Returns_at_most_ten_commits_even_if_api_returns_more()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, GitHubFixtures.CommitsJson(25)));
        Assert.Equal(10, (await client.GetLatestCommitsAsync()).Count);
    }

    [Fact]
    public async Task Omits_authorization_header_when_no_token_configured()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        await CreateClient(handler, GitHubFixtures.Options(token: null)).GetLatestCommitsAsync();
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitHubErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, GitHubErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, GitHubErrorKind.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, GitHubErrorKind.Upstream)]
    [InlineData(HttpStatusCode.BadGateway, GitHubErrorKind.Upstream)]
    public async Task Maps_http_errors_to_error_kinds(HttpStatusCode status, GitHubErrorKind expected)
    {
        var client = CreateClient(StubHttpMessageHandler.Json(status, """{"message":"Bad credentials"}"""));

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());

        Assert.Equal(expected, ex.Kind);
        Assert.Equal(status, ex.StatusCode);
        Assert.DoesNotContain("test-token", ex.Message); // never leak the token
    }

    [Fact]
    public async Task Forbidden_with_exhausted_rate_limit_is_reported_as_rate_limited()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.Forbidden, """{"message":"API rate limit exceeded"}""", r =>
        {
            r.Headers.Add("x-ratelimit-remaining", "0");
            r.Headers.Add("x-ratelimit-reset", "1735725600");
        });

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => CreateClient(handler).GetLatestCommitsAsync());

        Assert.Equal(GitHubErrorKind.RateLimited, ex.Kind);
        Assert.Contains("Resets at", ex.Message);
    }

    [Fact]
    public async Task Empty_repository_conflict_returns_empty_list()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.Conflict, """{"message":"Git Repository is empty."}"""));
        Assert.Empty(await client.GetLatestCommitsAsync());
    }

    [Fact]
    public async Task Slow_response_is_reported_as_timeout()
    {
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/"), Timeout = TimeSpan.FromMilliseconds(100) };
        var client = new GitHubCommitsClient(http, new StaticOptionsMonitor<GitHubOptions>(GitHubFixtures.Options()), NullLogger<GitHubCommitsClient>.Instance);

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());

        Assert.Equal(GitHubErrorKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_timeout()
    {
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateClient(handler).GetLatestCommitsAsync(cts.Token));
    }

    [Fact]
    public async Task Network_failure_is_reported_as_network_error()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("No such host is known."));
        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => CreateClient(handler).GetLatestCommitsAsync());
        Assert.Equal(GitHubErrorKind.Network, ex.Kind);
    }

    [Fact]
    public async Task Malformed_json_is_reported_as_upstream_error()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, "<html>not json</html>"));
        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetLatestCommitsAsync());
        Assert.Equal(GitHubErrorKind.Upstream, ex.Kind);
    }
}
