using System.Text.Json;
using UnoTP.Models;

namespace UnoTP.Services;

/// <summary>
/// What the outside services' HTTP clients share: a service that is down, slow,
/// answering with an error or with something that cannot be read is an
/// <see cref="ExternalServiceException"/>, as it is from IDfy, so the pages deal
/// with every outside service the same way.
/// </summary>
public abstract class ExternalClient(HttpClient http, IPartner partner, string service) : ApiClient(http, partner)
{
    /// <summary>Calls an outside service, turning its failures into the errors the app shows.</summary>
    protected async Task<T> Ask<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException e)
        {
            throw new ExternalServiceException(service, Messages.OutsideServices.NotAnswering(service), inner: e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceException(service, Messages.OutsideServices.TookTooLong(service), inner: e);
        }
        catch (JsonException e)
        {
            throw new ExternalServiceException(service, Messages.OutsideServices.NotReadable(service), inner: e);
        }
    }
}
