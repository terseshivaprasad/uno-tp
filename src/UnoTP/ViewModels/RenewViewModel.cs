using System.Globalization;
using System.Text.RegularExpressions;
using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>What Renewal - Search was asked: by PAN and date of birth, or by folio.</summary>
public sealed record RenewSearch(string By, string Pan, string Dd, string Mm, string Yyyy, string Folio)
{
    public bool ByFolio => By == "folio";

    /// <summary>The date of birth as the backend takes it, dd-MM-yyyy, or null for no real date.</summary>
    public string? Dob =>
        int.TryParse(Dd, out var d) && int.TryParse(Mm, out var m) && int.TryParse(Yyyy, out var y)
        && y >= 1900 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m)
        && new DateTime(y, m, d) <= DateTime.Today
            ? new DateTime(y, m, d).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) : null;

    /// <summary>What is wrong with the search, by field, or nothing.</summary>
    public Dictionary<string, string> Problems()
    {
        var problems = new Dictionary<string, string>();
        if (ByFolio)
        {
            if (Folio.Length == 0) problems["Folio"] = "Enter the folio number";
        }
        else
        {
            if (Pan.Length == 0) problems["Pan"] = "Enter the PAN";
            else if (!Regex.IsMatch(Pan, "^[A-Z]{5}[0-9]{4}[A-Z]$")) problems["Pan"] = "Enter a valid PAN, like ABCDE1234F";
            if (Dd.Length + Mm.Length + Yyyy.Length == 0) problems["Dob"] = "Enter the date of birth";
            else if (Dob is null) problems["Dob"] = "Enter a real date, DD/MM/YYYY";
        }
        return problems;
    }
}

/// <summary>Renewal - Search: what was asked, the deposits found, and a word on each.</summary>
/// <param name="Deposits">Null until something is searched, or when nothing is on record for it.</param>
/// <param name="Said">What the last post had to say, if anything.</param>
public sealed record RenewViewModel(RenewSearch Search, bool Searched, IReadOnlyDictionary<string, string> Problems, IReadOnlyList<HeldDeposit>? Deposits,
    ReferenceData Ref, AppConfig Config, string? Said)
{
    public bool NotFound => Searched && Problems.Count == 0 && Deposits is null;

    public string? Problem(string field) => Problems.GetValueOrDefault(field);

    /// <summary>What was searched for, in words.</summary>
    public string Asked => Search.ByFolio ? $"folio {Search.Folio}" : $"PAN {Search.Pan} and date of birth {Search.Dd.PadLeft(2, '0')}/{Search.Mm.PadLeft(2, '0')}/{Search.Yyyy}";

    public static string StatusLabel(string status) => status switch
    {
        "running" => "Running", "due" => "Due for renewal", "late" => "Entry closed", "matured" => "Matured", "renewed" => "Renewed", "closed" => "Paid out",
        _ => status,
    };

    public static string StatusTone(string status) => status switch
    {
        "due" => "chip--warn", "renewed" => "chip--ok", _ => "chip--muted",
    };

    /// <summary>The remark beside a deposit: what renewing it means now, or why it cannot be entered.</summary>
    public string Remark(HeldDeposit d)
    {
        if (d.Status != "due") return d.Why;
        var until = d.AutoRenewal ? Config.RenewUntilDaysAutoRenewal : Config.RenewUntilDays;
        return $"Matures on {Money.Day(d.MaturesOn)}{(d.AutoRenewal ? " · tagged for auto renewal" : "")}. "
            + $"Entry open till {Money.Day(d.MaturesOn.AddDays(-until))}; renewed at the rate prevailing on maturity.";
    }

    public string PayoutName(string code) => Ref.Payouts.FirstOrDefault(p => p.Code == code)?.Name ?? code;
}
