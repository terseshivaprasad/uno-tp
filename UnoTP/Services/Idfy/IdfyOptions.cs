namespace UnoTP.Services.Idfy;

/// <summary>
/// Idfy.Api, from the "Idfy" section of appsettings: its base path under the
/// gateway (<see cref="BackendOptions.BaseUrl"/>), and the path of each call under
/// that. It answers document identification, OCR (a cheque's too), verification
/// with the issuer, the PAN-Aadhaar link and face match.
/// </summary>
public sealed class IdfyOptions : IApiAddress
{
    public const string Section = "Idfy";

    /// <summary>The API's own address, https://{host}/ ; blank while it is behind the gateway (Backend:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The API's base path under the gateway. Every path below is relative to it.</summary>
    public string BasePath { get; set; } = "";

    /// <summary>Whether a copy is the document it is handed in as, or which document it is.</summary>
    public string ValidateDocumentPath { get; set; } = "documents/validate";

    /// <summary>OCR of a PAN card.</summary>
    public string ExtractPanPath { get; set; } = "pan/extract";

    /// <summary>OCR of an Aadhaar.</summary>
    public string ExtractAadhaarPath { get; set; } = "aadhaar/extract";

    /// <summary>OCR of a driving licence.</summary>
    public string ExtractDrivingLicencePath { get; set; } = "driving-license/extract";

    /// <summary>OCR of a passport.</summary>
    public string ExtractPassportPath { get; set; } = "passport/extract";

    /// <summary>OCR of a voter ID.</summary>
    public string ExtractVoterIdPath { get; set; } = "voter-id/extract";

    /// <summary>OCR of a cheque.</summary>
    public string ExtractChequePath { get; set; } = "cheque/extract";

    /// <summary>A driving licence checked with its issuer.</summary>
    public string VerifyDrivingLicencePath { get; set; } = "driving-license/verify/sync";

    /// <summary>A passport checked with its issuer.</summary>
    public string VerifyPassportPath { get; set; } = "passport/verify/sync";

    /// <summary>A voter ID checked with its issuer.</summary>
    public string VerifyVoterIdPath { get; set; } = "voter-id/verify/sync";

    /// <summary>Whether an Aadhaar is linked to a PAN.</summary>
    public string VerifyPanAadhaarLinkPath { get; set; } = "pan-aadhaar-link/verify/sync";

    /// <summary>Whether the faces on two copies are the same person.</summary>
    public string CompareFacesPath { get; set; } = "face/compare";
}
