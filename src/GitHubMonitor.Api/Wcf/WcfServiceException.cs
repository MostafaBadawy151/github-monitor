namespace GitHubMonitor.Api.Wcf;

public enum WcfFailureKind
{
    /// <summary>The WCF host is not reachable (not running, wrong URL, refused).</summary>
    Unavailable,

    /// <summary>The WCF call exceeded the configured timeout.</summary>
    Timeout,

    /// <summary>The service returned a SOAP fault or a protocol error.</summary>
    Fault
}

/// <summary>Transport-level failure talking to the WCF service, as opposed to a business error in the result.</summary>
public sealed class WcfServiceException(WcfFailureKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public WcfFailureKind Kind { get; } = kind;
}
