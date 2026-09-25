namespace UnoTP.Backend;

/// <summary>
/// Who is asking. Every call to the backend is made on behalf of one partner,
/// and the backend only ever hands a partner their own applications. The web app
/// supplies this from the session; behind a real sign-in it is the signed-in
/// partner, and the HTTP client forwards it with every request.
/// </summary>
public interface IPartner
{
    string Id { get; }
}
