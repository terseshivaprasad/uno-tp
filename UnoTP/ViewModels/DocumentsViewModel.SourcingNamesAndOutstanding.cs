using UnoTP.Models;

namespace UnoTP.ViewModels;

// What the page says about the rest: the name behind a sourcing code, and what Proceed still asks for.
public partial class DocumentsViewModel
{
    // ===== What the page says about the rest ===================================

    /// <summary>The name a register holds against a code field, or why there is none.</summary>
    public (string Text, bool Found)? Resolved(SourcingModeOption? mode, bool sub)
    {
        var value = sub ? State.SubBroker : State.SourceCode;
        if (mode is null || value.Length == 0) return null;
        var house = sub ? (mode.Sub == SubField.House ? mode.House : "") : mode.House;
        if (house.Length > 0 && value.Equals(house, StringComparison.OrdinalIgnoreCase))
            return ("Stands for the sourcing mode itself — filled here, not typed.", false);
        var found = sub ? subParty : sourceParty;
        return found is not null
            ? ($"{(sub ? "Sub Broker Name" : mode.NameLabel)} — {found.Name}", true)
            : ("No name against this code here. You can still proceed — Operations check it before the deposit is booked.", false);
    }

    /// <summary>The register a code field is searched against as it is typed - "brokers" or "staff" - or null where it is not searched.</summary>
    public static string? RegisterName(SourcingModeOption? mode, bool sub) =>
        mode is null || mode.Search != (sub ? "sub" : "source") ? null
        : mode.Register switch { Register.Brokers => "brokers", Register.Employees => "staff", _ => null };

    /// <summary>What Proceed would ask for, in the order it asks, so the footer can say what is next.</summary>
    // A PAN copy filed for a holder NSDL has not verified.
    private bool NsdlUnsettled(DocHolder h) => NsdlApplies(h) && State.Docs.ContainsKey(h.Key("pan")) && !NsdlSettled(h);

    private string NsdlNeed(DocHolder h) => NsdlOf(h) switch
    {
        "failed" => $"NSDL holds no such PAN and date of birth. Retry the NSDL check if both are right. If not: {NsdlFailedNext(h)}",
        Unanswered => "NSDL could not be asked about the PAN. Retry the NSDL check",
        _ => "Type the name as printed on the PAN, and ask NSDL again",
    };

    // The bar's words for a missing document, and the card that holds it.
    private static readonly Dictionary<string, string> SlotOfNeed = new()
    {
        ["the application form"] = "form", ["the PAN copy"] = "pan", ["the proof of address"] = "poa",
        ["the photograph"] = "photo", ["the communication address proof"] = "mail",
        ["the instrument copy"] = "payment", ["the employee proof"] = "empproof",
    };

    /// <summary>The card that holds the next thing to do, when that is a document; null otherwise.</summary>
    public string? NextSlot() => Outstanding().FirstOrDefault() is { } first ? SlotOfNeed.GetValueOrDefault(first) : null;

    public List<string> Outstanding()
    {
        var s = State;
        var left = new List<string>();
        var mode = ModeOf(s.Sourcing);
        bool Missing(SlotDef d) => View(d).Missing;

        if (Missing(FormSlot)) left.Add("the application form");
        if (Missing(PanSlot)) left.Add("the PAN copy");
        if (NsdlUnsettled(Investor)) left.Add("the PAN verified with NSDL");
        if (!AutoProofType && View(PoaSlot).Used && s.PoaType.Length == 0) left.Add("the proof of address type");
        if (Missing(PoaSlot)) left.Add("the proof of address");
        if (Missing(PhotoSlot)) left.Add("the photograph");
        if (!AutoProofType && View(MailSlot).Used && s.MailPoaType.Length == 0) left.Add("the communication address proof type");
        if (Missing(MailSlot)) left.Add("the communication address proof");
        if (!IsRenewal && s.PayMode.Length == 0) left.Add("the payment mode");
        if (Missing(PaymentSlot)) left.Add("the instrument copy");
        if (mode is null) left.Add("the sourcing mode");
        else
        {
            if (s.SourceCode.Length == 0) left.Add("the " + mode.CodeLabel.ToLowerInvariant());
            if (SubRequired(mode) && s.SubBroker.Length == 0) left.Add("the sub broker code");
        }
        if (s.Category.Length == 0) left.Add("the deposit category");
        if (IsEmployee(s.Category))
        {
            if (s.EmpCode.Length == 0) left.Add("the employee code");
            if (s.EmpCompany.Length == 0) left.Add("the employee company");
            if (s.EmpHolder.Length == 0) left.Add("the employee holder");
            if (s.EmpRelation.Length == 0) left.Add("the relation with the holder");
            if (s.EmpProofType.Length == 0) left.Add("the employee proof type");
            if (Missing(EmpProofSlot)) left.Add("the employee proof");
        }
        if (s.AppType == Physical && s.TypedFormNo.Length == 0) left.Add("the form number");
        return left;
    }
}
