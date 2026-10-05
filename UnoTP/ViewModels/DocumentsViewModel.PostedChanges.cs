using UnoTP.Infrastructure;
using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.ViewModels;

// What each post does - upload, CKYC, Proceed - and how every post keeps what the form carried.
public partial class DocumentsViewModel
{
    // ===== What a post does ===================================================

    /// <summary>Upload: the file posted for the slot whose button was pressed.
    /// Returns where on the page to come back to.</summary>
    public Task<string?> UploadAsync(IFormFileCollection files) => UploadAsync(Posted.Slot, files);

    /// <summary>Upload for a slot by its key, the investor's or a joint holder's.</summary>
    public async Task<string?> UploadAsync(string? key, IFormFileCollection files)
    {
        if (Locate(key) is not { } found) return null;
        Keep();
        if (!WithinLimit(found.Holder, found.Def.Key)) return "slot-" + key;
        try
        {
            await TakeAsync(found.Def, found.Holder, files.GetFile("file_" + key));
        }
        finally
        {
            progress.Done(UploadProgress.SessionOf(session), AppNo);
        }
        return "slot-" + key;
    }

    /// <summary>
    /// Whether one more try at a holder's document of one type, or at a check on it,
    /// is within this session's limit for this application and minute (see <see cref="DocumentLimiter"/>).
    /// Over it nothing is sent anywhere, and the page says so in a popup.
    /// </summary>
    public bool WithinLimit(DocHolder h, string documentType)
    {
        if (limiter.Allows(UploadProgress.SessionOf(session), AppNo, h.Code, documentType)) return true;
        FlashMessages().Popup = limiter.Said;
        return false;
    }

    /// <summary>
    /// Says what the post is doing now - which outside service it is waiting on - for
    /// the wait on the screen to show (see <see cref="UploadProgress"/>).
    /// </summary>
    private void Doing(string stage) => progress.Say(UploadProgress.SessionOf(session), AppNo, stage);

    // The slot a key is for, and whose it is.
    private (SlotDef Def, DocHolder Holder)? Locate(string? key)
    {
        foreach (var h in Holders)
        {
            foreach (var def in h.Joint ? HolderSlots : Slots)
            {
                if (h.Key(def.Key) == key) return (def, h);
            }
        }
        return null;
    }

    // CERSAI is searched for the investor's PAN and date of birth. What it gives of
    // a record it holds is little - the name on it and its CKYC reference number -
    // and the application goes on with those: the record stands as the investor's
    // KYC, so the proof of address and the photograph stop being asked for. The PAN
    // copy does not, nor does anything of a joint holder's, whose KYC is not fetched.
    // It cannot be taken back, and it closes the paper route.
    //
    // Only when CERSAI holds a record is the route taken; when it does not, or
    // cannot be asked, the documents are uploaded as before.
    public async Task<string?> CkycAsync()
    {
        Keep();
        var s = State;
        if (!CkycOn || !CkycPanVerified || s.Ckyc || s.AppType == Physical) return "docsRoute";
        if (!WithinLimit(Investor, "ckyc")) return "docsRoute";

        CkycSearchResult found;
        try
        {
            Doing("Searching CKYC for the investor\u2019s record\u2026");
            found = await ckyc.SearchAsync(new CkycSearch(AppNo, Investor.Code, Who.Pan, Who.Dob));
        }
        catch (ExternalServiceException e)
        {
            FlashMessages().Banner = $"{e.Message} The KYC was not fetched; upload the proof of address and the photograph, or try again.";
            FlashMessages().BannerIsError = true;
            return "docsRoute";
        }

        if (!found.Available)
        {
            var why = found.Why.Length > 0 ? $" ({found.Why})" : "";
            FlashMessages().Banner = $"CERSAI holds no CKYC record for this PAN and date of birth{why}. Upload the proof of address and the photograph instead.";
            return "docsRoute";
        }

        s.Ckyc = true;
        s.CkycReference = found.Reference;
        // The name on the record is the investor's until NSDL verifies one off the PAN copy.
        var ckycName = NewApplicationViewModel.NormaliseName(found.Name);
        if (s.Name.Length == 0 && ckycName.Length > 0) s.Name = ckycName;
        s.PoaType = "";
        TakeOffApplication("poa");
        TakeOffApplication("photo");
        s.Reads["poa"].Reset();
        ForgetMail(Investor);
        SetMail(Investor, false, "");
        var named = ckycName.Length > 0 ? $" in the name {ckycName}" : "";
        var reference = found.Reference.Length > 0 ? $", CKYC reference {found.Reference}" : "";
        FlashMessages().Banner = $"CERSAI holds a CKYC record for this PAN{named}{reference}. The application goes on with these; the proof of address and the photograph are not uploaded.";
        return "docsRoute";
    }

