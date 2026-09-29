using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>Renew FD: a folio searched, the deposits it holds, and a word on each.</summary>
/// <param name="Deposits">Null until a folio is searched, or for a folio the register does not hold.</param>
/// <param name="Said">What the last post had to say, if anything.</param>
public sealed record RenewViewModel(string Folio, IReadOnlyList<HeldDeposit>? Deposits, ReferenceData Ref, AppConfig Config, string? Said)
{
    public bool Searched => Folio.Length > 0;

    public bool NotFound => Searched && Deposits is null;

    public static string StatusLabel(string status) => status switch
    {
        "running" => "Running", "maturing" => "Maturing", "matured" => "Matured", "renewed" => "Renewed", "closed" => "Paid out",
        _ => status,
    };

    public static string StatusTone(string status) => status switch
    {
        "maturing" or "matured" => "chip--warn", "renewed" => "chip--ok", _ => "chip--muted",
    };

    /// <summary>The remark beside a deposit: what renewing it means now, or why it cannot be.</summary>
    public static string Remark(HeldDeposit d) => d.Status switch
    {
        "maturing" => $"Matures on {Money.Day(d.MaturesOn)}: renewed now, the new deposit starts that day.",
        "matured" => $"Matured on {Money.Day(d.MaturesOn)}: the new deposit starts on the day the investor accepts.",
        _ => d.Why,
    };

    public string PayoutName(string code) => Ref.Payouts.FirstOrDefault(p => p.Code == code)?.Name ?? code;
}
