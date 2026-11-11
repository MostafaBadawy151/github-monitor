using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GitHubMonitor.Core.Configuration;
using GitHubMonitor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitHubMonitor.Core.GitHub;

/// <summary>
/// Typed <see cref="HttpClient"/> for <c>GET /repos/{owner}/{repo}/commits</c>.
/// Every failure becomes a <see cref="GitHubApiException"/> with a <see cref="GitHubErrorKind"/>,
/// so callers never have to interpret raw HTTP status codes.
/// </summary>
public sealed class GitHubCommitsClient : IGitHubCommitsClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<GitHubOptions> _options;
    private readonly ILogger<GitHubCommitsClient> _logger;

    public GitHubCommitsClient(HttpClient http, IOptionsMonitor<GitHubOptions> options, ILogger<GitHubCommitsClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CommitInfo>> GetLatestCommitsAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var path = $"repos/{Uri.EscapeDataString(options.Owner)}/{Uri.EscapeDataString(options.Repository)}/commits?per_page={options.CommitCount}";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        // Required by the task: "Authorization: Bearer {TOKEN}" and "User-Agent: IntegrationTaskApp".
        request.Headers.UserAgent.ParseAdd(options.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (options.HasToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token!.Trim());
        }
        else
        {
            _logger.LogWarning("No GitHub token configured; calling the API anonymously. A private repository will return 404.");
        }

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed. The caller did not cancel.
            _logger.LogWarning("GitHub request timed out after {Timeout}s", options.TimeoutSeconds);
            throw new GitHubApiException(GitHubErrorKind.Timeout,
                $"GitHub did not respond within {options.TimeoutSeconds} seconds.", inner: ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error calling GitHub");
            throw new GitHubApiException(GitHubErrorKind.Network, "Could not reach the GitHub API (network error).", inner: ex);
        }

        using (response)
        {
            _logger.LogInformation("GitHub {Method} {Path} -> {StatusCode} in {Elapsed} ms",
                request.Method, path, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // GitHub answers 409 "Git Repository is empty." for a repo with no commits.
                return Array.Empty<CommitInfo>();
            }

            if (!response.IsSuccessStatusCode)
            {
                throw await CreateErrorAsync(response, options, cancellationToken);
            }

            List<GitHubCommitResponse>? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<List<GitHubCommitResponse>>(cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new GitHubApiException(GitHubErrorKind.Upstream, "GitHub returned an unexpected response format.", response.StatusCode, ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new GitHubApiException(GitHubErrorKind.Timeout,
                    $"GitHub did not finish responding within {options.TimeoutSeconds} seconds.", inner: ex);
            }

            return (payload ?? [])
                .Take(options.CommitCount)
                .Select(Map)
                .ToList();
        }
    }

    private static CommitInfo Map(GitHubCommitResponse c)
    {
        var gitAuthor = c.Commit?.Author ?? c.Commit?.Committer;
        return new CommitInfo(
            Sha: c.Sha,
            Message: c.Commit?.Message?.Trim() ?? string.Empty,
            Author: gitAuthor?.Name ?? c.Author?.Login ?? "Unknown",
            AuthorLogin: c.Author?.Login,
            Date: gitAuthor?.Date ?? DateTimeOffset.MinValue,
            HtmlUrl: c.HtmlUrl);
    }

    private async Task<GitHubApiException> CreateErrorAsync(HttpResponseMessage response, GitHubOptions options, CancellationToken ct)
    {
        var gitHubMessage = await TryReadGitHubMessageAsync(response, ct);
        var status = response.StatusCode;
        var repo = $"{options.Owner}/{options.Repository}";

        var (kind, message) = status switch
        {
            HttpStatusCode.Unauthorized => (GitHubErrorKind.Unauthorized,
                "GitHub rejected the credentials (401). The token is missing, invalid, expired or revoked."),
            HttpStatusCode.TooManyRequests => (GitHubErrorKind.RateLimited,
                $"GitHub rate limit exceeded.{FormatReset(response)}"),
            HttpStatusCode.Forbidden when IsRateLimited(response) => (GitHubErrorKind.RateLimited,
                $"GitHub rate limit exceeded.{FormatReset(response)}"),
            HttpStatusCode.Forbidden => (GitHubErrorKind.Forbidden,
                "GitHub denied access (403). The token lacks permission to read this repository's contents."),
            HttpStatusCode.NotFound => (GitHubErrorKind.NotFound,
                $"Repository '{repo}' was not found, or the token does not have access to it (404)."),
            _ when (int)status >= 500 => (GitHubErrorKind.Upstream,
                $"GitHub is currently unavailable ({(int)status})."),
            _ => (GitHubErrorKind.Upstream, $"GitHub returned an unexpected status ({(int)status}).")
        };

        _logger.LogWarning("GitHub call failed: {Kind} ({StatusCode}). GitHub said: {GitHubMessage}",
            kind, (int)status, gitHubMessage ?? "<none>");

        return new GitHubApiException(kind, message, status);
    }

    private static bool IsRateLimited(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-ratelimit-remaining", out var values) && values.FirstOrDefault() == "0"
        || response.Headers.Contains("retry-after");

    private static string FormatReset(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-ratelimit-reset", out var values)
            && long.TryParse(values.FirstOrDefault(), out var epoch))
        {
            return $" Resets at {DateTimeOffset.FromUnixTimeSeconds(epoch):u}.";
        }
        return string.Empty;
    }

    private static async Task<string?> TryReadGitHubMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<GitHubErrorResponse>(ct);
            return error?.Message;
        }
        catch
        {
            return null; // Body is diagnostic only.
        }
    }
}
