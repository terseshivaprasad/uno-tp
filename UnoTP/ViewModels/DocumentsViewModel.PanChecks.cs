using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.ViewModels;

// A holder's PAN with NSDL, and its link with their Aadhaar.
public partial class DocumentsViewModel
{
    // A joint holder's PAN on the application but not verified with NSDL: the link
    // card says what it waits on, rather than still asking for the PAN copy.
    private void LinkWaitsOnNsdl(DocHolder h)
    {
        var card = State.Reads[h.Key("pan")];
        card.Lines = MaskPan(h.Who.Pan) + " · link with Aadhaar not asked yet";
        (card.State, card.Kind, card.From) = NsdlOf(h) == "failed"
            ? ("Not asked", "is-failed", "NSDL did not verify the PAN, so the link is not asked.")
            : ("Waiting on NSDL", "", $"Asked once NSDL verifies the PAN{(AadhaarOf(h).Length > 0 ? ", with the Aadhaar already read" : "")}.");
    }

    // An Aadhaar number, read or typed, kept for the link and asked with now - or,
    // while NSDL has not verified the PAN, once it does.
    private async Task AskLinkWithAsync(DocHolder h, string number, LogEntry entry)
    {
        session.SetString(AadhaarKey(h), number);
        if (PanOnApplication(h) && (!NsdlApplies(h) || NsdlSettled(h))) await RelinkPanAsync(h, entry);
        else if (PanOnApplication(h))
        {
            // The Aadhaar number is kept, and asked with once NSDL verifies the PAN.
            LinkWaitsOnNsdl(h);
            entry.Add("The PAN-Aadhaar link waits until NSDL verifies the PAN; this Aadhaar is asked with then.", "warn");
        }
    }

    // An Aadhaar filed whose whole number is not held: the link card says it waits
    // on the number, typed in the row under the proofs.
    private void LinkWaitsOnNumber(DocHolder h)
    {
        var card = State.Reads[h.Key("pan")];
        card.Lines = MaskPan(h.Who.Pan) + " · link with Aadhaar not asked yet";
        (card.State, card.Kind) = ("Aadhaar number needed", "is-failed");
        card.From = AadhaarLastFour(h).Length > 0
            ? "Only the last 4 digits of the Aadhaar number are held, as read off the copy. Type the first 8 in the row under the proofs of address, and the link is asked."
            : "The Aadhaar number was not read off the copy. Type it in the row under the proofs of address, and the link is asked.";
    }

    /// <summary>
    /// The last four digits of the Aadhaar filed for a holder, as OCR read them off
    /// the copy and as its record keeps them. Empty where nothing was read - OCR
    /// switched off - and the whole number is typed.
    /// </summary>
    public string AadhaarLastFour(DocHolder h)
    {
        var slot = PoaTypeOf(h) == "Aadhaar" ? PoaSlot : MailSlot;
        var kept = State.Docs.GetValueOrDefault(h.Key(slot.Key))?.Checks?.Number ?? "";
        return AadhaarNumbers.LastFour(kept);
    }

    /// <summary>
    /// Whether the Aadhaar number is asked for by hand: an Aadhaar is filed for a
    /// holder with no folio, and no whole number is held for the link - OCR read
    /// only its last 4 digits, or the session it was kept in has ended.
    /// </summary>
    public bool AsksAadhaarNumber(DocHolder h) =>
        LinkApplies(h) && AadhaarFiled(h) && AadhaarOf(h).Length == 0
        && State.Reads[h.Key("pan")] is { Kind: not "is-done" } card && card.State != "Not linked";

    /// <summary>The Aadhaar number row's field, the last four digits it is typed against, and what was wrong with the number last typed.</summary>
    public (string Field, string LastFour, string? Error) AadhaarNumberField(DocHolder h) =>
        (h.Key("aadhaarNo"), AadhaarLastFour(h), Shown?.Errors.GetValueOrDefault(h.Key("aadhaarNo")));

