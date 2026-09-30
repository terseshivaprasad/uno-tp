using UnoTP.Backend;

namespace UnoTP.Api;

/// <summary>The partner a request is made for, as the web app sends it: X-Partner-Id, and X-Session-Id once a session is started.</summary>
public sealed class RequestPartner(IHttpContextAccessor accessor) : IPartner
{
    public string Id => accessor.HttpContext?.Request.Headers[ApiClient.PartnerHeader].ToString() ?? "";

    public string? SessionId => accessor.HttpContext?.Request.Headers[ApiClient.SessionHeader].ToString() is { Length: > 0 } s ? s : null;
}