    // Proceed says what is missing where it is missing, and the page comes back
    // to the first of it.
    public string? Proceed()
    {
        Keep();
        var s = State;
        var flash = FlashMessages();
        void Need(bool ok, string key, string message)
        {
            if (ok) return;
            flash.Errors[key] = message;
            flash.Focus ??= key;
        }

        var mode = ModeOf(s.Sourcing);
        // A type chosen from the drop-down, when it is not set from the upload.
        if (!AutoProofType && View(PoaSlot).Used) Need(s.PoaType.Length > 0, "docsPoaType", "Choose the proof of address");
        if (!AutoProofType && View(MailSlot).Used) Need(s.MailPoaType.Length > 0, "docsMailType", "Choose the communication address proof");
        if (!IsRenewal) Need(s.PayMode.Length > 0, "docsPayMode", "Choose the payment mode");
        Need(s.Sourcing.Length > 0, "docsSourcing", "Choose the sourcing mode");
        if (mode is not null)
        {
            Need(s.SourceCode.Length > 0, "docsSourceCode", $"Enter the {mode.CodeLabel.ToLowerInvariant()}");
            if (SubRequired(mode)) Need(s.SubBroker.Length > 0, "docsSubBroker", "Enter the sub broker code");
        }
        Need(s.Category.Length > 0, "docsCategory", "No deposit category is set: choose the sourcing mode");
        if (IsEmployee(s.Category))
        {
            // A code this screen cannot put a name to is not a reason to stop:
            // the staff register is Operations' to check.
            Need(s.EmpCode.Length > 0, "docsEmployeeCode", "Enter the employee code");
            Need(s.EmpCompany.Length > 0, "docsEmployeeCompany", "Enter the employee company name");
            Need(s.EmpHolder.Length > 0, "docsEmployeeHolder", "Choose which holder is the employee");
            Need(s.EmpRelation.Length > 0, "docsEmployeeRelation", "Choose the relation with the holder");
            Need(s.EmpProofType.Length > 0, "docsEmployeeProofType", "Choose the employee proof");
        }
        if (s.AppType == Physical) Need(s.TypedFormNo.Length > 0, "docsFormNo", "Enter the physical form number");

        foreach (var slot in Slots.Select(View).Where(v => v.Missing))
        {
            flash.Errors[slot.Key] = "This document is required";
            flash.Focus ??= "slot-" + slot.Key;
        }
        // An investor with no folio goes on only once NSDL has verified their PAN.
        if (NsdlUnsettled(Investor))
        {
            flash.Errors["nsdl"] = NsdlNeed(Investor);
            flash.Focus ??= "read-nsdl";
        }
        // ...and, with an Aadhaar filed, once the PAN-Aadhaar link is confirmed...
        if (LinkApplies(Investor) && AadhaarFiled(Investor) && LinkNeed(Investor) is { } linkNeed)
        {
            flash.Errors[Investor.Key("link")] = linkNeed;
            flash.Focus ??= "read-" + Investor.Key("link");
        }
        // ...and once the name and date of birth on the proof of address match the PAN's.
        if (State.Docs.ContainsKey(Investor.Key("poa")) && DetailsNeed(Investor) is { } detailsNeed)
        {
            flash.Errors[Investor.Key("details")] = detailsNeed;
            flash.Focus ??= "read-" + Investor.Key("details");
        }

        if (flash.Errors.Count == 0)
        {
            Said = null;
            Complete = true;
        }
        return flash.Focus;
    }

    /// <summary>The messages this post will show on the page it returns to (made on first use).</summary>
    private Flash FlashMessages() => Said ??= new Flash();

    // What stops Proceed on the PAN-Aadhaar link, by where the link card stands; null once it is linked.
    private string? LinkNeed(DocHolder h)
    {
        var card = State.Reads.GetValueOrDefault(h.Key("pan"));
        if (card is null) return "The PAN-Aadhaar link must be confirmed before proceeding.";
        return card.State switch
        {
            "Linked with Aadhaar" => null,
            "Not asked" => null,
            "Not linked" => "The PAN is not linked with Aadhaar. The investor links it with the Income Tax department; the application cannot proceed until it is.",
            "Aadhaar number needed" => "Type the Aadhaar number in the row under the proofs of address, so the PAN-Aadhaar link can be asked.",
            "Link not checked" => "The PAN-Aadhaar link could not be checked. Upload the Aadhaar again, or type its number, to ask again.",
            _ => "The PAN-Aadhaar link must be confirmed before proceeding.",
        };
    }

