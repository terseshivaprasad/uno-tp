using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.ViewModels;

// The cards that show what was read off a document: the name and date of birth, the face match, the PAN, the address.
public partial class DocumentsViewModel
{
    // ----- The PAN-POA name and date of birth ---------------------------------------
    // The name and date of birth OCR reads off the proof of address are held up to
    // the holder's own - the name NSDL or the folio gives, and the date of birth
    // searched on. For now it is only said: a proof is filed whatever it reads.

    /// <summary>What the proof of address was read to say of the holder, or that it waits on one.</summary>
    public ReadCard DetailsOf(DocHolder h) =>
        State.Docs.ContainsKey(h.Key("poa")) && State.Reads.TryGetValue(h.Key("details"), out var card) ? card
        : new ReadCard("Not yet compared", "Compared once the proof of address is filed.",
            "The name and date of birth read off the proof are matched with the holder's.", "is-na");

    /// <summary>How a name read off a document stands against the holder's:
    /// "match", "partial" (initials, or a name left out) or "mismatch".</summary>
    // The name and date of birth on a proof of address against the PAN's: the name
    // by the name match service, the date of birth exactly. What it found stands in
    // the "PAN-POA name & DOB" card and the history.
    private async Task DetailsAsync(DocHolder h, OcrReading reading, string type, LogEntry entry)
    {
        var named = Printed(type);
        // With OCR switched off nothing was read to compare: not asked, and Operations check.
        if (!switches.IsOn(OutsideSwitches.Ocr))
        {
            State.Reads[h.Key("details")] = new ReadCard("Not asked", $"OCR is {OutsideSwitches.Off}, so nothing was read off the {named} to compare.",
                "The proof is filed; Operations compare the name and date of birth.", "is-na");
            entry.Add($"Name and date of birth on the {named} not compared: OCR is {OutsideSwitches.Off}.", "warn");
            return;
        }
        string nameSays, dobSays;
        bool nameOk, dobOk, dobNa = false;
        var name = reading.Name.Trim();
        if (h.Who.Name.Length == 0) (nameSays, nameOk) = ("name not compared: the holder's name is not verified yet", false);
        else if (name.Length == 0) (nameSays, nameOk) = ("name could not be read", false);
        else
        {
            Doing("Matching the name\u2026");
            var match = (await names.MatchAsync(name, h.Who.Name)).Outcome;
            if (match == NameMatchOutcome.NotAsked)
            {
                State.Reads[h.Key("details")] = new ReadCard("Not asked", $"The name match is {OutsideSwitches.Off}, so the name on the {named} was not compared.",
                    "The proof is filed; Operations compare the name and date of birth.", "is-na");
                entry.Add($"Name on the {named} not compared: the name match is {OutsideSwitches.Off}.", "warn");
                return;
            }
            (nameSays, nameOk) = match switch
            {
                NameMatchOutcome.Match => ("name matches", true),
                NameMatchOutcome.Partial => ($"name partly matches ({name})", false),
                _ => ($"name does not match ({name})", false),
            };
        }
        if (!HasPhoto(type)) (dobSays, dobOk, dobNa) = ($"no date of birth on a {named}", true, true);
        else if (reading.Dob.Length == 0) (dobSays, dobOk) = ("date of birth could not be read", false);
        else if (reading.Dob == h.Who.Dob) (dobSays, dobOk) = ("date of birth matches", true);
        else (dobSays, dobOk) = ($"date of birth does not match ({MaskDate(reading.Dob)})", false);

        var bad = nameSays.Contains("does not match") || dobSays.Contains("does not match");
        var (state, kind) = nameOk && dobOk ? (dobNa ? "Name matches" : "Details match", "is-done")
            : bad ? ("Do not match", "is-failed")
            : ("Partly match", "is-failed");
        State.Reads[h.Key("details")] = new ReadCard(state, $"{Capitalize(nameSays)} · {dobSays}.",
            kind == "is-done" ? $"Read off the {named} and matched with the holder's name{(dobNa ? "" : " and date of birth")}."
                : $"Read off the {named}. The proof is filed; Operations check the details.", kind);
        entry.Add($"Name and date of birth on the {named}: {nameSays}; {dobSays}.", kind == "is-done" ? "ok" : bad ? "bad" : "warn");
    }

