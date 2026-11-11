using System.Net;
using GitHubMonitor.Api.Services;
using GitHubMonitor.Api.Wcf;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GitHubMonitor.Tests.Api;

public class GitHubCommitsServiceTests
{
    private readonly IGitHubCommitServiceClient _wcf = Substitute.For<IGitHubCommitServiceClient>();

    private GitHubCommitsService CreateService() => new(_wcf, NullLogger<GitHubCommitsService>.Instance);

    [Fact]
    public async Task Maps_wcf_result_to_dto()
    {
        _wcf.GetLiveCommitsAsync(Arg.Any<CancellationToken>()).Returns(new CommitQueryResult
        {
            Success = true,
            Source = "Live",
            Message = "ok",
            LastSyncTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc),
            Commits =
            [
                new CommitData { Sha = "0123456789abcdef", Message = "Subject line\r\n\r\nBody", Author = "Jane", Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), Source = "Live" }
            ]
        });

        var result = await CreateService().GetLiveAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var commit = Assert.Single(result.Data!.Commits);
        Assert.Equal("0123456", commit.ShortSha);
        Assert.Equal("Subject line", commit.MessageHeadline);
        Assert.Equal(TimeSpan.Zero, commit.Date.Offset);
        Assert.Equal("Live", commit.Source);
        Assert.Equal(1, result.Data.Count);
    }

    [Theory]
    [InlineData("Unauthorized", HttpStatusCode.BadGateway)]
    [InlineData("Forbidden", HttpStatusCode.BadGateway)]
    [InlineData("NotFound", HttpStatusCode.BadGateway)]
    [InlineData("Timeout", HttpStatusCode.GatewayTimeout)]
    [InlineData("RateLimited", HttpStatusCode.ServiceUnavailable)]
    [InlineData("InternalError", HttpStatusCode.InternalServerError)]
    public async Task Maps_business_error_codes_to_http_status(string errorCode, HttpStatusCode expected)
    {
        _wcf.GetLiveCommitsAsync(Arg.Any<CancellationToken>())
            .Returns(new CommitQueryResult { Success = false, ErrorCode = errorCode, Message = "GitHub said no" });

        var result = await CreateService().GetLiveAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expected, result.StatusCode);
        Assert.Equal("GitHub said no", result.Message);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(WcfFailureKind.Unavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(WcfFailureKind.Timeout, HttpStatusCode.GatewayTimeout)]
    [InlineData(WcfFailureKind.Fault, HttpStatusCode.BadGateway)]
    public async Task Maps_wcf_transport_failures_to_http_status(WcfFailureKind kind, HttpStatusCode expected)
    {
        _wcf.GetScheduledCommitsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new WcfServiceException(kind, "wcf problem"));

        var result = await CreateService().GetScheduledAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expected, result.StatusCode);
        Assert.Equal("wcf problem", result.Message);
    }
}
