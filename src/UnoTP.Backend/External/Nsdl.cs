namespace UnoTP.Backend.External;

/// <summary>NSDL: whether it holds a PAN against a date of birth, and against a name.</summary>
public interface INsdlService
{
    Task<NsdlAnswer> VerifyAsync(string pan, string dob, string name, CancellationToken ct = default);
}

/// <summary>NSDL's answer: the PAN and date of birth as a pair, and the name against them.</summary>
public sealed record NsdlAnswer(bool PairOk, bool NameOk);

/// <summary>POST verify { pan, dob, name } → NsdlAnswer.</summary>
public sealed class NsdlClient(HttpClient http, IPartner partner) : ExternalClient(http, partner, "NSDL"), INsdlService
{
    public const string Name = "Nsdl";

    public Task<NsdlAnswer> VerifyAsync(string pan, string dob, string name, CancellationToken ct = default) =>
        Ask(() => Send<NsdlAnswer>(HttpMethod.Post, "verify", Body(new { pan, dob, name }), ct), ct);
}
