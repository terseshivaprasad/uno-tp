namespace UnoTP.Backend.External;

/// <summary>
/// The name screening service: whether a holder may invest online, by name, PAN and
/// date of birth (sanctions and watch lists). Asked of every holder on Investor
/// Information before the application goes on; one not allowed invests offline, at
/// a branch.
/// </summary>
public interface INameScreeningService
{
    Task<NameScreeningResult> ScreenAsync(NameScreeningRequest request, CancellationToken ct = default);
}

/// <param name="Dob">dd-MM-yyyy.</param>
public sealed record NameScreeningRequest(string Name, string Pan, string Dob);

/// <param name="Allowed">Whether the holder may invest online.</param>
/// <param name="Reference">The service's reference for the check, kept on the application's KYC row.</param>
public sealed record NameScreeningResult(bool Allowed, string Reference = "");

/// <summary>POST screen { name, pan, dob } → { status: "allowed" or "blocked", reference }.</summary>
public sealed class NameScreeningClient(HttpClient http, IPartner partner)
    : ExternalClient(http, partner, "Name screening"), INameScreeningService
{
    public const string Name = "NameScreening";

    public Task<NameScreeningResult> ScreenAsync(NameScreeningRequest request, CancellationToken ct = default) =>
        Ask(async () =>
        {
            using var message = Request(HttpMethod.Post, "screen", JsonBody(new { name = request.Name, pan = request.Pan, dob = request.Dob }));
            using var response = await SendAsync(message, ct);
            response.EnsureSuccessStatusCode();
            var answer = await Read<Answer>(response, ct);
            var allowed = string.Equals(answer.Status, "allowed", StringComparison.OrdinalIgnoreCase);
            return new NameScreeningResult(allowed, answer.Reference ?? "");
        }, ct);

    private sealed record Answer(string? Status, string? Reference);
}
