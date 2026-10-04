namespace UnoTP.Models;

/// <summary>
/// Aadhaar masking (UID masking): the copy with its Aadhaar number masked. Upload
/// Documents masks an Aadhaar after OCR has read it and the name has been matched,
/// and before any copy of it is filed or kept aside, so no copy with the whole
/// number is ever stored. The PAN-Aadhaar link is asked afterwards, with the number
/// OCR read off the copy before it was masked.
/// </summary>
public interface IMaskingService
{
    /// <summary>The masked copy, and the digits of the Aadhaar number it still shows.</summary>
    Task<MaskedAadhaar> MaskAsync(AadhaarToMask aadhaar, CancellationToken ct = default);
}

/// <summary>An Aadhaar copy to mask, with the application and holder it was uploaded for.</summary>
/// <param name="HolderType">01 the investor, 02 the second holder, 03 the third.</param>
/// <param name="Dob">dd-MM-yyyy.</param>
/// <param name="Consent">Whether the holder has consented to their Aadhaar being
/// processed. Nothing is sent without it.</param>
/// <param name="FormCode">The application form the copy belongs to.</param>
public sealed record AadhaarToMask(
    string AppNo, string Folio, string FormCode, string HolderType, string Pan, string Dob, UploadFile Copy, bool Consent);

/// <param name="Copy">The masked copy: the same file name, the masked image.</param>
/// <param name="Suffix">The last digits of the Aadhaar number, which the masked copy still shows; empty when none came back.</param>
public sealed record MaskedAadhaar(UploadFile Copy, string Suffix);
