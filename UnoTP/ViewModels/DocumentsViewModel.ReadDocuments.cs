using UnoTP.Models;
using UnoTP.Services;

namespace UnoTP.ViewModels;

// What is done with what OCR read off a proof of address, a payment instrument and a PAN copy.
public partial class DocumentsViewModel
{
    // The address OCR read off a proof is taken onto the application, with what
    // the issuer behind that proof answered when asked about it - asked already,
    // before anything is taken (see CheckAsync), so nothing here can fail part-way.
    // An Aadhaar has no issuer to ask.
    private (string, string) ReadAddress(SlotDef def, DocHolder h, OcrReading reading, Verification answer, LogEntry entry)
    {
        // The permanent address, or the communication address where post goes elsewhere.
        var mailing = def.Key == "mail";
        var what = mailing ? "communication address" : "address";
        var card = mailing ? MailReadOf(h) : State.Reads[h.Key("poa")];
        var type = TypeOf(def, h);
        var named = type.Length > 0 ? type.ToLowerInvariant() : "proof";
        entry.Add("OCR read: " + reading.Address);
        // The number the proof carries, and when it runs out, stand in its box. An
        // Aadhaar shows its last four digits only.
        (card.Number, card.Expiry) = (ProofNumber(type, reading), reading.Expiry);
        if (card.Number.Length > 0) entry.Add($"Number read: {card.Number}{(card.Expiry.Length > 0 ? $", valid till {Dates.Show(card.Expiry)}" : "")}.");
        // The investor's gender, where the folio gives none, sets the category. An
        // Aadhaar, a passport and a voter ID carry it on the copy; a driving licence
        // does not, and its issuer gives it instead.
        var gender = reading.Gender.Length > 0 ? reading.Gender : answer.Gender;
        if (!h.Joint && Who.Gender.Length == 0 && gender.Length > 0 && State.Gender != gender)
        {
            State.Gender = gender;
            entry.Add($"Gender read: {gender}.");
        }
        var issuer = answer.Verifier;

        // The issuer's own validity stands over the one read off the copy, which OCR
        // can take from the wrong line - a licence's issue date, say.
        if (answer.Confirmed && answer.Expiry.Length > 0 && answer.Expiry != card.Expiry)
        {
            entry.Add(card.Expiry.Length > 0
                ? $"{Capitalize(issuer)} holds it valid till {Dates.Show(answer.Expiry)}; the copy read {Dates.Show(card.Expiry)}, which is set aside."
                : $"{Capitalize(issuer)} holds it valid till {Dates.Show(answer.Expiry)}; no expiry could be read off the copy.");
            card.Expiry = answer.Expiry;
        }
        if (answer.Confirmed && answer.Standing.Length > 0 && !answer.Standing.Equals("Active", StringComparison.OrdinalIgnoreCase))
            entry.Add($"{Capitalize(issuer)} holds it as {answer.Standing}.", "warn");

        // The address the copy reads is the one the application takes, confirmed or
        // not: a permanent address and its PIN code are needed either way. Who
        // confirmed it, or why nobody did, stands beside it for Operations.
        // With OCR switched off nothing was read, and the address stays as it stands.
        var read = reading.Address.Trim().Length > 0;
        var before = mailing ? (card.Kind == "is-done" ? card.Lines : "") : AddressOf(h);
        var took = $"Nothing was read off the copy, so the {what} on the application is unchanged.";
        var taken = $"the {what} is left as it stands";
        if (read)
        {
            took = before.Length > 0 && before != reading.Address
                ? $"{Capitalize(what)} on the application replaced. Was: " + before
                : $"{Capitalize(what)} on the application set.";
            taken = $"the {what} is taken as read off the proof";
            (card.Lines, card.Was) = (reading.Address, before);
            if (!mailing) KeepAddress(h, reading.Address);
        }

        // An Aadhaar has no issuer to confirm its address with: the copy has already
        // been identified as an Aadhaar and read, and its name and date of birth matched.
        if (issuer.Length == 0 && type == "Aadhaar" && read)
        {
            (card.State, card.Kind) = ("Read off the Aadhaar", "is-done");
            card.From = "Read off the Aadhaar filed above by OCR. An Aadhaar has no issuer to confirm it with, so it is taken as read.";
            entry.Add("An Aadhaar has no issuer to confirm that address with: it is taken as OCR read it.", "ok");
            entry.Add(took, "ok");
            entry.End("Filed", "ok");
            return ($"Identified and read. The {what} on the application now comes from this Aadhaar, as OCR read it.", "ok");
        }

        // Any other proof with no issuer behind it is said to be so first: there is
        // nobody who "could not be asked".
        if (issuer.Length == 0)
        {
            // "An Aadhaar", "a passport": the proof as a sentence names it.
            var aProof = type == "Aadhaar" ? "an Aadhaar" : $"a {named}";
            (card.State, card.Kind) = ("With Operations", "is-failed");
            card.From = $"{Capitalize(aProof)} has no issuer to check with, so {taken}, for Operations to settle.";
            entry.Add($"{Capitalize(aProof)} has no register behind it to put that address to.", "warn");
            entry.Add(took, "ok");
            entry.End($"Filed, {what} not confirmed", "warn");
            return ($"Read, but nothing outside answers for {aProof}. The copy is filed and {taken}; Operations settle it.", "warn");
        }

        if (answer.NotAsked is { } why)
        {
            (card.State, card.Kind) = ("With Operations", "is-failed");
            card.From = $"{Capitalize(issuer)} could not be asked: {why}. {Capitalize(taken)}, for Operations to settle.";
            entry.Add($"{Capitalize(issuer)} not asked: {why}.", "warn");
            entry.Add(took, "ok");
            entry.End($"Filed, {what} not confirmed", "warn");
            return ($"Read, but {issuer} could not be asked: {why}. The copy is filed and {taken}; Operations settle it.", "warn");
        }
        if (!answer.Confirmed)
        {
            (card.State, card.Kind) = ("Not confirmed", "is-failed");
            card.From = $"{issuer} did not confirm this proof. {Capitalize(taken)}, unconfirmed: upload a clearer copy or a different proof, or Operations settle it.";
            entry.Add($"{issuer} did not confirm that proof.", "bad");
            entry.Add(took, "warn");
            entry.End($"Filed, {what} not confirmed", "warn");
            return ($"Read, but {issuer} did not confirm what it says. The copy is filed and {taken}, unconfirmed — upload a clearer copy, or another proof.", "bad");
        }
        (card.State, card.Kind) = ($"Verified with {issuer}", "is-done");
        card.From = $"Read off the {named} filed above and confirmed with {issuer}.";
        entry.Add($"{issuer} confirmed that proof.", "ok");
        entry.Add(took, "ok");
        entry.End("Filed", "ok");
        return ($"Identified, read and confirmed with {issuer}. The {what} on the application now comes from this proof.", "ok");
    }