    // What stops Proceed on the PAN-POA name and date of birth; null once they match.
    private string? DetailsNeed(DocHolder h)
    {
        var card = State.Reads.GetValueOrDefault(h.Key("details"));
        if (card is null) return "The name on the proof of address must be matched with the PAN's before proceeding.";
        if (card.Kind == "is-done") return null;
        if (card.Kind == "is-na") return null;
        return "The name and date of birth on the proof of address must match the PAN's before proceeding. Upload a clearer copy of the holder's own proof.";
    }

    /// <summary>
    /// What Proceed on Investor Information asks of each joint holder, the same way
    /// this step asks the investor: every document that is theirs to file. Returns
    /// where on the page to come back to; nothing missing, it returns null and says nothing.
    /// </summary>
    public string? ProceedJoint()
    {
        var flash = FlashMessages();
        void Need(string key, string message, string? focus = null)
        {
            flash.Errors[key] = message;
            flash.Focus ??= focus ?? key;
        }

        foreach (var h in JointHolders)
        {
            if (!AutoProofType && View(PoaSlot, h).Used && PoaTypeOf(h).Length == 0) Need(h.Key("poaType"), "Choose the proof of address");
            if (!AutoProofType && View(MailSlot, h).Used && MailTypeOf(h).Length == 0) Need(h.Key("mailType"), "Choose the communication address proof");
            foreach (var v in HolderSlots.Select(d => View(d, h)).Where(v => v.Missing))
                Need(v.Key, "This document is required", "slot-" + v.Key);
            if (NsdlUnsettled(h)) Need(h.Key("nsdl"), NsdlNeed(h), "read-" + h.Key("nsdl"));
        }
        if (flash.Errors.Count == 0 && flash.Banner is null) Said = null;
        return flash.Focus;
    }

    /// <summary>
    /// What each joint holder's card on Investor Information posted: where their post
    /// goes. Post going back to the permanent address takes the communication address
    /// proof off. A proof's type is not posted: it is what the copy is identified as.
    /// </summary>
    public void KeepJoint(IFormCollection form)
    {
        string Proof(string? type, string slot) => ProofsFor(slot).Contains(type) ? type! : "";
        foreach (var h in JointHolders)
        {
            if (!AutoProofType && form.TryGetValue(h.Key("poaType"), out var poa)) SetPoaType(h, Proof(poa, PoaSlot.Key));
            if (!MailCanDiffer(h) || !form.TryGetValue(h.Key("mailing"), out var mailing)) continue;
            var different = mailing == "different";
            if (!different) ForgetMail(h);
            var type = !different ? "" : !AutoProofType && form.TryGetValue(h.Key("mailType"), out var mail) ? Proof(mail, MailSlot.Key) : MailTypeOf(h);
            SetMail(h, different, type);
        }
    }

    // Asked before post goes back to the permanent address, when a copy is filed.
    public const string MailDropAsk =
        "Post will go to the permanent address, and the communication address proof uploaded will be removed. Switch to Same as Permanent?";

    // The proof of another address, and what it was read to say, taken off.
    private void ForgetMail(DocHolder h)
    {
        TakeOffApplication(h.Key("mail"));
        State.Reads[h.Key("mail")] = MailCard();
    }

    // ===== Keeping what was typed ===============================================