    /// <summary>
    /// The Aadhaar number, typed for the PAN-Aadhaar link: its first 8 digits, against
    /// the last 4 OCR read off the Aadhaar filed. The last 4 are the copy's own and are
    /// not typed, so a number that is not the copy's cannot be put to the link: where
    /// they were read wrong, the Aadhaar is uploaded again. Like a number read whole,
    /// it is held in the session only, never saved. Returns where on the page to come back to.
    /// </summary>
    public async Task<string?> AadhaarNumberAsync(DocHolder h, string? typed)
    {
        var key = h.Key("aadhaarNo");
        if (!AsksAadhaarNumber(h)) return "read-" + h.Key("link");
        if (!WithinLimit(h, "aadhaarNo")) return "row-" + key;
        var digits = new string((typed ?? "").Where(char.IsAsciiDigit).ToArray());
        var lastFour = AadhaarLastFour(h);
        var number = digits + lastFour;
        if (lastFour.Length > 0 && digits.Length != 8)
        {
            FlashMessages().Errors[key] = "Enter the first 8 digits of the Aadhaar number";
            return "row-" + key;
        }
        if (!AadhaarNumbers.IsValid(number))
        {
            // With no last 4 read (OCR switched off) the whole number is typed.
            FlashMessages().Errors[key] = number.Length != 12 ? "Enter the 12-digit Aadhaar number"
                : lastFour.Length > 0 ? $"That is not a valid Aadhaar number — check the first 8 digits against the card. If its last 4 are not {lastFour}, upload the Aadhaar again"
                : "That is not a valid Aadhaar number — check it against the card";
            return "row-" + key;
        }
        var entry = new LogEntry(Guid.NewGuid().ToString("n")[..8], "PAN–Aadhaar link · number typed", 1,
            DateTime.Now.ToString("HH:mm:ss"), "Aadhaar number typed: XXXX XXXX " + number[^4..]) { Holder = h.Joint ? h.Code : "" };
        State.Log.Insert(0, entry);
        await AskLinkWithAsync(h, number, entry);
        var card = State.Reads[h.Key("pan")];
        entry.End(card.Kind == "is-done" ? "Linked" : card.State, card.Kind == "is-done" ? "ok" : "warn");
        return "read-" + h.Key("link");
    }

    // An Aadhaar is taken only when the name and date of birth it reads are the
    // PAN's: the name as NSDL or the folio holds it - or, until NSDL verifies it,
    // as read off the PAN copy - allowing initials and a name left out, and the
    // date of birth exactly. Null when they match; otherwise why not.
    private async Task<string?> AadhaarMismatchAsync(DocHolder h, OcrReading reading)
    {
        var panName = h.Who.Name.Length > 0 ? h.Who.Name
            : State.Reads.TryGetValue(h.Key("panocr"), out var panRead) ? panRead.Lines : "";
        var name = reading.Name.Trim();
        if (panName.Length == 0) return "The PAN's name is not known yet, so the Aadhaar cannot be matched with it — file the PAN copy first";
        // The name match switched off is not a mismatch: the Aadhaar is taken, and Operations compare.
        Doing("Matching the name\u2026");
        var nameOk = name.Length > 0 && !(await names.MatchAsync(name, panName)).IsMismatch;
        var dobOk = reading.Dob.Length > 0 && reading.Dob == h.Who.Dob;
        return (nameOk, dobOk) switch
        {
            (true, true) => null,
            (false, false) => "The name and date of birth on the Aadhaar do not match the PAN's",
            (false, _) => name.Length == 0 ? "The name on the Aadhaar could not be read" : "The name on the Aadhaar does not match the PAN's",
            _ => reading.Dob.Length == 0 ? "The date of birth on the Aadhaar could not be read" : "The date of birth on the Aadhaar does not match the PAN's",
        };
    }

