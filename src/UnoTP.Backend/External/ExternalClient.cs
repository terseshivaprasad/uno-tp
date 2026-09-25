using System.Text.Json;

namespace UnoTP.Backend.External;

/// <summary>
/// What the outside services' HTTP clients share: a service that is down, slow,
/// answering with an error or with something that cannot be read is an
/// <see cref="ExternalServiceException"/>, as it is from IDfy, so the pages deal
/// with every outside service the same way.
/// </summary>
public abstract class ExternalClient(HttpClient http, IPartner partner, string service) : ApiClient(http, partner)
{
    protected async Task<T> Ask<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException e)
        {
            throw new ExternalServiceException(service, $"{service} is not answering. Try again in a while.", inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceException(service, $"{service} took too long to answer. Try again in a while.", inner: e);
        }
        catch (JsonException e)
        {
            throw new ExternalServiceException(service, $"{service} answered with something that could not be read. Try again in a while.", inner: e);
        }
    }
}