    // ----- The PAN-POA face match -------------------------------------------------
    // The photograph on the PAN copy is compared with the one on the proof of
    // address once both are filed. For now it is only said: a proof is filed
    // whatever the answer.

    /// <summary>What the face match said, or that it waits on the copies.</summary>
    public ReadCard FaceOf(DocHolder h) =>
        State.Docs.ContainsKey(h.Key("poa")) && State.Reads.TryGetValue(h.Key("face"), out var card) ? card
        : new ReadCard("Not yet compared", "Compared once the PAN copy and the proof of address are both filed.",
            "The photograph on the PAN copy is matched with the one on the proof of address.", "is-na");

    /// <summary>
    /// What a filed PAN copy was read to say - the name, the PAN and the date of
    /// birth - under where NSDL stands on it, for its box. A copy filed before this
    /// step shows the holder as the application has them.
    /// </summary>
    private ReadCard PanReadOf(DocHolder h, StoredDoc doc)
    {
        var ocr = State.Reads.GetValueOrDefault(h.Key("panocr"));
        var name = ocr?.Lines is { Length: > 0 } n ? n : h.Who.Name;
        var pan = ocr?.Number is { Length: > 0 } p ? p : h.Who.Pan;
        var dob = ocr?.Dob is { Length: > 0 } d ? d : h.Who.Dob;
        var (state, kind) = doc.Before ? ("On the application", "is-done")
            : !NsdlApplies(h) ? ("PAN & DOB match", "is-done")
            : NsdlOf(h) switch
            {
                "verified" => ("Verified with NSDL", "is-done"),
                "name" => ("Name not matched", "is-failed"),
                "failed" => ("Not verified", "is-failed"),
                _ => ("Not yet checked", "is-na"),
            };
        return new ReadCard(state, name.Length > 0 ? name : "Name not read", "", kind)
        {
            Number = $"PAN {MaskPan(pan)}" + (dob.Length > 0 ? $" · DOB {MaskDate(dob)}" : ""),
        };
    }

    /// <summary>The number a proof carries, labelled as it is shown: an Aadhaar by its
    /// last four digits only, anything else in full.</summary>
    public string ProofNumber(string type, OcrReading reading)
    {
        var number = (reading.Number.Length > 0 ? reading.Number : reading.IdNumber).Trim();
        if (number.Length == 0) return "";
        return type switch
        {
            "Aadhaar" => number.Replace(" ", "") is { Length: >= 4 } digits ? $"Aadhaar XXXX XXXX {digits[^4..]}" : "",
            "Driving Licence" => "DL " + number,
            _ when !HasPhoto(type) => "",
            _ => $"{type} {number}",
        };
    }

    /// <summary>
    /// An address and the PIN code it ends with, apart: the address is cut to fit
    /// its box, and the PIN has to show whatever the length. Empty PIN when none
    /// can be found.
    /// </summary>
    public static (string Body, string Pin) SplitPin(string address)
    {
        var found = System.Text.RegularExpressions.Regex.Matches(address, @"(?<!\d)(\d{3})\s?(\d{3})(?!\d)");
        if (found.Count == 0) return (address, "");
        var last = found[^1];
        var body = (address[..last.Index] + address[(last.Index + last.Length)..]).Trim().TrimEnd(',', '-', ' ');
        return (body, last.Groups[1].Value + last.Groups[2].Value);
    }