    // NSDL asked about a joint holder's PAN, date of birth and a name; verified,
    // the name is theirs from here on.
    private async Task NsdlAsync(DocHolder h, string name, LogEntry entry, bool typed)
    {
        // The PAN check switched off: NSDL is not asked. The PAN, date of birth and
        // name stand as the copy reads them, and the application goes on.
        if (!switches.IsOn(OutsideSwitches.PanCheck))
        {
            SetNsdl(h, name, NsdlNotAsked);
            if (name.Length > 0) TakeName(h, name);
            entry.Add($"The PAN check is {OutsideSwitches.Off}: NSDL is not asked, and the PAN is taken as the copy reads it; Operations check it.", "warn");
            return;
        }
        Doing("Checking the PAN with NSDL\u2026");
        var answer = await pan.VerifyAsync(new PanToVerify(AppNo, h.Code, h.Who.Pan, h.Who.Dob, name));
        var result = !answer.PairOk ? "failed" : answer.NameOk ? "verified" : "name";
        SetNsdl(h, name, result);
        if (result == "verified") TakeName(h, name);
        entry.Add(answer.PairOk ? "NSDL holds the PAN against the date of birth." : "NSDL holds no such PAN and date of birth.", answer.PairOk ? "ok" : "bad");
        if (answer.PairOk)
            entry.Add(answer.NameOk ? $"NSDL holds it against {name}{(typed ? ", as typed" : "")}." : $"NSDL does not hold it against {name}{(typed ? ", as typed" : "")}.", answer.NameOk ? "ok" : "warn");
    }

    /// <summary>
    /// NSDL asked again about a PAN copy already filed, without the copy being
    /// uploaded - and identified and read - again: only where NSDL held the PAN
    /// against another name, with the name printed on the card typed and put to it.
    /// Where it held no such PAN and date of birth, or could not answer, there is no
    /// retry: the PAN copy is uploaded again. Returns where on the page to come back to.
    /// </summary>
    public async Task<string?> RetryNsdlAsync(DocHolder h, string? typed)
    {
        var key = h.Key("nsdl");
        if (!NsdlApplies(h) || NsdlOf(h) != "name" || State.Docs.GetValueOrDefault(h.Key("pan")) is not { } doc) return "read-" + key;
        if (!WithinLimit(h, "nsdl")) return "read-" + key;

        var name = NewApplicationViewModel.NormaliseName(typed);
        if (name.Length < 3 || !InvestorViewModel.IsName(name))
        {
            FlashMessages().Errors[key] = name.Length < 3 ? "Enter the name as printed on the PAN" : "Enter the name as printed on the PAN: letters only";
            return "read-" + key;
        }
        var entry = new LogEntry(Guid.NewGuid().ToString("n")[..8], $"{PanSlot.Label} · NSDL again", 1,
            DateTime.Now.ToString("HH:mm:ss"), $"Name typed from the PAN card: {name}") { Holder = h.Code };
        State.Log.Insert(0, entry);
        try
        {
            await NsdlAsync(h, name, entry, typed: true);
        }
        catch (ExternalServiceException e)
        {
            entry.Add($"{e.Service} could not answer: {e.Message}", "bad");
            entry.End("Not checked", "bad");
            FlashMessages().Errors[key] = e.Message;
            return "read-" + key;
        }
        h = Again(h);
        if (NsdlOf(h) != "verified")
        {
            // The copy says where NSDL stands now, which may not be where it stood before.
            var (stillCheck, stillKind) = NotVerifiedCheck(h);
            State.Docs[h.Key("pan")] = doc with { Check = stillCheck, CheckKind = stillKind };
            LinkWaitsOnNsdl(h);
            entry.End("Not verified", "warn");
            FlashMessages().Errors[key] = NsdlOf(h) == "failed"
                ? $"NSDL holds no record of PAN {MaskPan(h.Who.Pan)} against the date of birth searched"
                : $"NSDL does not hold PAN {MaskPan(h.Who.Pan)} against that name";
            return "read-" + key;
        }
        // Verified: the link can be asked now, and the copy says what came of it.
        var link = await LinkAsync(h, entry);
        var (check, kind) = PanCheck(h, link);
        State.Docs[h.Key("pan")] = doc with { Check = check, CheckKind = kind };
        entry.End("Verified", "ok");
        return "read-" + key;
    }

    private bool PanOnApplication(DocHolder h) => h.Who.PanFiled || State.Docs.ContainsKey(h.Key("pan"));

    // A PAN copy the holder needs and has not filed yet: not one the folio holds,
    // nor one a holder on a folio may leave out.
    private bool PanWanted(DocHolder h) => !PanOnApplication(h) && View(PanSlot, h) is { Used: true, Optional: false };

