namespace UnoTP.Backend.External;

/// <summary>
/// An outside service could not answer: the copy was unreadable, too large or the
/// wrong kind of file, or the service itself was down. Either way nothing was
/// checked, so it costs no attempt.
/// </summary>
public sealed class ExternalServiceException(string service, string message, string? traceId = null, Exception? inner = null)
    : Exception(message, inner)
{
    /// <summary>Who could not answer, as the partner would name them.</summary>
    public string Service { get; } = service;

    /// <summary>The service's trace id, to quote when reporting it.</summary>
    public string? TraceId { get; } = traceId;
}
