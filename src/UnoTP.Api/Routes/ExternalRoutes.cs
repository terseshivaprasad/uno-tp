using UnoTP.Backend.External;

namespace UnoTP.Api.Routes;

/// <summary>
/// The outside services the web app reaches through its backend, at
/// external/{service}/ (docs/backend-api.md, Outside services). Each answers from
/// its mock until the backend proxies the real one.
/// </summary>
public static class ExternalRoutes
{
    private sealed record DecryptBody(string Value);

    private sealed record NsdlBody(string Pan, string Dob, string Name);

    private sealed record ProofBody(string ProofType, OcrReading Reading, string HolderDob);

    private sealed record AccountBody(string Account, string Bank);

    private sealed record LinkBody(string Pan, string AadhaarNumber);

    public static void MapExternal(this IEndpointRouteBuilder app)
    {
        var x = app.MapGroup("external");

        x.MapPost("decrypt/decrypt", async (DecryptBody body, IDecryptionService decryption, CancellationToken ct) =>
            await decryption.DecryptAsync(body.Value, ct) is { } plain ? Results.Ok(new { value = plain }) : Results.BadRequest());

        x.MapPost("nsdl/verify", async (NsdlBody body, INsdlService nsdl, CancellationToken ct) =>
            Results.Ok(await nsdl.VerifyAsync(body.Pan, body.Dob, body.Name, ct)));

        x.MapPost("identify/identify", async (HttpRequest req, IDocumentIdentifier identifier, CancellationToken ct) =>
        {
            if (await ApplicationRoutes.FileOf(req, "file", ct) is not { } file || !Enum.TryParse<DocumentKind>(req.Form["expected"], out var expected))
                return Results.BadRequest();
            return Results.Ok(await identifier.IdentifyAsync(expected, req.Form["type"].ToString(), file, ct));
        });

        x.MapPost("masking/check", async (HttpRequest req, IMaskingService masking, CancellationToken ct) =>
            await ApplicationRoutes.FileOf(req, "file", ct) is { } file
                ? Results.Ok(new { masked = await masking.IsMaskedAsync(file, req.Form["consent"] == "true", ct) })
                : Results.BadRequest());

        x.MapPost("ocr/read", async (HttpRequest req, IOcrService ocr, CancellationToken ct) =>
        {
            if (await ApplicationRoutes.FileOf(req, "file", ct) is not { } file || !Enum.TryParse<DocumentKind>(req.Form["kind"], out var kind))
                return Results.BadRequest();
            var f = req.Form;
            return Results.Ok(await ocr.ReadAsync(kind, f["type"].ToString(), file,
                new OcrSubject(f["pan"].ToString(), f["dob"].ToString(), f["name"].ToString()), f["consent"] == "true", ct));
        });

        x.MapPost("verification/proof", async (ProofBody body, IVerificationService verification, CancellationToken ct) =>
            Results.Ok(await verification.ConfirmProofAsync(body.ProofType, body.Reading, body.HolderDob, ct)));

        x.MapPost("verification/account", async (AccountBody body, IVerificationService verification, CancellationToken ct) =>
            Results.Ok(await verification.ConfirmAccountAsync(body.Account, body.Bank, ct)));

        x.MapPost("panaadhaarlink/check", async (LinkBody body, IPanAadhaarLinkService link, CancellationToken ct) =>
            Results.Ok(new { link = await link.CheckAsync(body.Pan, body.AadhaarNumber, ct) }));

        x.MapPost("facematch/compare", async (HttpRequest req, IFaceMatchService faces, CancellationToken ct) =>
            await ApplicationRoutes.FileOf(req, "pan", ct) is { } pan && await ApplicationRoutes.FileOf(req, "proof", ct) is { } proof
                ? Results.Ok(await faces.CompareAsync(pan, proof, ct))
                : Results.BadRequest());
    }
}
