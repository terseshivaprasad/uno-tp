namespace UnoTP.Models;

/// <summary>
/// Short links for the investor: the payment link an application is submitted with
/// is shortened before it goes to the backend, so the SMS carries a cmtpl.in link
/// rather than a long one. Nothing is retried - a repeated call could mint a second
/// short link - and a shortener that cannot answer does not stop a submission: the
/// application goes with the long link alone.
/// </summary>
public interface IShortLinkService
{
    /// <summary>
    /// The short link for <paramref name="longUrl"/>, or null when there is nothing
    /// to shorten with (no shortener configured). A shortener that is configured
    /// but cannot answer throws an ExternalServiceException.
    /// </summary>
    Task<string?> ShortenAsync(string longUrl, CancellationToken ct = default);
}