    // Every post carries the whole form, so every post keeps it, and applies what
    // each answer settles for the rest of the step. A field shut by the page is not
    // posted, so what it holds is the page's to say, not the post's.
    public void Keep()
    {
        var s = State;

        if (Posted.AppType is Digital or Physical) s.AppType = s.Ckyc ? Digital : Posted.AppType;
        // A digital application has no paper form, and the register will not take an
        // empty field for one: it is filed as 0000. A number typed for a paper
        // application is kept through a change of mind and put back.
        var typed = (Posted.FormNo ?? "").Trim();
        if (typed.Length > 0 && typed != DigitalFormNo) s.TypedFormNo = typed;
        else if (typed.Length == 0 && s.AppType == Physical && Posted.FormNo is not null) s.TypedFormNo = "";
        s.FormNo = s.AppType == Physical ? s.TypedFormNo : DigitalFormNo;
        // A slot the application has no use for keeps nothing.
        if (s.AppType != Physical) TakeOffApplication("form");

        // A type chosen from the drop-down, when it is not set from the upload.
        if (!AutoProofType && Posted.PoaType is not null) s.PoaType = ProofsFor(PoaSlot.Key).Contains(Posted.PoaType) ? Posted.PoaType : "";

        // Post going to the permanent address has nothing else to prove: the proof
        // of another address goes with the answer.
        if (Posted.Mailing is "same" or "different" && MailCanDiffer(Investor))
        {
            var different = Posted.Mailing == "different";
            if (!different) ForgetMail(Investor);
            var type = !different ? "" : !AutoProofType && Posted.MailType is not null ? (ProofsOfAddress.Contains(Posted.MailType) ? Posted.MailType : "") : s.MailPoaType;
            SetMail(Investor, different, type);
        }

        if (Posted.PayMode is not null)
        {
            s.PayMode = PaymentModes.Any(m => m.Name == Posted.PayMode) ? Posted.PayMode : "";
            if (DocumentOf(s.PayMode) is null)
            {
                TakeOffApplication("payment");
                s.Reads["payment"].Reset();
            }
        }

        KeepSourcing(s);

        if (IsEmployee(s.Category))
        {
            if (Posted.EmpCode is not null) s.EmpCode = Posted.EmpCode.Trim().ToUpperInvariant();
            if (Posted.EmpCompany is not null) s.EmpCompany = Posted.EmpCompany.Trim();
            if (Posted.EmpHolder is not null) s.EmpHolder = EmployeeHolders.Contains(Posted.EmpHolder) ? Posted.EmpHolder : "";
            if (Posted.EmpRelation is not null) s.EmpRelation = EmployeeRelations.Contains(Posted.EmpRelation) ? Posted.EmpRelation : "";
            // The primary holder is the employee themselves; anyone else cannot be.
            if (EmployeeIsPrimary) s.EmpRelation = SelfRelation;
            else if (s.EmpRelation == SelfRelation) s.EmpRelation = "";
            if (Posted.EmpProofType is not null) s.EmpProofType = EmployeeProofs.Contains(Posted.EmpProofType) ? Posted.EmpProofType : "";
        }
        else
        {
            // Every other category asks none of it, and anything typed goes with the block.
            (s.EmpCode, s.EmpCompany, s.EmpHolder, s.EmpRelation, s.EmpProofType) = ("", "", "", "", "");
            TakeOffApplication("empproof");
        }
    }

    // The old screen hangs the whole of Additional Details off the sourcing mode.
    // A change of mode is a fresh answer: whatever was typed under the last one
    // goes, the way the old screen empties both fields before it fills them.
    private void KeepSourcing(UploadState s)
    {
        // Nothing here is a partner's other than 1033 to choose; the sub broker is.
        if (!Chooses)
        {
            if (Posted.SubBroker is not null) s.SubBroker = Posted.SubBroker.Trim().ToUpperInvariant();
            Settle(s);
            return;
        }
        if (Posted.Sourcing is null) return;
        var mode = ModeOf(Posted.Sourcing);
        var fresh = Posted.Sourcing != s.Sourcing;
        s.Sourcing = mode?.Code ?? "";

        if (mode is null)
        {
            (s.SourceCode, s.SubBroker, s.Category) = ("", "", "");
            return;
        }

        var postedSource = fresh ? "" : (Posted.SourceCode ?? s.SourceCode).Trim().ToUpperInvariant();
        var postedSub = fresh ? "" : (Posted.SubBroker ?? s.SubBroker).Trim().ToUpperInvariant();

        s.SourceCode = mode.House.Length > 0 ? mode.House : postedSource;
        s.SubBroker = mode.Sub switch
        {
            SubField.House => mode.House,
            SubField.Shut => "",
            SubField.Free => postedSub,
            // Sourced by an employee: the application opens with the code of whoever
            // is at the keyboard, theirs to change under MFL-EX and not under MIBS.
            SubField.EmployeeShut => PartnerCode,
            _ => postedSub.Length > 0 ? postedSub : PartnerCode,
        };

        // What a deposit may be booked as belongs to the mode, and within it to the
        // investor's date of birth and gender: it is set, not chosen.
        s.Category = CategoryUnder(mode, s);
    }

    public static bool SubRequired(SourcingModeOption mode) => mode.Sub is SubField.Employee or SubField.EmployeeShut;
}
