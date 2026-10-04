using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.ViewModels;

// Taking a document: the file's own checks, then identification, OCR and filing.
public partial class DocumentsViewModel
{
    // ===== Taking a document ===================================================

    private const long Mb = 1024 * 1024;

    // Everything that has to be true of the file itself is checked first: a file
    // turned away for its type or its size never reached the document, so it costs
    // no attempt.
    private async Task TakeAsync(SlotDef def, DocHolder h, IFormFile? file)
    {
        var view = View(def, h);
        if (!view.Used) return;
        string? refuse =
            file is null || file.Length == 0 ? "Choose a file to upload"
            : view.Locked is not null ? view.Locked
            : view.Final is not null ? view.Final
            : view.Spent ? $"{MaxAttempts} copies of this document were refused one after another, so it cannot be uploaded just now — {TryAgainIn(view.RetryAt)}."
            : !Accepted(def, file) ? "That file type is not accepted here"
            : file.Length > def.Accepts.MaxMb * Mb ? $"The file is over {def.Accepts.MaxMb} MB — {SizeOf(file.Length)}"
            : null;

        byte[] bytes = [];
        if (refuse is null)
        {
            bytes = await ReadAllAsync(file!);
            // The name and the type the browser gives are only claims: the first
            // bytes say what the file is. Costs no attempt, like any other file problem.
            if (!LooksLike(bytes, file!)) refuse = "That file is not a readable PDF or JPEG";
            else if (def.Key == "photo") refuse = PhotoProblem(bytes);
        }
        if (refuse is not null)
        {
            FlashMessages().Errors[view.Key] = refuse;
            return;
        }

        await PutAsync(def, h, file!, bytes);
    }

