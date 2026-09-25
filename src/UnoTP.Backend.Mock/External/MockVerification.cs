using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock issuers and bank: each confirms only what it holds. A utility bill has
/// no issuer to ask.
/// </summary>
public sealed class MockVerification : IVerificationService
{
    private static readonly Dictionary<string, string> Issuers = new()
    {
        ["Aadhaar"] = "UIDAI",
        ["Passport"] = "Passport Seva",
        ["Driving Licence"] = "Sarathi",
        ["Voter ID"] = "the Election Commission",
    };

    // An issuer holds the proof when what was read off it is the address it holds.
    // Sarathi holds a licence valid six years on - or run out a year ago, for the
    // copy named "expired" - whatever date OCR read off it.
    public Task<Verification> ConfirmProofAsync(string proofType, OcrReading reading, string holderDob, CancellationToken ct = default)
    {
        if (!Issuers.TryGetValue(proofType, out var issuer)) return Task.FromResult(new Verification(false, ""));
        var found = reading.Address == MockScans.Address;
        if (proofType != "Driving Licence" || !found) return Task.FromResult(new Verification(found, issuer));
        var ranOut = DateTime.Today.AddYears(-1).ToString("dd-MM-yyyy");
        var until = reading.Expiry == ranOut ? ranOut : DateTime.Today.AddYears(6).ToString("dd-MM-yyyy");
        return Task.FromResult(new Verification(true, issuer, Expiry: until, Standing: "Active"));
    }

    public Task<Verification> ConfirmAccountAsync(string account, string bank, CancellationToken ct = default) =>
        Task.FromResult(new Verification(bank == MockScans.Bank && account == MockScans.Account, bank));
}
