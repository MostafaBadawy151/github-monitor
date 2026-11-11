using System.Net;
using System.Text;
using GitHubMonitor.Core.Configuration;
using Microsoft.Extensions.Options;

namespace GitHubMonitor.Tests.TestSupport;

/// <summary>HttpMessageHandler that hands each request to a delegate and records it.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler Json(HttpStatusCode status, string json, Action<HttpResponseMessage>? configure = null) =>
        new((_, _) =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            configure?.Invoke(response);
            return Task.FromResult(response);
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return responder(request, cancellationToken);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal static class GitHubFixtures
{
    public static GitHubOptions Options(string? token = "test-token", int timeoutSeconds = 15) => new()
    {
        ApiBaseUrl = "https://api.github.com/",
        Owner = "company",
        Repository = "integration-task-private-repo",
        Token = token,
        UserAgent = "IntegrationTaskApp",
        CommitCount = 10,
        TimeoutSeconds = timeoutSeconds
    };

    /// <summary>Builds a GitHub "list commits" JSON array with <paramref name="count"/> commits.</summary>
    public static string CommitsJson(int count) =>
        "[" + string.Join(",", Enumerable.Range(1, count).Select(i => $$"""
        {
          "sha": "{{i:D2}}aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "html_url": "https://github.com/company/integration-task-private-repo/commit/{{i}}",
          "commit": {
            "message": "Commit {{i}} subject\n\nBody line",
            "author": { "name": "Author {{i}}", "email": "a{{i}}@example.com", "date": "2025-01-{{i:D2}}T10:00:00Z" }
          },
          "author": { "login": "user{{i}}" }
        }
        """)) + "]";
}