    // The permanent address a holder has on the application: the one read off the
    // proof of address filed here, or the one on record for a holder on a folio.
    private string AddressOf(DocHolder h)
    {
        var read = h.Joint ? State.Joint[h.Code].Address : State.Address;
        if (read.Length > 0) return read;
        return h.Who.Address;
    }

    // Keeps the address read off a holder's proof of address, PIN code and all, for
    // their permanent address row.
    private void KeepAddress(DocHolder h, string address)
    {
        if (h.Joint) State.Joint[h.Code].Address = address.Trim();
        else State.Address = address.Trim();
    }

    // A cheque carries an account rather than an address, and it is the bank it is
    // drawn on that is asked to stand behind it.
    private async Task<(string, string)> ReadInstrumentAsync(OcrReading reading, LogEntry entry)
    {
        var card = State.Reads["payment"];
        var mode = State.PayMode.Length > 0 ? State.PayMode.ToLowerInvariant() : "cheque";
        // With OCR switched off nothing was read off the instrument: the copy is filed
        // as handed over, and the account is entered on Bank Details & Payment.
        if (!switches.IsOn(OutsideSwitches.Ocr))
        {
            State.ChequeRead = null;
            (card.State, card.Kind) = ("Not asked", "is-na");
            card.From = $"OCR is {OutsideSwitches.Off}, so the account was not read off this {mode}. Enter the account on Bank Details & Payment.";
            entry.End("Filed", "ok");
            return ($"Filed. OCR is {OutsideSwitches.Off}, so the account was not read off this {mode}; enter it on Bank Details & Payment.", "warn");
        }
        entry.Add("OCR read: " + reading.Account);
        Doing("Checking the account with the bank\u2026");
        var answer = await verification.ConfirmAccountAsync(reading.Account, reading.Bank);
        var bank = answer.Verifier.Length > 0 ? answer.Verifier : "The bank";

        // The bank could not be asked: nothing was found either way, so what was read
        // off the cheque is carried forward for the partner to check on Bank Details.
        if (answer.NotAsked is { } why)
        {
            if (reading.Cheque is null || reading.Cheque.AccountNumber.Length == 0)
            {
                State.ChequeRead = null;
                (card.State, card.Kind) = ("Not read", "is-failed");
                card.From = $"The account could not be read off this {mode}, so nothing is carried forward. Upload a clearer copy of the instrument.";
                entry.Add("No account could be read off the copy.", "warn");
                entry.End("Filed, account not carried", "warn");
                return ($"Filed, but no account could be read off this {mode}. Enter the account on Bank Details & Payment.", "warn");
            }
            card.Lines = reading.Account;
            State.ChequeRead = reading.Cheque;
            (card.State, card.Kind) = ("Read, not confirmed", "is-done");
            card.From = $"Read off the {mode} filed above. The bank was not asked: {why}. Bank Details & Payment opens with this account - check it there.";
            entry.Add($"The bank was not asked: {why}.", "warn");
            entry.Add("Account carried to Bank Details & Payment, as read.", "ok");
            entry.End("Filed", "ok");
            return ($"Read off the {mode}; the bank was not asked to confirm it. Bank Details & Payment opens with this account - check it there.", "warn");
        }

        if (!answer.Confirmed)
        {
            State.ChequeRead = null;
            (card.State, card.Kind) = ("Not confirmed", "is-failed");
            card.From = $"{bank} did not confirm that account against this {mode}, so nothing is carried forward. Upload a clearer copy of the instrument.";
            entry.Add($"{bank} did not confirm that account.", "bad");
            entry.End("Filed, account not carried", "warn");
            return ($"Read, but {bank} did not confirm the account on this {mode}. The copy is filed and no account is carried to Bank Details & Payment.", "bad");
        }
        card.Lines = reading.Account;
        // What Bank Details opens with: the account, its IFSC and the cheque itself.
        State.ChequeRead = reading.Cheque;
        (card.State, card.Kind) = ($"Confirmed with {bank}", "is-done");
        card.From = $"Read off the {mode} filed above and confirmed with {bank}. Bank Details & Payment opens with this account.";
        entry.Add($"{bank} confirmed that account.", "ok");
        entry.Add("Account carried to Bank Details & Payment.", "ok");
        entry.End("Filed", "ok");
        return ($"Identified, read and confirmed with {bank}. Bank Details & Payment opens with this account.", "ok");
    }

