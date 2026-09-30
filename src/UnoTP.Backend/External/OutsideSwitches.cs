using Microsoft.Extensions.Configuration;

namespace UnoTP.Backend.External;

/// <summary>
/// Which outside services are switched on (Backend:Switches:{name}, true unless set
/// false). One switched off is not called: its check is skipped, the page goes on,
/// and the history says the check was not asked, for Operations. Masking, NSDL and
/// the portal's decryption cannot be switched off: a switch on them is ignored.
/// </summary>
public sealed class OutsideSwitches(IConfiguration config)
{
    /// <summary>The services a switch may turn off.</summary>
    public static readonly string[] Switchable =
    [
        DocumentIdentifierClient.Name, OcrClient.Name, VerificationClient.Name,
        PanAadhaarLinkClient.Name, FaceMatchClient.Name, NameScreeningClient.Name, NameMatchClient.Name,
    ];

    public bool IsOn(string name)
    {
        if (!Switchable.Contains(name)) return true;
        var setting = config[$"Backend:Switches:{name}"];
        if (string.IsNullOrWhiteSpace(setting)) return true;
        return !string.Equals(setting.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What the history says of a check that was not asked because its service is off.</summary>
    public const string Off = "switched off";
}
