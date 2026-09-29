using System.Security.Cryptography;
using System.Text;
using UnoTP.Backend.Shortener;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock shortener: a cmtpl.in link made of the long URL's hash, so the same
/// URL always gets the same short link and nothing is sent anywhere.
/// </summary>
public sealed class MockShortener : IShortLinkService
{
    public const string Host = "https://cmtpl.in/";

    public Task<string?> ShortenAsync(string longUrl, CancellationToken ct = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(longUrl));
        var code = Convert.ToBase64String(hash)[..8].Replace('+', 'a').Replace('/', 'b').Replace('=', 'c');
        return Task.FromResult<string?>(Host + code);
    }
}