    // What a filed PAN copy says while NSDL has not verified it, and how it is put right.
    private (string, string) NotVerifiedCheck(DocHolder h) => NsdlOf(h) switch
    {
        "failed" => ($"Filed, but NSDL holds no record of this PAN against the date of birth searched. If both are right, upload the PAN copy again and NSDL is asked again. If not: {NsdlFailedNext(h)}", "bad"),
        Unanswered => ("Filed, but NSDL could not be asked just now. Upload the PAN copy again and NSDL is asked again.", "warn"),
        _ => ("Filed, but NSDL does not hold this PAN against the name read off it. Type the name as printed on the card, in the NSDL card below.", "warn"),
    };

    // A PAN copy is read for the number on it, and - for a holder with no folio -
    // the PAN-Aadhaar link is asked whether an Aadhaar is held against their PAN,
    // once there is an Aadhaar number on the application to ask with. An unlinked PAN does not stop
    // the application: TDS runs at the higher rate until the investor links it.
    private async Task<(string, string)> ReadPanAsync(DocHolder h, OcrReading reading, LogEntry entry)
    {
        entry.Add("OCR read: PAN " + MaskPan(reading.Pan.Length > 0 ? reading.Pan : h.Who.Pan));
        if (NsdlApplies(h) && !NsdlSettled(h))
        {
            // Not verified: the link waits for NSDL, and the copy and its card say why.
            LinkWaitsOnNsdl(h);
            entry.Add("The PAN-Aadhaar link waits until NSDL verifies the PAN.", "warn");
            entry.End("Filed, not verified", NsdlOf(h) == "failed" ? "bad" : "warn");
            return NotVerifiedCheck(h);
        }
        if (!LinkApplies(h))
        {
            // On a folio: the link is not asked.
            entry.End("Filed", "ok");
            return ($"Identified and read as PAN {MaskPan(h.Who.Pan)}.", "ok");
        }
        var link = await LinkAsync(h, entry);
        entry.End(link switch
        {
            PanAadhaarLink.Linked => "Filed",
            PanAadhaarLink.NotLinked => "Filed, PAN not linked",
            _ => "Filed, link not checked",
        }, link == PanAadhaarLink.Linked ? "ok" : "warn");
        return PanCheck(h, link);
    }
}
