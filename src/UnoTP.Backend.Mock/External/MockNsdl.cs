using System.Text.RegularExpressions;
using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock NSDL. A PAN from the test data is answered exactly as the list says,
/// and the name has to match the one NSDL holds. Any other PAN still reaches every
/// outcome, decided by its last digit - 0 no such pair, 1 the name does not match,
/// anything else all three match - and any name typed in place of OCR's reading
/// is taken.
/// </summary>
public sealed partial class MockNsdl : INsdlService
{
    public Task<NsdlAnswer> VerifyAsync(string pan, string dob, string name, CancellationToken ct = default)
    {
        var known = MockInvestors.Pans.FirstOrDefault(p => p.Pan == pan);
        if (known is not null)
        {
            var pairOk = known.Dob == dob && known.Outcome != "pan-dob";
            return Task.FromResult(new NsdlAnswer(pairOk, pairOk && Normalise(name) == Normalise(known.Name)));
        }

        var outcome = pan.Length > 8 ? pan[8] switch { '0' => "pan-dob", '1' => "name", _ => "all" } : "pan-dob";
        // Off the list, the name NSDL holds differently is not the one OCR reads,
        // so only OCR's reading fails to match.
        var ocr = MockOcr.NameOn(pan);
        var pair = outcome != "pan-dob";
        return Task.FromResult(new NsdlAnswer(pair, pair && (outcome != "name" || name != ocr)));
    }

    // Names are compared the way a clerk reads them: case and spacing aside.
    private static string Normalise(string name) => Spaces().Replace(name.ToUpperInvariant(), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