    private static bool Accepted(SlotDef def, IFormFile file)
    {
        var accepted = def.Accepts.Mime.Split(',');
        if (accepted.Contains(file.ContentType)) return true;
        // Some browsers send a PDF or a JPEG as a bare stream: its name says what it is.
        var byName = Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "",
        };
        return accepted.Contains(byName);
    }

    /// <summary>
    /// A posted file, read straight into one array of its own length. The request
    /// size limit has already capped how long that can be.
    /// </summary>
    public static async Task<byte[]> ReadAllAsync(IFormFile file)
    {
        var bytes = new byte[file.Length];
        await using var stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(bytes);
        return bytes;
    }

    /// <summary>Whether a file's first bytes are those of the type it claims to be.</summary>
    public static bool LooksLike(byte[] bytes, IFormFile file)
    {
        var pdf = bytes.Length >= 5 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F' && bytes[4] == '-';
        var jpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return file.ContentType == "application/pdf" || ext == ".pdf" ? pdf : jpeg;
    }

    public static string SizeOf(long bytes) =>
        bytes < Mb
            ? $"{Math.Max(1, (int)Math.Round(bytes / 1024.0))} KB"
            : (bytes / (double)Mb).ToString(bytes < 10 * Mb ? "0.0" : "0") + " MB";

    // What a photograph has to be to be worth comparing a face against. The
    // dimensions are read off the image itself rather than assumed from its weight.
    private const int MinKb = 15, MaxKb = 2048, MinPx = 150, MaxPx = 4096;

    private static string? PhotoProblem(byte[] bytes)
    {
        var kb = bytes.Length / 1024.0;
        if (kb < MinKb) return $"That photograph is {Math.Round(kb)} KB. A face cannot be compared against anything under {MinKb} KB.";
        if (kb > MaxKb) return $"That photograph is {Math.Round(kb)} KB, over the {MaxKb} KB a photograph may be.";
        if (JpegSize(bytes) is not var (w, h)) return "That file could not be read as a photograph.";
        if (w < MinPx || h < MinPx) return $"That photograph is {w}×{h} pixels. It must be at least {MinPx} pixels on both sides.";
        if (w > MaxPx || h > MaxPx) return $"That photograph is {w}×{h} pixels, over the {MaxPx} a side that can be handled.";
        return null;
    }

    // A JPEG says its size in its start-of-frame marker; nothing else is needed to find it.
    private static (int Width, int Height)? JpegSize(byte[] b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { i += 2; continue; }
            var length = (b[i + 2] << 8) | b[i + 3];
            // SOF0-SOF15, bar DHT (C4), JPG (C8) and DAC (CC).
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + length;
        }
        return null;
    }

    // What a slot is checked as, for the documents somebody outside answers for,
    // and what a refusal calls it.
    private static readonly Dictionary<string, (DocumentKind Kind, string What)> Checked = new()
    {
        ["pan"] = (DocumentKind.PanCard, "a PAN card"),
        ["poa"] = (DocumentKind.ProofOfAddress, "a proof of address"),
        ["mail"] = (DocumentKind.ProofOfAddress, "a proof of address"),
        ["payment"] = (DocumentKind.Cheque, "a cheque"),
    };

    /// <summary>What a payment instrument's copy says while the cheque check is switched off.</summary>
    public const string ChequeNotChecked = "Filed as handed over. The cheque check is switched off, so nothing is read off it; enter the account on Bank Details & Payment.";

    // Not every document has somebody behind it to ask: the card says who does
    // look at the copy instead, and when.
    /// <summary>What happens to a document no register outside answers for, once uploaded.</summary>
    public static string NoCheckOf(string slot) => NoCheck.GetValueOrDefault(slot, "Filed as handed over.");

    private static readonly Dictionary<string, string> NoCheck = new()
    {
        ["form"] = "Filed as handed over. No register outside answers for an application form — Operations check it against the application before the deposit is booked.",
        ["photo"] = "Filed as handed over. No register outside answers for a photograph — Operations compare it with the KYC record before the deposit is booked.",
        ["empproof"] = "Filed as handed over. No register outside answers for an employee proof — Operations check it against the staff register.",
    };

    // From here it is the document that is being checked, and that is what an
    // attempt is spent on. Each outside service is asked in turn, and each attempt
    // is kept in the history as the checks it went through. A copy that gets
    // through them is filed with DMS; one that does not is kept aside there.
    private async Task PutAsync(SlotDef def, DocHolder h, IFormFile file, byte[] bytes)
    {
        var s = State;
        var key = h.Key(def.Key);
        var entry = new LogEntry(Guid.NewGuid().ToString("n")[..8], h.Joint ? $"{def.Label} · {h.Who.Name}" : def.Label,
            AttemptsNow(key) + 1, DateTime.Now.ToString("HH:mm:ss"), $"{file.FileName} · {SizeOf(file.Length)}") { Holder = h.Joint ? h.Code : "" };
        s.Log.Insert(0, entry);
        var copy = new UploadFile(Path.GetFileName(file.FileName), file.ContentType, bytes);

        // What is filed is described as stored: an Aadhaar is filed masked, a different file from the one handed over.
        StoredDoc Filed(UploadFile stored, string check, string kind) =>
            new(stored.FileName, stored.Bytes.Length, stored.ContentType, check, kind);

        // A cheque whose check is switched off (Backend:Switches:Cheque) is filed as
        // handed over, like a document nothing outside answers for: it is not
        // identified or read, and its account is entered on Bank Details & Payment.
        var chequeOff = def.Key == PaymentSlot.Key && !switches.IsOn(OutsideSwitches.Cheque);
        if (chequeOff || !Checked.TryGetValue(def.Key, out var rule))
        {
            await FileAsync(def, h, copy);
            s.Attempts[key] = 0;
            s.RefusedAt.Remove(key);
            var said = NoCheck.GetValueOrDefault(def.Key, "Filed as handed over.");
            if (chequeOff)
            {
                said = ChequeNotChecked;
                s.ChequeRead = null;
                var card = s.Reads[PaymentSlot.Key];
                (card.State, card.Kind, card.From) = ("Not asked", "is-na", ChequeNotChecked);
                entry.Add($"Filed as handed over — the cheque check is {OutsideSwitches.Off}.");
            }
            else
            {
                entry.Add("Filed as handed over — nothing outside answers for this document.");
            }
            entry.End("Filed", "ok");
            s.Docs[key] = Filed(copy, said, "");
            return;
        }

        try
        {
            await CheckAsync(def, h, rule, copy, entry, Filed);
        }
        catch (ExternalServiceException e)
        {
            // Nothing was checked, so nothing is filed and no attempt is spent.
            entry.Add($"{e.Service} could not answer: {e.Message}", "bad");
            if (e.TraceId is not null) entry.Add("Trace " + e.TraceId);
            entry.End("Not checked", "bad");
            FlashMessages().Errors[key] = $"{e.Message} Nothing was filed, and it does not count as a refusal.";
            FlashMessages().ErrorLog[key] = entry.Id;
        }
    }

    private async Task CheckAsync(SlotDef def, DocHolder h, (DocumentKind Kind, string What) rule, UploadFile copy, LogEntry entry, Func<UploadFile, string, string, StoredDoc> filed)
    {
        var s = State;
        var key = h.Key(def.Key);

        // 1. Identification: is it the document it is handed in as? A proof of
        // address is handed in as nothing in particular: which proof it is is what
        // identification says, and that is its type from here on. A payment is
        // handed in as the mode chosen for it.
        var proof = def.Key is "poa" or "mail";
        var detect = proof && AutoProofType;
        Doing("Identifying the document\u2026");
        var identified = await identifier.IdentifyAsync(rule.Kind, detect ? "" : TypeOf(def, h), copy);
        if (detect && identified.Matches && !ProofsOfAddress.Contains(identified.Type))
            identified = new Identification(false, $"It could not be told which proof of address it is. Upload a clearer copy of {OneOf(ProofsFor(def.Key)).ToLowerInvariant().Replace("aadhaar", "an Aadhaar")}.");
        var type = detect ? identified.Type ?? "" : TypeOf(def, h);

        async Task RefuseAsync(string why, string whatNext, params (string Text, string Kind)[] stages)
        {
            // After the wait the count starts again; the wait runs from this refusal.
            var attempts = s.Attempts[key] = AttemptsNow(key) + 1;
            s.RefusedAt[key] = DateTime.Now;
            // An Aadhaar is kept aside masked, as it is filed masked.
            if (proof && type == "Aadhaar") copy = await MaskedAsync(h, copy);
            var kept = await documents.KeepRefusedAsync(AppNo, FiledUnder(def, h), def.Key, copy);
            // Telling a partner to upload it again when there is nothing left to
            // upload with is worse than saying nothing.
            var message = attempts >= MaxAttempts
                ? $"{why}, and that is {MaxAttempts} refused one after another. With a clearer copy, {TryAgainIn(RetryAt(key))}. The copy is kept for a week as {kept.Ref}."
                : $"{why}. {whatNext} The copy is kept for a week as {kept.Ref}.";
            foreach (var (text, kind) in stages) entry.Add(text, kind);
            entry.Add($"Copy kept for analysis as {kept.Ref} until {kept.KeptUntil:dd MMM yyyy}, and deleted after.", "warn");
            entry.End("Refused", "bad");
            FlashMessages().Errors[key] = message;
            FlashMessages().ErrorLog[key] = entry.Id;
        }

        if (!identified.Matches)
        {
            await RefuseAsync($"That does not read as {rule.What}", identified.Hint ?? "Upload a clearer copy.", ($"Not identified as {rule.What}.", "bad"));
            return;
        }
        // A proof the box does not take - one without a photograph, for the permanent address.
        if (proof && !ProofsFor(def.Key).Contains(type))
        {
            await RefuseAsync($"A {type.ToLowerInvariant()} is not taken as proof of the permanent address",
                $"Upload {(h.Joint ? "this holder's" : "the investor's")} {OneOf(ProofsFor(def.Key))} — a {type.ToLowerInvariant()} proves only a communication address.",
                ($"Identified as a {type.ToLowerInvariant()}, which proves only a communication address.", "bad"));
            return;
        }
        if (switches.IsOn(OutsideSwitches.Identify)) entry.Add($"Identified as {(proof ? "a proof of address: " + type : rule.What)}.", "ok");
        else entry.Add($"Identification is {OutsideSwitches.Off}: taken as {(proof ? type : rule.What)} as handed in; Operations check the copy.", "warn");

        // 2. OCR. An Aadhaar is taken only when the name and date of birth it reads
        // are the PAN's; masked or not, its number need not be read (see ReadAddressAsync).
        Doing("Reading the document (OCR)\u2026");
        var reading = await ocr.ReadAsync(rule.Kind, type, copy, new OcrSubject(h.Who.Pan, h.Who.Dob, h.Who.Name), AadhaarConsent);
        if (!switches.IsOn(OutsideSwitches.Ocr)) entry.Add($"OCR is {OutsideSwitches.Off}: nothing is read off the copy, and it is taken as the holder's own; Operations check it.", "warn");
        // A PAN copy has to be the holder's own: the PAN and date of birth it reads
        // as are the ones already on the application, which cannot be changed here.
        if (def.Key == "pan" && PanCopyMismatch(reading, h.Who.Pan, h.Who.Dob) is { } notTheirs)
        {
            await RefuseAsync(notTheirs, $"Upload {(h.Joint ? "this holder's" : "the investor's")} own PAN card, clear enough to read.",
                ($"OCR read: PAN {(reading.Pan.Length > 0 ? MaskPan(reading.Pan) : "none")}, date of birth {(reading.Dob.Length > 0 ? MaskDate(reading.Dob) : "none")}.", ""),
                ("It does not match the PAN and date of birth on the application.", "bad"));
            return;
        }
        // What the PAN copy reads, for its box once it is filed.
        if (def.Key == "pan")
            State.Reads[h.Key("panocr")] = new ReadCard("", NewApplicationViewModel.NormaliseName(reading.Name), "")
                { Number = reading.Pan.Replace(" ", "").ToUpperInvariant(), Dob = reading.Dob };
        if (proof && type == "Aadhaar" && await AadhaarMismatchAsync(h, reading) is { } notTheirs2)
        {
            await RefuseAsync(notTheirs2, $"Upload {(h.Joint ? "this holder's" : "the investor's")} own Aadhaar, clear enough to read the name and date of birth.",
                ($"OCR read: name {(reading.Name.Trim().Length > 0 ? reading.Name.Trim() : "none")}, date of birth {(reading.Dob.Length > 0 ? MaskDate(reading.Dob) : "none")}.", ""),
                ("An Aadhaar is taken only when its name and date of birth match the PAN's.", "bad"));
            return;
        }

        // A holder with no folio is put to NSDL with the name the copy reads. NSDL not
        // answering does not cost the copy: it was identified and read, so it is filed
        // all the same, and NSDL is asked again from its card - not by uploading it,
        // which would have it identified and read a second time.
        if (def.Key == "pan" && NsdlApplies(h))
        {
            var readName = NewApplicationViewModel.NormaliseName(reading.Name);
            try
            {
                await NsdlAsync(h, readName, entry, typed: false);
            }
            catch (ExternalServiceException e)
            {
                SetNsdl(h, readName, Unanswered);
                entry.Add($"{e.Service} could not answer: {e.Message} The copy is filed all the same; retry the NSDL check from its card.", "warn");
                if (e.TraceId is not null) entry.Add("Trace " + e.TraceId);
            }
            h = Again(h);
        }

        if (detect)
        {
            // Taken: the proof's type is what it was identified as, whatever it was before.
            if (def.Key == "poa") SetPoaType(h, type);
            else SetMail(h, true, type);
            entry.Add($"Its type is set to {type} from the copy.", "ok");
        }

        // 3. Whoever answers for what was read.
        var (check, kind) = def.Key switch
        {
            "pan" => await ReadPanAsync(h, reading, entry),
            "poa" or "mail" => await ReadAddressAsync(def, h, reading, entry),
            _ => await ReadInstrumentAsync(reading, entry),
        };

        // 4. An Aadhaar is masked before it is filed - after OCR has read the whole
        // number off it and the name and date of birth have been matched - so no copy
        // with the whole number is ever stored.
        var masked = proof && type == "Aadhaar";
        if (masked)
        {
            copy = await MaskedAsync(h, copy);
            entry.Add("Aadhaar number masked before the copy is filed.", "ok");
        }
        await FileAsync(def, h, copy);
        s.Docs[key] = filed(copy, check, kind) with { Checks = ChecksOf(def, h, type, reading, masked) };
        // Taken, so whatever was refused before it is behind the partner.
        s.Attempts[key] = 0;
        s.RefusedAt.Remove(key);

        // 5. The PAN-Aadhaar link, with the number read before masking.
        if (proof && type == "Aadhaar") await LinkAfterFilingAsync(h, reading, entry);

        if (def.Key == "poa") await DetailsAsync(h, reading, type, entry);

        // Both copies filed: the faces on them are compared - again, when either is replaced.
        if (def.Key is "poa" or "pan") await FaceAsync(h, entry);
    }

    // What the checks made of a document, kept with it for the record of it
    // (t_FD_BT_KYC_document). A service that is switched off was not asked.
    private DocChecks ChecksOf(SlotDef def, DocHolder h, string type, OcrReading reading, bool masked)
    {
        var identifiedAs = "";
        if (switches.IsOn(OutsideSwitches.Identify)) identifiedAs = def.Key == "pan" ? "PAN card" : type;

        var ocrAsked = switches.IsOn(OutsideSwitches.Ocr);
        var read = ocrAsked && reading != new OcrReading();
        if (!read) return new DocChecks(identifiedAs, ocrAsked, Masked: masked);
        return new DocChecks(identifiedAs, ocrAsked, true, NumberOn(def, type, reading), ExpiryOn(def, h), masked);
    }

    // The number a document carries. An Aadhaar's is never kept whole: its last four digits only.
    private string NumberOn(SlotDef def, string type, OcrReading reading)
    {
        if (def.Key == "pan") return reading.Pan.Replace(" ", "").ToUpperInvariant();
        if (def.Key == "payment") return reading.Cheque?.Number ?? "";

        var number = (reading.Number.Length > 0 ? reading.Number : reading.IdNumber).Replace(" ", "");
        if (type == "Aadhaar") return number.Length >= 4 ? "XXXXXXXX" + number[^4..] : "";
        if (!HasPhoto(type)) return "";
        return number;
    }

    // When a proof runs out, as its box shows it: the issuer's date where the issuer gave one.
    private string ExpiryOn(SlotDef def, DocHolder h)
    {
        if (def.Key == "poa") return State.Reads[h.Key("poa")].Expiry;
        if (def.Key == "mail") return MailReadOf(h).Expiry;
        return "";
    }

    // 5. After an Aadhaar is filed: it carries the number the PAN-Aadhaar link is
    // asked with, so a PAN already on the application can be asked about now. The
    // number is the one OCR read off the copy before it was masked, and is kept in
    // the session alone. Where OCR could not read the whole number - masked already,
    // or not clear enough - it is typed instead.
    private async Task LinkAfterFilingAsync(DocHolder h, OcrReading reading, LogEntry entry)
    {
        if (!LinkApplies(h)) return;
        if (AadhaarNumbers.IsWhole(reading.IdNumber))
        {
            await AskLinkWithAsync(h, reading.IdNumber, entry);
            return;
        }
        session.Remove(AadhaarKey(h));
        LinkWaitsOnNumber(h);
        entry.Add("OCR read no whole 12-digit Aadhaar number off it; the number is typed for the PAN-Aadhaar link.", "warn");
    }

    // A slot holds one copy in DMS: a copy filed before is deleted, then the new
    // one filed. One that came over from the step before has no copy here.
    private async Task FileAsync(SlotDef def, DocHolder h, UploadFile copy)
    {
        var under = FiledUnder(def, h);
        if (State.Docs.GetValueOrDefault(h.Key(def.Key)) is { Before: false }) await documents.DeleteAsync(AppNo, under, def.Key);
        Doing("Filing the copy\u2026");
        await documents.FileAsync(AppNo, under, def.Key, copy);
    }

    /// <summary>The words with the first letter in capitals.</summary>
    private static string Capitalize(string words) => words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
}
