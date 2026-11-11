using System.Diagnostics;
using System.ServiceModel;
using GitHubMonitor.Api.Configuration;
using Microsoft.Extensions.Options;

namespace GitHubMonitor.Api.Wcf;

/// <summary>
/// Registered as a singleton. The <see cref="ChannelFactory{TChannel}"/> is expensive to build
/// and thread-safe, so it is created once. Channels are cheap and are created per call,
/// then closed (or aborted on failure) per the standard WCF client pattern.
/// </summary>
public sealed class WcfGitHubCommitServiceClient : IGitHubCommitServiceClient, IDisposable
{
    private readonly ChannelFactory<IGitHubCommitServiceChannel> _factory;
    private readonly ILogger<WcfGitHubCommitServiceClient> _logger;
    private readonly string _endpoint;

    public WcfGitHubCommitServiceClient(IOptions<WcfClientOptions> options, ILogger<WcfGitHubCommitServiceClient> logger)
    {
        _logger = logger;
        var o = options.Value;
        _endpoint = o.EndpointUrl;

        var binding = new BasicHttpBinding(
            o.EndpointUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase)
                ? BasicHttpSecurityMode.Transport
                : BasicHttpSecurityMode.None)
        {
            OpenTimeout = TimeSpan.FromSeconds(o.OpenTimeoutSeconds),
            SendTimeout = TimeSpan.FromSeconds(o.SendTimeoutSeconds),
            ReceiveTimeout = TimeSpan.FromSeconds(o.SendTimeoutSeconds),
            CloseTimeout = TimeSpan.FromSeconds(5),
            MaxReceivedMessageSize = 1024 * 1024
        };

        _factory = new ChannelFactory<IGitHubCommitServiceChannel>(binding, new EndpointAddress(o.EndpointUrl));
    }

    public Task<CommitQueryResult> GetLiveCommitsAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync("GetLiveCommits", c => c.GetLiveCommitsAsync(), cancellationToken);

    public Task<CommitQueryResult> GetScheduledCommitsAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync("GetScheduledCommits", c => c.GetScheduledCommitsAsync(), cancellationToken);

    private async Task<CommitQueryResult> InvokeAsync(
        string operation,
        Func<IGitHubCommitServiceChannel, Task<CommitQueryResult>> call,
        CancellationToken cancellationToken)
    {
        var channel = _factory.CreateChannel();
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            // WCF client calls take no CancellationToken. WaitAsync stops waiting when the
            // HTTP caller disconnects, and the abort in the finally block tears the channel down.
            var result = await call(channel).WaitAsync(cancellationToken);
            channel.Close();
            success = true;

            _logger.LogInformation("WCF {Operation} completed in {Elapsed} ms (Success={Success}, ErrorCode={ErrorCode})",
                operation, stopwatch.ElapsedMilliseconds, result.Success, result.ErrorCode);
            return result;
        }
        catch (EndpointNotFoundException ex)
        {
            _logger.LogError(ex, "WCF service unreachable at {Endpoint}", _endpoint);
            throw new WcfServiceException(WcfFailureKind.Unavailable,
                "The GitHub WCF service is unavailable. Make sure the WCF host is running.", ex);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "WCF {Operation} timed out after {Elapsed} ms", operation, stopwatch.ElapsedMilliseconds);
            throw new WcfServiceException(WcfFailureKind.Timeout, "The GitHub WCF service did not respond in time.", ex);
        }
        catch (FaultException ex)
        {
            _logger.LogError(ex, "WCF {Operation} returned a fault", operation);
            throw new WcfServiceException(WcfFailureKind.Fault, "The GitHub WCF service reported an error.", ex);
        }
        catch (CommunicationException ex)
        {
            _logger.LogError(ex, "WCF {Operation} communication failure", operation);
            throw new WcfServiceException(WcfFailureKind.Unavailable,
                "Could not communicate with the GitHub WCF service. Make sure the WCF host is running.", ex);
        }
        finally
        {
            if (!success)
            {
                channel.Abort();
            }
        }
    }

    public void Dispose()
    {
        try { _factory.Close(); }
        catch { _factory.Abort(); }
    }
}
