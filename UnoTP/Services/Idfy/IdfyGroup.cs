using System.Security.Cryptography;
using System.Text;
using UnoTP.Infrastructure;

namespace UnoTP.Services.Idfy;

/// <summary>
/// The group id every IDfy request made for one application carries. IDfy files the
/// requests that share a group id together, so every check run for an application -
/// each document identified, read, verified with its issuer, the faces compared, the
/// PAN-Aadhaar link - can be found as one set.
///
/// IDfy takes a GUID, so the group id is made from the application number, and is
/// always the same for that number: nothing has to be kept to send it again, and it
/// can be worked out for any application (<see cref="Of"/>).
/// </summary>
public sealed class IdfyGroup(IHttpContextAccessor http)
{
    /// <summary>
    /// The group id for the application the request now being answered is about: the
    /// one in its address. Null where the address names no application; IDfy's
    /// wrapper then makes a group id of its own.
    /// </summary>
    public Guid? OfThisRequest()
    {
        var appNo = http.HttpContext?.CurrentApplication();
        if (string.IsNullOrWhiteSpace(appNo)) return null;
        return Of(appNo);
    }

    /// <summary>The group id of an application: the same GUID every time for the same number.</summary>
    public static Guid Of(string appNo)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("UNO_TP:" + appNo.Trim().ToUpperInvariant()));
        return new Guid(hash.AsSpan(0, 16));
    }
}
