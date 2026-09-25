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
    public Task<Verification> ConfirmProofAsync(string proofType, OcrReading reading, string holderDob, CancellationToken ct = default) =>
        Task.FromResult(Issuers.TryGetValue(proofType, out var issuer)
            ? new Verification(reading.Address == MockScans.Address, issuer)
            : new Verification(false, ""));

    public Task<Verification> ConfirmAccountAsync(string account, string bank, CancellationToken ct = default) =>
        Task.FromResult(new Verification(bank == MockScans.Bank && account == MockScans.Account, bank));
}