    // The Aadhaar arrived after the PAN: the link is asked now, and the PAN's card
    // and what its copy says both follow the answer.
    private async Task RelinkPanAsync(DocHolder h, LogEntry entry)
    {
        var link = await LinkAsync(h, entry);
        if (State.Docs.GetValueOrDefault(h.Key("pan")) is { Before: false } doc)
        {
            var (check, kind) = PanCheck(h, link);
            State.Docs[h.Key("pan")] = doc with { Check = check, CheckKind = kind };
        }
    }

    // Null when the link check could not answer. The PAN or the Aadhaar that
    // prompted it is filed all the same: the link is only ever a note on the PAN.
    private async Task<PanAadhaarLink?> LinkAsync(DocHolder h, LogEntry entry)
    {
        var card = State.Reads[h.Key("pan")];
        var pan = MaskPan(h.Who.Pan);
        PanAadhaarLink link;
        try
        {
            Doing("Checking the PAN\u2013Aadhaar link\u2026");
            link = await panLink.CheckAsync(h.Who.Pan, AadhaarOf(h));
        }
        catch (ExternalServiceException e)
        {
            card.Lines = pan + " · link with Aadhaar not checked";
            (card.State, card.Kind) = ("Link not checked", "");
            card.From = $"The PAN-Aadhaar link could not be asked just now: {e.Message}";
            entry.Add($"{e.Service} could not answer the PAN-Aadhaar link: {e.Message}", "warn");
            return null;
        }
        switch (link)
        {
            case PanAadhaarLink.Linked:
                card.Lines = pan + " · linked with Aadhaar";
                (card.State, card.Kind) = ("Linked with Aadhaar", "is-done");
                card.From = $"Confirmed with {PanAuthority} against the Aadhaar number on this application, read off the copy or typed.";
                entry.Add($"{Capitalize(PanAuthority)} holds an Aadhaar against this PAN.", "ok");
                break;
            case PanAadhaarLink.NotLinked:
                card.Lines = pan + " · not linked with Aadhaar";
                (card.State, card.Kind) = ("Not linked", "is-failed");
                card.From = $"{Capitalize(PanAuthority)} holds no Aadhaar against this PAN. The application cannot proceed until the investor links it.";
                entry.Add("No Aadhaar against this PAN.", "warn");
                break;
            case PanAadhaarLink.NotAsked:
                card.Lines = pan + " · link with Aadhaar not asked";
                (card.State, card.Kind) = ("Not asked", "is-na");
                card.From = $"The PAN-Aadhaar link is {OutsideSwitches.Off}. The application goes on; Operations check the link.";
                entry.Add($"PAN-Aadhaar link not asked: {OutsideSwitches.Off}.", "warn");
                break;
            case PanAadhaarLink.NeedsAadhaar when AadhaarFiled(h):
                // An Aadhaar is filed, but its number was not read: it is typed.
                LinkWaitsOnNumber(h);
                entry.Add("No Aadhaar number held for this application; it is typed for the PAN-Aadhaar link.", "warn");
                break;
            default:
                card.Lines = pan + " · link with Aadhaar not asked yet";
                (card.State, card.Kind) = ("Waiting on an Aadhaar", "");
                card.From = LinkWaitsShort;
                entry.Add("No Aadhaar number read on this application yet, so the PAN-Aadhaar link is not asked.", "warn");
                break;
        }
        return link;
    }

    private static (string, string) PanCheck(DocHolder h, PanAadhaarLink? link) => (MaskPan(h.Who.Pan), link) switch
    {
        (var pan, PanAadhaarLink.Linked) => ($"Identified, read as PAN {pan} and confirmed with {PanAuthority}: an Aadhaar is held against it.", "ok"),
        (var pan, PanAadhaarLink.NotLinked) => ($"Read as PAN {pan}, but {PanAuthority} holds no Aadhaar against it. The copy is filed and the deposit can be booked — TDS runs at the higher rate until the {(h.Joint ? "holder" : "investor")} links it.", "warn"),
        (var pan, PanAadhaarLink.NeedsAadhaar) => ($"Identified and read as PAN {pan}. {LinkWaits}", "warn"),
        (var pan, _) => ($"Identified and read as PAN {pan}. The PAN-Aadhaar link could not be asked just now; it is asked again when an Aadhaar is next filed.", "warn"),
    };
}