    /// <summary>Whether a proof's expiry date has passed.</summary>
    public static bool Expired(string expiry) =>
        DateTime.TryParseExact(expiry, "dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var until) && until < DateTime.Today;

    // A proof named as it is printed in a sentence: the Aadhaar, the Voter ID.
    // A proof with no photograph is a paper, named in lower case; an ID keeps its capitals.
    private string Printed(string type) => type.Length == 0 ? "proof" : HasPhoto(type) ? type : type.ToLowerInvariant();

    // A proof whose type carries no photograph has no face to compare.
    private bool HasPhoto(string proofType) => Ref.ProofsOfAddress.Any(p => p.Type == proofType && p.HasPhoto);

    private async Task FaceAsync(DocHolder h, LogEntry entry)
    {
        var key = h.Key("face");
        var type = PoaTypeOf(h);
        void FlashMessages(string state, string lines, string from, string kind) => State.Reads[key] = new ReadCard(state, lines, from, kind);

        if (!State.Docs.ContainsKey(h.Key("poa"))) return;
        // A comparison made before, with a copy since replaced, no longer stands.
        KeepFace(h, "pan", null);
        KeepFace(h, "poa", null);
        if (!HasPhoto(type))
        {
            FlashMessages("Not applicable", $"A {type.ToLowerInvariant()} carries no photograph.", "There is no face on the proof to compare with the PAN copy.", "is-na");
            return;
        }
        // Only a PAN copy filed on this application can be sent: one the folio holds,
        // or one filed before this step, is not here to compare with.
        var pan = State.Docs.GetValueOrDefault(h.Key("pan")) is { Before: false }
            ? await documents.CopyAsync(AppNo, FiledUnder(PanSlot, h), PanSlot.Key) : null;
        var proof = await documents.CopyAsync(AppNo, FiledUnder(PoaSlot, h), PoaSlot.Key);
        if (pan is null || proof is null)
        {
            FlashMessages("Not compared", "No PAN copy was filed on this application to compare with.",
                "The PAN copy is on the folio or was filed before this step, so Operations compare the faces.", "is-na");
            entry.Add("Face match not asked: no PAN copy on this application to compare with.", "warn");
            return;
        }
        FaceMatch answer;
        try
        {
            Doing("Matching the photograph\u2026");
            answer = await faces.CompareAsync(pan, proof);
        }
        catch (ExternalServiceException e)
        {
            FlashMessages("Could not answer", "The face match could not be asked.", $"{e.Message} The proof is filed; Operations compare the faces.", "is-failed");
            entry.Add($"Face match could not answer: {e.Message}", "warn");
            return;
        }
        if (switches.IsOn(OutsideSwitches.FaceMatch))
        {
            KeepFace(h, "pan", new FaceCheck(answer.PanFace, answer.Score));
            KeepFace(h, "poa", new FaceCheck(answer.ProofFace, answer.Score));
        }
        var named = Printed(type);
        if (answer.Unsure is { } why)
        {
            FlashMessages("Not sure", $"Score {answer.Score} of 100", $"{Capitalize(why)}. The proof is filed; Operations compare the faces.", "is-failed");
            entry.Add($"Face match not sure: {why}.", "warn");
        }
        else if (answer.Matched)
        {
            FlashMessages("Faces match", $"Score {answer.Score} of 100", $"The photograph on the PAN copy and on the {named} are the same person.", "is-done");
            entry.Add($"Face match: the PAN copy and the {named} are the same person (score {answer.Score}).", "ok");
        }
        else
        {
            FlashMessages("Faces do not match", $"Score {answer.Score} of 100", $"The photograph on the {named} is not the one on the PAN copy. The proof is filed; Operations look into it.", "is-failed");
            entry.Add($"Face match: the {named} is not the person on the PAN copy (score {answer.Score}).", "bad");
        }
    }

    // The face comparison, kept with each of the two copies it was made of.
    private void KeepFace(DocHolder h, string slot, FaceCheck? face)
    {
        var key = h.Key(slot);
        if (State.Docs.GetValueOrDefault(key) is not { Checks: not null } doc) return;
        State.Docs[key] = doc with { Checks = doc.Checks with { Face = face } };
    }

    /// <summary>
    /// What a holder's PAN was checked with: NSDL, for a joint holder, and the
    /// PAN-Aadhaar link. These take two copies, or a name typed again, so they stand
    /// as cards; an address stands in the box of the proof it was read off.
    /// </summary>
    public IReadOnlyList<ReadItem> PanReadsOf(DocHolder h) =>
        [.. ReadsOf(h).Where(i => i.Kind is not ("Permanent address" or "Communication address"))];

    /// <summary>
    /// What the payment instrument was read to say, beside its box. A mode settled
    /// electronically carries no instrument, and the card says so.
    /// </summary>
    public ReadItem PaymentRead() => View(PaymentSlot).Used
        ? new("Account", State.Reads["payment"], "payment")
        : new("Account", NotRead("Not applicable", "No instrument is copied for this payment mode.",
            "An account is read only off a cheque."));

    // What NSDL said about a joint holder's PAN, and - when it holds the PAN
    // against another name - where the name printed on the card is typed to ask again.
    private ReadItem NsdlCard(DocHolder h)
    {
        var pan = MaskPan(h.Who.Pan);
        if (!NsdlApplies(h))
            return new("PAN – NSDL", NotRead("Not applicable", $"{pan} · on the folio", "A holder on a folio is not asked about with NSDL again."));
        var (nsdlState, nsdlName) = (NsdlOf(h), NsdlNameOf(h));
        var key = h.Key("nsdl");
        return nsdlState switch
        {
            "verified" => new("PAN – NSDL", new ReadCard("Verified with NSDL", $"{pan} · {h.Who.Name}", "The PAN, date of birth and name read off the PAN copy all match.", "is-done"), key),
            "name" => new("PAN – NSDL", new ReadCard("Name not matched", $"Put to NSDL: {nsdlName}",
                "NSDL holds the PAN and date of birth, but not against that name. Type the name exactly as printed on the PAN card, and NSDL is asked again.", "is-failed"), key,
                new NameRetry(h.Key("nsdlName"), nsdlName, Shown?.Errors.GetValueOrDefault(key))),
            "failed" => new("PAN – NSDL", new ReadCard("Not verified", $"No record of {pan} against {MaskDate(h.Who.Dob)}",
                $"NSDL holds no such PAN and date of birth, so this {(h.Joint ? "holder" : "application")} cannot go on. {NsdlFailedNext(h)}", "is-failed"), key),
            _ => new("PAN – NSDL", new ReadCard("Not yet checked", "Checked once the PAN copy is filed above.",
                "OCR reads the name off the PAN copy, and NSDL is asked whether it holds the PAN, the date of birth and that name."), key),
        };
    }

    // The address a folio holds, shown as it stands: nothing on this step checked it.
    private static ReadCard FolioAddress(DocHolder h, string then) =>
        new("Not verified", h.Who.Address, $"On the folio, {then}", "is-unverified");

    // A card for something there is nothing to read off, saying why. Not kept.
    private static ReadCard NotRead(string state, string lines, string from) => new(state, lines, from, "is-na");

    /// <summary>What a holder's communication address proof was read to say.</summary>
    public ReadCard MailReadOf(DocHolder h)
    {
        var key = h.Key("mail");
        if (!State.Reads.TryGetValue(key, out var card)) State.Reads[key] = card = MailCard();
        return card;
    }

    private static ReadCard MailCard() => new("Not yet read", "Read off the communication address proof once one is filed.",
        "Confirmed with whoever issued the proof, as the permanent address is.");

    public string? DocumentOf(string payMode) =>
        PaymentModes.FirstOrDefault(m => m.Name == payMode)?.Document;

    public SourcingModeOption? ModeOf(string code) => SourcingModes.FirstOrDefault(m => m.Code == code);
}
