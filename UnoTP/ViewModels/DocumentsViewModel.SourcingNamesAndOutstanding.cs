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
        "failed" => Messages.UploadDocuments.NsdlFailedUploadAgain(NsdlFailedNext(h)),
        Unanswered => Messages.UploadDocuments.NsdlNotAsked,
        _ => Messages.UploadDocuments.TypeNameAskAgain,
    };

    // The bar's words for a missing document, and the card that holds it.
    private static readonly Dictionary<string, string> SlotOfNeed = new()
    {
        [Messages.UploadDocuments.MissingForm] = "form", [Messages.UploadDocuments.MissingPan] = "pan", [Messages.UploadDocuments.MissingPoa] = "poa",
        [Messages.UploadDocuments.MissingPhoto] = "photo", [Messages.UploadDocuments.MissingMail] = "mail",
        [Messages.UploadDocuments.MissingInstrument] = "payment", [Messages.UploadDocuments.MissingEmployeeProof] = "empproof",
    };

    /// <summary>The card that holds the next thing to do, when that is a document; null otherwise.</summary>
    public string? NextSlot() => Outstanding().FirstOrDefault() is { } first ? SlotOfNeed.GetValueOrDefault(first) : null;

    public List<string> Outstanding()
    {
        var s = State;
        var left = new List<string>();
        var mode = ModeOf(s.Sourcing);
        bool Missing(SlotDef d) => View(d).Missing;

        if (Missing(FormSlot)) left.Add(Messages.UploadDocuments.MissingForm);
        if (Missing(PanSlot)) left.Add(Messages.UploadDocuments.MissingPan);
        if (NsdlUnsettled(Investor)) left.Add(Messages.UploadDocuments.MissingNsdl);
        if (!AutoProofType && View(PoaSlot).Used && s.PoaType.Length == 0) left.Add(Messages.UploadDocuments.MissingPoaType);
        if (Missing(PoaSlot)) left.Add(Messages.UploadDocuments.MissingPoa);
        if (Missing(PhotoSlot)) left.Add(Messages.UploadDocuments.MissingPhoto);
        if (!AutoProofType && View(MailSlot).Used && s.MailPoaType.Length == 0) left.Add(Messages.UploadDocuments.MissingMailType);
        if (Missing(MailSlot)) left.Add(Messages.UploadDocuments.MissingMail);
        if (!IsRenewal && s.PayMode.Length == 0) left.Add(Messages.UploadDocuments.MissingPayMode);
        if (Missing(PaymentSlot)) left.Add(Messages.UploadDocuments.MissingInstrument);
        if (mode is null) left.Add(Messages.UploadDocuments.MissingSourcing);
        else
        {
            if (s.SourceCode.Length == 0) left.Add("the " + mode.CodeLabel.ToLowerInvariant());
            if (SubRequired(mode) && s.SubBroker.Length == 0) left.Add(Messages.UploadDocuments.MissingSubBroker);
        }
        if (s.Category.Length == 0) left.Add(Messages.UploadDocuments.MissingCategory);
        if (IsEmployee(s.Category))
        {
            if (s.EmpCode.Length == 0) left.Add(Messages.UploadDocuments.MissingEmployeeCode);
            if (s.EmpCompany.Length == 0) left.Add(Messages.UploadDocuments.MissingEmployeeCompany);
            if (s.EmpHolder.Length == 0) left.Add(Messages.UploadDocuments.MissingEmployeeHolder);
            if (s.EmpRelation.Length == 0) left.Add(Messages.UploadDocuments.MissingEmployeeRelation);
            if (s.EmpProofType.Length == 0) left.Add(Messages.UploadDocuments.MissingEmployeeProofType);
            if (Missing(EmpProofSlot)) left.Add(Messages.UploadDocuments.MissingEmployeeProof);
        }
        if (s.AppType == Physical && s.TypedFormNo.Length == 0) left.Add(Messages.UploadDocuments.MissingFormNo);
        return left;
    }
}
