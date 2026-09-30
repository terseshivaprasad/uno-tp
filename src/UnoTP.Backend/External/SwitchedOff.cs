namespace UnoTP.Backend.External;

// What each outside service answers while it is switched off (OutsideSwitches): an
// answer the pages carry on with, marked as not asked wherever a page shows it.

/// <summary>Identification off: the copy is taken as the type it was handed in as; a proof of address is then chosen from the list, not detected.</summary>
public sealed class OffDocumentIdentifier : IDocumentIdentifier
{
    public Task<Identification> IdentifyAsync(DocumentKind expected, string type, UploadFile file, CancellationToken ct = default) =>
        Task.FromResult(new Identification(true, Type: type));
}

/// <summary>OCR off: nothing is read off the copy; the holder's own PAN, date of birth and name stand in, so the copy is taken as theirs and Operations check it.</summary>
public sealed class OffOcr : IOcrService
{
    public Task<OcrReading> ReadAsync(DocumentKind kind, string type, UploadFile file, OcrSubject subject, bool consent, CancellationToken ct = default) =>
        Task.FromResult(new OcrReading(Pan: subject.Pan, Name: subject.Name, Dob: subject.Dob));
}

/// <summary>Issuer verification off: nothing is confirmed; the pages already carry a check that was not asked.</summary>
public sealed class OffVerification : IVerificationService
{
    public Task<Verification> ConfirmProofAsync(string proofType, OcrReading reading, string holderDob, CancellationToken ct = default) =>
        Task.FromResult(new Verification(false, "", NotAsked: "the check is " + OutsideSwitches.Off));

    public Task<Verification> ConfirmAccountAsync(string account, string bank, CancellationToken ct = default) =>
        Task.FromResult(new Verification(false, "", NotAsked: "the check is " + OutsideSwitches.Off));
}

/// <summary>PAN-Aadhaar link off: not asked, and the application goes on.</summary>
public sealed class OffPanAadhaarLink : IPanAadhaarLinkService
{
    public Task<PanAadhaarLink> CheckAsync(string pan, string aadhaarNumber, CancellationToken ct = default) =>
        Task.FromResult(PanAadhaarLink.NotAsked);
}

/// <summary>Face match off: not sure, for Operations to compare.</summary>
public sealed class OffFaceMatch : IFaceMatchService
{
    public Task<FaceMatch> CompareAsync(UploadFile panCopy, UploadFile proof, CancellationToken ct = default) =>
        Task.FromResult(new FaceMatch(false, 0, Unsure: "the face match is " + OutsideSwitches.Off));
}

/// <summary>Name match off: not asked; the proof is filed and Operations compare the names.</summary>
public sealed class OffNameMatch : INameMatchService
{
    public Task<NameMatchResult> MatchAsync(string name, string other, CancellationToken ct = default) =>
        Task.FromResult(new NameMatchResult(NameMatchOutcome.NotAsked));
}

/// <summary>Name screening off: every holder goes on, and the KYC row says the check was skipped.</summary>
public sealed class OffNameScreening : INameScreeningService
{
    public Task<NameScreeningResult> ScreenAsync(NameScreeningRequest request, CancellationToken ct = default) =>
        Task.FromResult(new NameScreeningResult(true, OutsideSwitches.Off));
}
