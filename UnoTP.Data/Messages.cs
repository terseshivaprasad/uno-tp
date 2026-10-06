namespace UnoTP;

/// <summary>
/// Every validation and error message the app shows, in one place. To change what
/// a message says, change its words here and nowhere else: the pages, the checks
/// on the server and the scripts in the browser all take it from this file.
///
/// Each message is under the page that shows it. The line above it says which
/// field it belongs to and when it is shown; docs/VALIDATIONS.md is the same list
/// as a table, written from this file (UnoTP.Tests/ValidationListTests.cs).
///
/// A message with a value in it is a small method: the value goes where its name
/// stands in braces. Keep the braces and the name; change the words around them.
/// </summary>
public static class Messages
{
    /// <summary>What any page can say: typed text, a stale page, too many tries, something going wrong.</summary>
    [Page("Every page")]
    public static class Shared
    {
        [Shown("Any typed text", "a character other than a letter, digit, space or - , & / .")]
        public const string OnlyAllowedCharacters =
            "Only letters, digits, spaces and - , & / . are allowed";

        [Shown("Any typed text", "longer than the field takes")]
        public static string TooLong(object? max) =>
            $"At most {max} characters";

        [Shown("A name", "anything but letters", InBrowser = true)]
        public const string LettersOnly =
            "Enter letters only";

        [Shown("An address line", "a character an address does not take", InBrowser = true)]
        public const string NoSpecialCharacters =
            "No special characters";

        [Shown("A code", "anything but letters and digits", InBrowser = true)]
        public const string LettersAndDigitsOnly =
            "Letters and digits only";

        [Shown("A date", "not a real date", InBrowser = true)]
        public const string DateNotReal =
            "Enter a real date, DD/MM/YYYY";

        [Shown("A date", "a date still to come", InBrowser = true)]
        public const string DateInFuture =
            "Enter a date that is not in the future";

        [Shown("The whole step", "the application was changed in another window while this one was being sent")]
        public const string ChangedElsewhere =
            "This application changed somewhere else while that was being sent, so it was not kept. The page shows it as it stands now — do it again.";

        [Shown("An upload or a check", "more tries in a minute than are allowed")]
        public static string TooManyTries(object? perMinute) =>
            $"That is more than {perMinute} tries in a minute. Wait a minute, then do it again. Nothing was sent this time, and no attempt was used.";

        [Shown("Leaving a step", "the step has changes that are not saved (asks before leaving)", InBrowser = true)]
        public const string UnsavedChanges =
            "This step has changes that are not saved yet. Leave it without saving them?";

        [Shown("Any request", "the request carries input no page takes")]
        public const string InputRefused =
            "The request carried input this page does not take.";

        [Shown("Any request", "an error nobody caught, answered to a script")]
        public const string SomethingWentWrong =
            "Something went wrong.";

        [Shown("Any request", "an error nobody caught, and the error page failed too")]
        public static string CouldNotFinish(object? reference) =>
            $"Uno TP could not finish that. Reference: {reference}";

        [Shown("The error page", "an error nobody caught")]
        public const string CouldNotFinishPage =
            "Uno TP could not finish that. Nothing you saved is lost: go back and try again, and if it happens again, give support the reference below.";

        [Shown("A search-as-you-type field", "the search could not be answered", InBrowser = true)]
        public const string SearchFailed =
            "Could not search just now. Check the connection and type again.";
    }

    /// <summary>Why Uno TP did not open, and the pages that say a session ended or a limit was passed.</summary>
    [Page("Coming in from the portal")]
    public static class SignIn
    {
        [Shown("The link from the portal", "its user or system code is missing")]
        public const string LinkIncomplete =
            "The link from the portal is missing its user or system code.";

        [Shown("The link from the portal", "the decryption service did not answer")]
        public const string PortalNotReadNow =
            "The portal's details could not be read just now. Try opening Uno TP from the portal again in a while.";

        [Shown("The link from the portal", "what it carries could not be decrypted")]
        public const string LinkNotRead =
            "The link from the portal could not be read. Open Uno TP from the portal again.";

        [Shown("The session", "the portal started none for this user")]
        public const string NoSessionFromPortal =
            "The portal did not start a session for you on Uno TP. Ask your administrator for access.";

        [Shown("The session", "the sign-in service did not answer")]
        public const string SessionNotStarted =
            "Uno TP could not start your session just now. Try again in a while.";

        [Shown("The menu", "Uno TP is not on the user's menu")]
        public const string NotOnMenu =
            "Your menu does not include Uno TP. Ask your administrator for access.";

        [Shown("A page", "the user has no access to it")]
        public const string NoAccess =
            "You do not have access to this page.";

        [Shown("The Session Expired page", "the session ended, or the app was not opened from the portal")]
        public const string SessionEndedWhy =
            "For your security, Uno TP signs you out when your session ends, or when it was not opened from the portal.";

        [Shown("The Session Expired page", "the session ended, or the app was not opened from the portal")]
        public const string SessionEndedWhatNext =
            "Open Uno TP from the portal again. A draft you saved is kept, and opens from Investor Identification.";

        [Shown("The Too Many Requests page", "more requests in a minute than are taken")]
        public const string TooManyRequestsWhy =
            "Uno TP received more requests from you in a minute than it takes, so the last one was not carried out.";

        [Shown("The Too Many Requests page", "more requests in a minute than are taken")]
        public const string TooManyRequestsWhatNext =
            "Wait a minute, then go back and do it again. Everything saved before it is kept.";
    }

    /// <summary>The PAN, date of birth and folio search, and what the register holds against them.</summary>
    [Page("Investor Identification")]
    public static class InvestorIdentification
    {
        [Shown("Cancel (an incomplete application)", "the application is not there to cancel")]
        public const string NoDraftToCancel =
            "There is no such incomplete application to cancel.";

        [Shown("PAN", "left empty")]
        public const string PanRequired =
            "Enter the PAN";

        [Shown("PAN", "not five letters, four digits and a letter", InBrowser = true)]
        public const string PanInvalid =
            "Enter a valid PAN, like ABCDE1234F";

        [Shown("Date of Birth", "a part of the date left empty")]
        public const string DobRequired =
            "Enter the date of birth";

        [Shown("Date of Birth", "the year has fewer than four digits")]
        public const string YearInFull =
            "Enter the year in full";

        [Shown("Date of Birth", "not a real date")]
        public const string DateInvalid =
            "Enter a valid date";

        [Shown("Date of Birth", "a date still to come")]
        public const string DobInFuture =
            "The date of birth cannot be in the future";

        [Shown("Date of Birth", "the depositor is younger than the minimum age")]
        public static string UnderAge(object? minimumAge) =>
            $"The depositor must be {minimumAge} years or above";

        [Shown("Folio Number", "left empty")]
        public const string FolioRequired =
            "Enter the folio number";

        [Shown("Folio Number", "the register holds no such folio")]
        public const string FolioNotFound =
            "No record against that folio number — check it, or search by PAN instead";

        [Shown("The record found", "the PAN is first holder of a folio with no deposit on it")]
        public static string FolioWithoutDeposit(object? pan, object? folio) =>
            $"The folio master holds PAN {pan} as the first holder of folio {folio}, but no deposit of theirs was found on it. Operations has to look at the record before a deposit can be booked.";

        [Shown("The record found", "several folios are held against the PAN")]
        public static string ManyFoliosToMerge(object? folios, object? lastFolio, object? pan) =>
            $"Folios {folios} and {lastFolio} are all held against PAN {pan}. Operations has to merge them before a deposit can be booked against the PAN.";

        [Shown("The record found", "the register holds the PAN without a date of birth")]
        public static string NoDobOnRegister(object? pan, object? folio) =>
            $"The register holds PAN {pan} (folio {folio}) without a date of birth, so the investor's age cannot be checked. Operations has to add it to the record before a deposit can be booked.";

        [Shown("The record found", "the folio has a deposit but no record of its own")]
        public static string FolioRecordMissing(object? folio, object? pan) =>
            $"Folio {folio} has a deposit against PAN {pan}, but the folio's own record (name and address) could not be found. Operations has to look at the record before a deposit can be booked.";

        [Shown("The record found", "the folio belongs to a non-individual")]
        public const string NonIndividual =
            "Deposits from non-individual entities are prohibited.";

        [Shown("The record found", "the folio has no date of birth")]
        public const string NoDobOnFolio =
            "DOB is not updated for Existing Folio.";

        [Shown("The record found", "the date of birth searched is not the folio's")]
        public const string DobDoesNotMatchFolio =
            "The DOB for the existing folio does not match the DOB provided for the search.";

        [Shown("The record found", "several folios are held against the PAN")]
        public const string ManyFolios =
            "Multiple folios found against the provided PAN. Please check with Mahindra Finance Fixed Deposit Team.";

        [Shown("The record found", "a folio is held against the PAN, so the investor is not new")]
        public const string ExistingHolder =
            "A folio is already held against the provided PAN, so the investor cannot go on as new. Please check with Mahindra Finance Fixed Deposit Team.";

        [Shown("Cancel (an incomplete application)", "pressed (asks first)")]
        public static string ConfirmCancelDraft(object? applicationNumber) =>
            $"Cancel application {applicationNumber}? It is dropped and cannot be continued again.";
    }

    /// <summary>The fields of the step, a copy that is refused, and what stops Proceed.</summary>
    [Page("Upload Documents")]
    public static class UploadDocuments
    {
        [Shown("A refused copy", "said of a joint holder, inside \"Upload … own PAN card\"")]
        public const string ThisHolders =
            "this holder's";

        [Shown("A refused copy", "said of the first holder, inside \"Upload … own PAN card\"")]
        public const string TheInvestors =
            "the investor's";

        [Shown("Proof of address type", "not chosen")]
        public const string PoaTypeRequired =
            "Choose the proof of address";

        [Shown("Communication address proof type", "not chosen")]
        public const string MailTypeRequired =
            "Choose the communication address proof";

        [Shown("Payment Mode", "not chosen")]
        public const string PayModeRequired =
            "Choose the payment mode";

        [Shown("Sourcing Mode", "not chosen")]
        public const string SourcingRequired =
            "Choose the sourcing mode";

        [Shown("Broker / employee code", "left empty")]
        public static string SourceCodeRequired(object? codeLabel) =>
            $"Enter the {codeLabel}";

        [Shown("Sub Broker Code", "left empty where the sourcing mode asks for it")]
        public const string SubBrokerRequired =
            "Enter the sub broker code";

        [Shown("Deposit Category", "none is set because no sourcing mode is chosen")]
        public const string CategoryNotSet =
            "No deposit category is set: choose the sourcing mode";

        [Shown("Employee Code", "left empty")]
        public const string EmployeeCodeRequired =
            "Enter the employee code";

        [Shown("Employee company", "left empty")]
        public const string EmployeeCompanyRequired =
            "Enter the employee company name";

        [Shown("Employee holder", "not chosen")]
        public const string EmployeeHolderRequired =
            "Choose which holder is the employee";

        [Shown("Relation with the holder", "not chosen")]
        public const string EmployeeRelationRequired =
            "Choose the relation with the holder";

        [Shown("Employee Proof", "not chosen")]
        public const string EmployeeProofRequired =
            "Choose the employee proof";

        [Shown("Physical Application Form No.", "left empty on a physical application")]
        public const string FormNoRequired =
            "Enter the physical form number";

        [Shown("A document", "Proceed pressed without it")]
        public const string DocumentRequired =
            "This document is required";

        [Shown("Proof of address (upload)", "no proof type chosen yet")]
        public const string ChoosePoaTypeFirst =
            "Choose the proof of address first";

        [Shown("Communication address proof (upload)", "no proof type chosen yet")]
        public const string ChooseMailTypeFirst =
            "Choose the communication address proof first";

        [Shown("Employee proof (upload)", "no proof type chosen yet")]
        public const string ChooseEmployeeProofFirst =
            "Choose the employee proof first";

        [Shown("Instrument copy (upload)", "no payment mode chosen yet")]
        public const string ChoosePayModeFirst =
            "Choose the payment mode first";

        [Shown("A document (upload)", "refused too many times in a row")]
        public const string RefusedTooOften =
            "Refused too many times in a row: try again after some time.";

        [Shown("Communication Address", "switching to Same as Permanent with a proof filed (asks first)")]
        public const string ConfirmDropMailProof =
            "Post will go to the permanent address, and the communication address proof uploaded will be removed. Switch to Same as Permanent?";

        [Shown("Fetch from CKYC", "NSDL has not verified the PAN copy yet")]
        public const string CkycWaitsOnPan =
            "Available once NSDL verifies the uploaded PAN copy.";

        [Shown("Fetch from CKYC", "the CKYC service is switched off")]
        public const string CkycSwitchedOff =
            "Unavailable: the CKYC service is switched off for now. Upload the proof of address and the photograph instead.";

        [Shown("Fetch from CKYC", "the CKYC service could not answer")]
        public static string CkycNotFetched(object? why) =>
            $"{why} The KYC was not fetched; upload the proof of address and the photograph, or try again.";

        [Shown("Fetch from CKYC", "CERSAI holds no record for the PAN and date of birth")]
        public static string CkycNoRecord(object? why) =>
            $"CERSAI holds no CKYC record for this PAN and date of birth{why}. Upload the proof of address and the photograph instead.";

        [Shown("A document (upload)", "no file chosen")]
        public const string NoFileChosen =
            "Choose a file to upload";

        [Shown("A document (upload)", "not a file type the document takes")]
        public const string FileTypeNotAccepted =
            "That file type is not accepted here";

        [Shown("A document (upload)", "over the size the document takes")]
        public static string FileTooLarge(object? maxMb, object? size) =>
            $"The file is over {maxMb} MB — {size}";

        [Shown("A document (upload)", "the file is not really a PDF or JPEG")]
        public const string FileNotReadable =
            "That file is not a readable PDF or JPEG";

        [Shown("Photograph", "under the smallest size a face can be compared at")]
        public static string PhotoTooSmall(object? kb, object? minimumKb) =>
            $"That photograph is {kb} KB. A face cannot be compared against anything under {minimumKb} KB.";

        [Shown("Photograph", "over the largest size a photograph may be")]
        public static string PhotoTooLarge(object? kb, object? maximumKb) =>
            $"That photograph is {kb} KB, over the {maximumKb} KB a photograph may be.";

        [Shown("Photograph", "the file cannot be read as a picture")]
        public const string PhotoNotReadable =
            "That file could not be read as a photograph.";

        [Shown("Photograph", "fewer pixels a side than the minimum")]
        public static string PhotoTooFewPixels(object? width, object? height, object? minimumPixels) =>
            $"That photograph is {width}×{height} pixels. It must be at least {minimumPixels} pixels on both sides.";

        [Shown("Photograph", "more pixels a side than can be handled")]
        public static string PhotoTooManyPixels(object? width, object? height, object? maximumPixels) =>
            $"That photograph is {width}×{height} pixels, over the {maximumPixels} a side that can be handled.";

        [Shown("A document (upload)", "an outside check could not answer, so nothing was filed")]
        public static string NothingFiled(object? why) =>
            $"{why} Nothing was filed, and it does not count as a refusal.";

        [Shown("Proof of address", "it cannot be told which proof it is")]
        public static string ProofNotIdentified(object? proofs) =>
            $"It could not be told which proof of address it is. Upload a clearer copy of {proofs}.";

        [Shown("A document (upload)", "the copy is not the document asked for")]
        public static string NotReadAs(object? document) =>
            $"That does not read as {document}";

        [Shown("A document (upload)", "the copy is not the document asked for, and nothing more is known")]
        public const string UploadClearerCopy =
            "Upload a clearer copy.";

        [Shown("Proof of address", "a PAN card is filed as the proof")]
        public const string NotAProofOfAddress =
            "It reads as a PAN card, which is not a proof of address. Upload an Aadhaar, passport, driving licence or voter ID.";

        [Shown("Proof of address", "the kind of proof is clear but it cannot be read")]
        public static string ReadsAsButUnreadable(object? document) =>
            $"It reads as {document}, but could not be read. Upload a sharper scan, with the whole card in view.";

        [Shown("A document (upload)", "the copy cannot be read")]
        public const string CopyUnreadable =
            "The copy could not be read. Upload a sharper scan, with the whole card in view.";

        [Shown("A document (upload)", "the copy is another document than the one asked for")]
        public static string ReadsAsAnother(object? found, object? wanted) =>
            $"It reads as {found}. Upload {wanted} itself.";

        [Shown("Proof of address", "the proof is one that proves only a communication address")]
        public static string NotProofOfPermanentAddress(object? proof) =>
            $"A {proof} is not taken as proof of the permanent address";

        [Shown("Proof of address", "the proof is one that proves only a communication address")]
        public static string UploadProofOfPermanentAddress(object? whose, object? proofs, object? proof) =>
            $"Upload {whose} {proofs} — a {proof} proves only a communication address.";

        [Shown("PAN copy", "the PAN or date of birth read off it is not the one on the application")]
        public static string UploadOwnPan(object? whose) =>
            $"Upload {whose} own PAN card, clear enough to read.";

        [Shown("Proof of address (Aadhaar)", "its name or date of birth is not the PAN's")]
        public static string UploadOwnAadhaar(object? whose) =>
            $"Upload {whose} own Aadhaar, clear enough to read the name and date of birth.";

        [Shown("Proof of address (Aadhaar)", "neither the number nor its last 4 digits can be read")]
        public const string AadhaarNumberNotRead =
            "The Aadhaar number could not be read off it";

        [Shown("Proof of address (Aadhaar)", "neither the number nor its last 4 digits can be read")]
        public const string UploadAadhaarWithNumber =
            "Upload a copy of the Aadhaar that shows its number — all 12 digits, or the last 4 of a masked one — clear enough to read.";

        [Shown("Proof of address", "the address or its PIN code cannot be read")]
        public static string UploadProofWithAddress(object? proof) =>
            $"Upload a copy of the {proof} that shows the address with its PIN code, clear enough to read.";

        [Shown("Proof of address", "the address cannot be read")]
        public const string AddressNotRead =
            "The address could not be read off it";

        [Shown("Proof of address", "the PIN code cannot be read")]
        public const string PinNotRead =
            "The PIN code could not be read off it";

        [Shown("PAN copy", "OCR cannot read the PAN number")]
        public const string PanNotReadOnCopy =
            "OCR could not read the PAN number on it";

        [Shown("PAN copy", "the PAN read is not the one on the application")]
        public static string PanReadsAsAnother(object? read, object? entered) =>
            $"The PAN on it reads as {read}, not {entered}";

        [Shown("PAN copy", "OCR cannot read the date of birth")]
        public const string DobNotReadOnCopy =
            "OCR could not read the date of birth on it";

        [Shown("PAN copy", "the date of birth read is not the one entered")]
        public static string DobNotTheOneEntered(object? entered) =>
            $"The date of birth on it does not match the one entered ({entered})";

        [Shown("Proof of address (Aadhaar)", "no PAN copy is filed yet to match the name with")]
        public const string PanNameNotKnown =
            "The PAN's name is not known yet, so the Aadhaar cannot be matched with it — file the PAN copy first";

        [Shown("Proof of address (Aadhaar)", "neither its name nor its date of birth is the PAN's")]
        public const string AadhaarNameAndDobMismatch =
            "The name and date of birth on the Aadhaar do not match the PAN's";

        [Shown("Proof of address (Aadhaar)", "its name cannot be read")]
        public const string AadhaarNameNotRead =
            "The name on the Aadhaar could not be read";

        [Shown("Proof of address (Aadhaar)", "its name is not the PAN's")]
        public const string AadhaarNameMismatch =
            "The name on the Aadhaar does not match the PAN's";

        [Shown("Proof of address (Aadhaar)", "its date of birth cannot be read")]
        public const string AadhaarDobNotRead =
            "The date of birth on the Aadhaar could not be read";

        [Shown("Proof of address (Aadhaar)", "its date of birth is not the PAN's")]
        public const string AadhaarDobMismatch =
            "The date of birth on the Aadhaar does not match the PAN's";

        [Shown("Aadhaar number", "the first 8 digits are not all typed")]
        public const string AadhaarFirst8Required =
            "Enter the first 8 digits of the Aadhaar number";

        [Shown("Aadhaar number", "not 12 digits")]
        public const string Aadhaar12Required =
            "Enter the 12-digit Aadhaar number";

        [Shown("Aadhaar number", "not a valid number, with the last 4 read off the copy")]
        public static string AadhaarInvalidCheckFirst8(object? lastFour) =>
            $"That is not a valid Aadhaar number — check the first 8 digits against the card. If its last 4 are not {lastFour}, upload the Aadhaar again";

        [Shown("Aadhaar number", "not a valid number")]
        public const string AadhaarInvalid =
            "That is not a valid Aadhaar number — check it against the card";

        [Shown("Name as on the PAN", "fewer than three letters typed")]
        public const string PanNameRequired =
            "Enter the name as printed on the PAN";

        [Shown("Name as on the PAN", "anything but letters")]
        public const string PanNameLettersOnly =
            "Enter the name as printed on the PAN: letters only";

        [Shown("Name as on the PAN", "NSDL holds no such PAN and date of birth")]
        public static string NsdlNoRecord(object? pan) =>
            $"NSDL holds no record of PAN {pan} against the date of birth searched";

        [Shown("Name as on the PAN", "NSDL does not hold the PAN against the name typed")]
        public static string NsdlNameNotHeld(object? pan) =>
            $"NSDL does not hold PAN {pan} against that name";

        [Shown("PAN-Aadhaar link", "the link service could not answer")]
        public static string LinkNotAskedNow(object? why) =>
            $"The PAN-Aadhaar link could not be asked just now: {why}";

        [Shown("Proceed", "the PAN-Aadhaar link is not confirmed")]
        public const string LinkMustBeConfirmed =
            "The PAN-Aadhaar link must be confirmed before proceeding.";

        [Shown("Proceed", "the PAN is not linked with Aadhaar")]
        public const string PanNotLinked =
            "The PAN is not linked with Aadhaar. The investor links it with the Income Tax department; the application cannot proceed until it is.";

        [Shown("Proceed", "the Aadhaar number is still to be typed")]
        public const string TypeAadhaarNumber =
            "Type the Aadhaar number in the row under the proofs of address, so the PAN-Aadhaar link can be asked.";

        [Shown("Proceed", "the PAN-Aadhaar link could not be checked")]
        public const string LinkNotChecked =
            "The PAN-Aadhaar link could not be checked. Upload the Aadhaar again, or type its number, to ask again.";

        [Shown("Proceed", "the name on the proof of address is not matched with the PAN's yet")]
        public const string NameMustBeMatched =
            "The name on the proof of address must be matched with the PAN's before proceeding.";

        [Shown("Proceed", "the name or date of birth on the proof of address is not the PAN's")]
        public const string NameAndDobMustMatch =
            "The name and date of birth on the proof of address must match the PAN's before proceeding. Upload a clearer copy of the holder's own proof.";

        [Shown("Proceed", "NSDL holds no such PAN and date of birth")]
        public static string NsdlFailedUploadAgain(object? whatElse) =>
            $"NSDL holds no such PAN and date of birth. Upload the PAN copy again if both are right. If not: {whatElse}";

        [Shown("Proceed", "NSDL could not be asked about the PAN")]
        public const string NsdlNotAsked =
            "NSDL could not be asked about the PAN. Upload the PAN copy again";

        [Shown("Proceed", "NSDL holds the PAN under another name")]
        public const string TypeNameAskAgain =
            "Type the name as printed on the PAN, and ask NSDL again";

        [Shown("Proceed", "NSDL holds no such PAN and date of birth, for a joint holder")]
        public const string RemoveHolderSearchAgain =
            "Remove this holder and search again.";

        [Shown("Proceed", "NSDL holds no such PAN and date of birth, for the first holder")]
        public const string StartAgainWithRightPan =
            "Start again from Investor Identification with the right PAN and date of birth.";

        [Shown("Still missing", "no application form filed")]
        public const string MissingForm =
            "the application form";

        [Shown("Still missing", "no PAN copy filed")]
        public const string MissingPan =
            "the PAN copy";

        [Shown("Still missing", "NSDL has not verified the PAN")]
        public const string MissingNsdl =
            "the PAN verified with NSDL";

        [Shown("Still missing", "no proof of address type chosen")]
        public const string MissingPoaType =
            "the proof of address type";

        [Shown("Still missing", "no proof of address filed")]
        public const string MissingPoa =
            "the proof of address";

        [Shown("Still missing", "no photograph filed")]
        public const string MissingPhoto =
            "the photograph";

        [Shown("Still missing", "no communication address proof type chosen")]
        public const string MissingMailType =
            "the communication address proof type";

        [Shown("Still missing", "no communication address proof filed")]
        public const string MissingMail =
            "the communication address proof";

        [Shown("Still missing", "no payment mode chosen")]
        public const string MissingPayMode =
            "the payment mode";

        [Shown("Still missing", "no instrument copy filed")]
        public const string MissingInstrument =
            "the instrument copy";

        [Shown("Still missing", "no sourcing mode chosen")]
        public const string MissingSourcing =
            "the sourcing mode";

        [Shown("Still missing", "no sub broker code typed")]
        public const string MissingSubBroker =
            "the sub broker code";

        [Shown("Still missing", "no deposit category set")]
        public const string MissingCategory =
            "the deposit category";

        [Shown("Still missing", "no employee code typed")]
        public const string MissingEmployeeCode =
            "the employee code";

        [Shown("Still missing", "no employee company typed")]
        public const string MissingEmployeeCompany =
            "the employee company";

        [Shown("Still missing", "the employee holder is not chosen")]
        public const string MissingEmployeeHolder =
            "the employee holder";

        [Shown("Still missing", "the relation with the holder is not chosen")]
        public const string MissingEmployeeRelation =
            "the relation with the holder";

        [Shown("Still missing", "no employee proof type chosen")]
        public const string MissingEmployeeProofType =
            "the employee proof type";

        [Shown("Still missing", "no employee proof filed")]
        public const string MissingEmployeeProof =
            "the employee proof";

        [Shown("Still missing", "no form number typed on a physical application")]
        public const string MissingFormNo =
            "the form number";

        [Shown("Broker / employee code", "nothing on the register matches what is typed", InBrowser = true)]
        public static string NothingOnRegister(object? typed) =>
            $"Nothing on the register matches “{typed}”";
    }

    /// <summary>The holders' details, the nominee and guardian, and what stops the investment online.</summary>
    [Page("Investor Information")]
    public static class InvestorInformation
    {
        [Shown("Joint holder search", "the PAN is already a holder on the application")]
        public const string PanAlreadyOn =
            "This PAN is already on the application";

        [Shown("Gender", "not chosen")]
        public const string GenderRequired =
            "Select the gender";

        [Shown("Name type", "not chosen")]
        public const string NameTypeRequired =
            "Select the name type";

        [Shown("Father / Mother / Spouse name", "left empty")]
        public const string ParentNameRequired =
            "Enter the father's, mother's or spouse's name";

        [Shown("Annual Income", "not chosen")]
        public const string IncomeRequired =
            "Select the annual income";

        [Shown("Occupation", "not chosen")]
        public const string OccupationRequired =
            "Select the occupation";

        [Shown("Sub Occupation", "not chosen", InBrowser = true)]
        public const string SubOccupationRequired =
            "Select the sub occupation";

        [Shown("Sub Occupation", "not one that goes with the occupation")]
        public const string SubOccupationMismatch =
            "Select a sub occupation that goes with the occupation";

        [Shown("Marital Status", "not chosen")]
        public const string MaritalStatusRequired =
            "Select the marital status";

        [Shown("Mobile Number", "left empty")]
        public const string MobileRequired =
            "Enter the mobile number";

        [Shown("Mobile Number", "not ten digits starting 6 to 9", InBrowser = true)]
        public const string MobileInvalid =
            "Enter a 10-digit mobile number";

        [Shown("E-Mail", "left empty")]
        public const string EmailRequired =
            "Enter the e-mail";

        [Shown("E-Mail", "not an e-mail address", InBrowser = true)]
        public const string EmailInvalid =
            "Enter a valid e-mail";

        [Shown("A PEP question", "neither Yes nor No chosen")]
        public const string YesOrNoRequired =
            "Choose Yes or No";

        [Shown("Address Line 1", "left empty")]
        public const string AddressLine1Required =
            "Enter the first line of the address";

        [Shown("City", "left empty")]
        public const string CityRequired =
            "Enter the city";

        [Shown("PIN code", "left empty")]
        public const string PinRequired =
            "Enter the PIN code";

        [Shown("PIN code", "not six digits", InBrowser = true)]
        public const string PinInvalid =
            "Enter a 6-digit PIN code";

        [Shown("PIN code", "no district is found for it")]
        public const string PinHasNoDistrict =
            "No district is found for this PIN code; check it";

        [Shown("Nominee name", "left empty")]
        public const string NomineeNameRequired =
            "Enter the nominee's name";

        [Shown("Nominee date of birth", "a part of the date left empty")]
        public const string NomineeDobRequired =
            "Enter the nominee's date of birth";

        [Shown("Nominee date of birth", "not a real date, or one still to come")]
        public const string NomineeDobNotReal =
            "Enter a real date of birth, not a future one";

        [Shown("Relation with primary holder", "not chosen")]
        public const string NomineeRelationRequired =
            "Select the relation with the primary holder";

        [Shown("Guardian name", "left empty for a minor nominee")]
        public const string GuardianNameRequired =
            "Enter the guardian's name";

        [Shown("Clear All", "pressed (asks first)")]
        public const string ConfirmClearAll =
            "Clear everything typed on this step, and remove the joint holders and the nominee?";

        [Shown("FATCA questions", "a holder is a tax or permanent resident of another country")]
        public const string FatcaOffline =
            "This investments needs to be done through offline mode. Kindly reach out to the nearest Mahindra branch. A list of all our branches is available on our website.";

        [Shown("Proceed", "name screening does not allow a holder to invest online")]
        public const string StopScreeningTitle =
            "Cannot proceed: not allowed to invest online";

        [Shown("Proceed", "name screening does not allow a holder to invest online")]
        public static string StopScreeningWho(object? holders) =>
            $"Name screening does not allow {holders} to invest online.";

        [Shown("Proceed", "name screening does not allow a holder to invest online")]
        public const string StopScreeningWhatNext =
            "This investment cannot be made here. Kindly ask the investor to visit the nearest Mahindra Finance branch to invest offline; a list of all branches is on our website.";

        [Shown("Proceed", "a holder is a tax or permanent resident of another country")]
        public const string StopOfflineTitle =
            "Cannot proceed: the investment has to be made offline";

        [Shown("Proceed", "a holder is a tax or permanent resident of another country")]
        public const string StopOfflineWho =
            "A holder is a tax or permanent resident of a country other than India.";

        [Shown("Proceed", "a holder is a tax or permanent resident of another country")]
        public const string StopOfflineWhatNext =
            "Kindly ask the investor to visit the nearest Mahindra Finance branch to invest offline; a list of all branches is on our website.";

        [Shown("PIN code", "no district is found for it, as it is typed", InBrowser = true)]
        public const string PinNotFound =
            "Not found for this PIN code";
    }

    /// <summary>The payment and repayment accounts and the cheque.</summary>
    [Page("Bank Details & Payment")]
    public static class BankDetails
    {
        [Shown("Bank", "no bank searched and picked")]
        public const string BankRequired =
            "Search for the bank and pick its branch";

        [Shown("Bank", "no branch has the IFSC typed")]
        public const string IfscNotFound =
            "No branch has this IFSC — pick one from the search, or check it against the cheque";

        [Shown("Account Number", "not 6 to 18 digits", InBrowser = true)]
        public const string AccountInvalid =
            "Enter the account number, 6 to 18 digits";

        [Shown("Re-enter Account Number", "left empty")]
        public const string AccountAgainRequired =
            "Enter the account number again";

        [Shown("Re-enter Account Number", "not the same as the account number", InBrowser = true)]
        public const string AccountMismatch =
            "Does not match the account number";

        [Shown("Cheque Number", "not six digits")]
        public const string ChequeNumberInvalid =
            "Enter the six-digit cheque number";

        [Shown("Cheque Date", "left empty")]
        public const string ChequeDateRequired =
            "Enter the cheque date";

        [Shown("Cheque Date", "not a real date")]
        public const string ChequeDateNotReal =
            "Enter the cheque date as a real date, DD/MM/YYYY";

        [Shown("Axis CMS Branch", "none searched and picked")]
        public const string CmsBranchRequired =
            "Search for the Axis CMS branch and pick it";

        [Shown("Axis CMS Branch", "typed but not picked from the search")]
        public const string CmsBranchNotPicked =
            "Pick an Axis CMS branch from the search";

        [Shown("Repayment bank", "paid online, and the bank is not on the payment gateway")]
        public static string BankNotOnGateway(object? bank) =>
            $"{bank} is not available on our payment gateway for online payment. What to do: choose a repayment account with a bank that is, or change the payment mode to RTGS or Cheque on Upload Documents.";

        [Shown("Repayment bank", "said in place of the bank's name when it is not known")]
        public const string ThisBank =
            "This bank";

        [Shown("Bank", "no branch matches what is typed", InBrowser = true)]
        public static string NoBranchMatches(object? typed) =>
            $"No branch matches “{typed}”";
    }

    /// <summary>The amount, tenure, payout, renewal and source of funds, and the rate card.</summary>
    [Page("FD Configuration")]
    public static class FdConfiguration
    {
        [Shown("Deposit amount", "left empty")]
        public const string AmountRequired =
            "Required — enter the deposit amount";

        [Shown("Deposit amount", "under the minimum")]
        public static string BelowMinimum(object? minimum) =>
            $"Below the {minimum} minimum";

        [Shown("Deposit amount", "over the maximum, when the settings carry no message of their own")]
        public static string AboveMaximum(object? maximum) =>
            $"Above the {maximum} maximum";

        [Shown("Deposit amount", "not a multiple of the step")]
        public static string NotAMultiple(object? step, object? amountInWords) =>
            $"Not a multiple of {step} — {amountInWords}";

        [Shown("What of the deposit is renewed", "not chosen on a renewal")]
        public const string RenewalOfRequired =
            "Required — choose what of the deposit is renewed";

        [Shown("What of the deposit is renewed", "principal chosen, but the deposit's principal is not known")]
        public const string PrincipalNotKnown =
            "The deposit's principal is not known — renew principal and interest";

        [Shown("Tenure", "not chosen")]
        public const string TenureRequired =
            "Choose the tenure";

        [Shown("Tenure", "not on the rate card for the amount")]
        public static string TenureNotOffered(object? months) =>
            $"A {months}-month deposit is not offered for this amount";

        [Shown("The rate note", "the tenure is not on the rate card for the amount")]
        public static string TenureNotOfferedChooseAnother(object? months) =>
            $"A {months}-month deposit is not offered for this amount. Choose another tenure, or change the amount.";

        [Shown("Interest payout", "not chosen")]
        public const string PayoutRequired =
            "Choose the interest payout";

        [Shown("Interest payout", "not on the rate card for the amount")]
        public static string PayoutNotOffered(object? payout) =>
            $"A {payout} payout is not offered for this amount";

        [Shown("The rate note", "the payout is not on the rate card for the amount")]
        public static string PayoutNotOfferedChooseAnother(object? payout) =>
            $"A {payout} payout is not offered for this amount. Choose another payout, or change the amount.";

        [Shown("What auto renewal renews", "auto renewal is on and nothing is chosen")]
        public const string AutoRenewalOfRequired =
            "Required — choose what auto renewal renews";

        [Shown("Delivery type", "not chosen")]
        public const string DeliveryTypeRequired =
            "Choose the delivery type";

        [Shown("Source of funds", "asked for and not chosen")]
        public const string SourceOfFundsRequired =
            "Required — choose the source of funds";

        [Shown("Source of funds remark", "the source chosen takes a remark and none is typed")]
        public const string SourceOfFundsRemarkRequired =
            "Required — say what the source of funds is";

        [Shown("Form 121", "no TDS is claimed and the form is not filed")]
        public const string TdsFormRequired =
            "Upload the Form 121 before proceeding, or turn the switch off";

        [Shown("Proceed", "the rate card has no row for the payout, tenure and amount")]
        public static string NoSuchPayoutOnCard(object? payout, object? months, object? amount) =>
            $"The rate card offers no {payout} payout for {months} months on {amount}. Change the tenure, the payout or the amount.";

        [Shown("Proceed", "no scheme in effect on the rate card fits the deposit")]
        public static string NotOnRateCard(object? scheme, object? category, object? applicationKind, object? payout, object? months, object? rate, object? amount) =>
            $"This deposit is not on the rate card: no {scheme} scheme is in effect for category {category} and {applicationKind} that pays {payout} for {months} months at {rate}% on {amount}. Check the category on Upload Documents, and the amount, tenure and payout here.";

        [Shown("The rate note", "the quote could not be fetched", InBrowser = true)]
        public const string QuoteNotFetched =
            "The quote could not be fetched just now — it is asked for again with the next change.";

        [Shown("Deposit amount", "over the maximum while typing, when the settings carry no message of their own", InBrowser = true)]
        public const string AboveMaximumShort =
            "Above the maximum";

        [Shown("Deposit amount", "not a multiple of the step, while typing", InBrowser = true)]
        public static string NotAMultipleOf(object? step) =>
            $"Not a multiple of ₹ {step}";
    }

    /// <summary>What is still missing before submitting, and a link that could not be sent.</summary>
    [Page("Review Summary and Application Submitted")]
    public static class ReviewSummary
    {
        [Shown("Generate link (Application Submitted)", "the application is paid, cancelled or past its window")]
        public const string LinkNotSent =
            "A link could not be sent: the application is paid, cancelled, or past its window.";

        [Shown("The payment link", "the shortener did not answer, so the full link goes out")]
        public const string LinkNotShortened =
            "The payment link could not be shortened, so the investor gets it in full.";

        [Shown("Submit Application", "something is still missing on a step")]
        public const string StillMissing =
            "Something is still missing, so the application was not submitted.";

        [Shown("Still missing", "a holder's communication address is not typed")]
        public static string MissingMailAddress(object? holder) =>
            $"the communication address of {holder}";

        [Shown("Still missing", "no bank accounts saved")]
        public const string MissingAccounts =
            "the payment and repayment accounts";

        [Shown("Still missing", "the payment account has a problem")]
        public const string MissingPaymentBank =
            "the payment bank and its account number";

        [Shown("Still missing", "the repayment account has a problem")]
        public const string MissingRepaymentBank =
            "the repayment bank and its account number";

        [Shown("Still missing", "the cheque details have a problem")]
        public const string MissingCheque =
            "the cheque details";

        [Shown("Still missing", "no TDS is claimed and the Form 121 is not filed")]
        public const string MissingTdsForm =
            "the Form 121";

        [Shown("Still missing", "no deposit amount saved")]
        public const string MissingDeposit =
            "the deposit";

        [Shown("Still missing", "the source of funds is asked for and not chosen")]
        public const string MissingSourceOfFunds =
            "the source of funds";

        [Shown("Still missing", "a holder has no name")]
        public const string MissingName =
            "the name";

        [Shown("Still missing", "a holder has no PAN")]
        public const string MissingPan =
            "the PAN";

        [Shown("Still missing", "a holder has no date of birth")]
        public const string MissingDob =
            "the date of birth";

        [Shown("Still missing", "a holder has no gender")]
        public const string MissingGender =
            "the gender";

        [Shown("Still missing", "a holder has no marital status")]
        public const string MissingMaritalStatus =
            "the marital status";

        [Shown("Still missing", "a holder has no father, mother or spouse name")]
        public const string MissingParentName =
            "the father, mother or spouse name";

        [Shown("Still missing", "a holder has no mobile number")]
        public const string MissingMobile =
            "the mobile number";

        [Shown("Still missing", "a holder has no e-mail")]
        public const string MissingEmail =
            "the e-mail";

        [Shown("Still missing", "a holder's permanent address has no first line")]
        public const string MissingAddressLine1 =
            "the first line of the permanent address";

        [Shown("Still missing", "a holder's permanent address has no city")]
        public const string MissingCity =
            "the city of the permanent address";

        [Shown("Still missing", "a holder's permanent address has no PIN code")]
        public const string MissingPinCode =
            "the PIN code of the permanent address";

        [Shown("Still missing", "a joint holder has no photograph filed")]
        public const string MissingPhoto =
            "the photograph";

        [Shown("Still missing", "a joint holder has no proof of address filed")]
        public const string MissingPoa =
            "the proof of address";

        [Shown("Still missing", "said of a holder: what is missing, then whose")]
        public static string MissingOf(object? what, object? whose) =>
            $"{what} of {whose}";

        [Shown("Still missing", "said in place of \"holder 2\" for the first holder")]
        public const string TheInvestor =
            "the investor";

        [Shown("Still missing", "said of a joint holder, by their number")]
        public static string HolderNumber(object? number) =>
            $"holder {number}";
    }

    /// <summary>The searches on the list pages and what they find nothing for.</summary>
    [Page("View Application and Pay-in Slips")]
    public static class Lists
    {
        [Shown("Generate slip", "no slip can be made for the application")]
        public const string NoSlip =
            "No slip could be generated for this application.";

        [Shown("Send link", "the acceptance link cannot be sent for the application")]
        public const string AcceptanceLinkNotSent =
            "The acceptance link could not be sent for this application.";

        [Shown("Application Number", "fewer than four characters typed", InBrowser = true)]
        public const string AppNoTooShort =
            "Enter at least the last four characters of the application number.";

        [Shown("Folio Number", "fewer than four characters typed", InBrowser = true)]
        public const string FolioTooShort =
            "Enter at least the last four characters of the folio number.";

        [Shown("From Date", "not a complete date", InBrowser = true)]
        public const string FromDateIncomplete =
            "Enter a complete From Date as DD / MM / YYYY.";

        [Shown("To Date", "not a complete date", InBrowser = true)]
        public const string ToDateIncomplete =
            "Enter a complete To Date as DD / MM / YYYY.";

        [Shown("From Date", "later than the To Date", InBrowser = true)]
        public const string FromAfterTo =
            "The From Date cannot be after the To Date.";

        [Shown("To Date", "a date still to come", InBrowser = true)]
        public const string ToInFuture =
            "The To Date cannot be in the future.";

        [Shown("The list (View Application)", "no application matches the number searched", InBrowser = true)]
        public const string NoApplicationByNumberTryFolio =
            "No application matches that number. Check it, or search by folio instead.";

        [Shown("The list (Pay-in Slips)", "no application matches the number searched", InBrowser = true)]
        public const string NoApplicationByNumberTryDate =
            "No application matches that number. Check it, or search by date instead.";

        [Shown("The list (View Application)", "no application is under the folio searched", InBrowser = true)]
        public const string NoApplicationUnderFolio =
            "No application under that folio. A new customer has no folio until their first deposit books — search by application number instead.";

        [Shown("The list", "no application was raised between the dates searched", InBrowser = true)]
        public const string NoApplicationBetweenDates =
            "No application was raised between those two dates.";

        [Shown("From Date (Pay-in Slips)", "earlier than the oldest application a slip can still be made for", InBrowser = true)]
        public static string TooOldForSlip(object? earliest) =>
            $"An application older than that has cancelled itself, so no slip can be made for it. The earliest date you can search from is {earliest}.";

        [Shown("From Date (View Application)", "earlier than a date search reaches back", InBrowser = true)]
        public static string DateSearchReach(object? days, object? earliest) =>
            $"A date search reaches back {days} days, to {earliest}. For anything older, search by application number or folio.";
    }

    /// <summary>A link that could not be sent.</summary>
    [Page("Short URL")]
    public static class ShortUrl
    {
        [Shown("Send link", "no link can be sent for the application")]
        public const string NoLinkSent =
            "No link could be sent for this application.";
    }

    /// <summary>A deposit that cannot be renewed, and a renewal request that cannot be cancelled.</summary>
    [Page("Renew FD")]
    public static class RenewFd
    {
        [Shown("Renew", "the deposit is no longer open for renewal")]
        public static string CannotRenewNow(object? number) =>
            $"Deposit {number} cannot be renewed now. The list shows it as it stands.";

        [Shown("Cancel renewal", "the deposit has no renewal request")]
        public static string NoRenewalToCancel(object? number) =>
            $"There is no renewal request for deposit {number} to cancel.";

        [Shown("Cancel renewal", "another partner entered the renewal")]
        public const string CancelOnlyByWhoEntered =
            "Entered by another partner: only they can cancel it.";

        [Shown("Cancel renewal", "a physical application is already submitted")]
        public const string CancelNotForPhysical =
            "Not applicable to a physical application once it is submitted.";

        [Shown("Cancel renewal", "the investor has accepted the renewal")]
        public const string CancelNotAfterAcceptance =
            "The investor has accepted it, so it can no longer be cancelled here.";

        [Shown("Cancel renewal", "pressed for a renewal not submitted yet (asks first)")]
        public static string ConfirmCancelRenewalDraft(object? number) =>
            $"Cancel the renewal request for deposit {number}? Its application is dropped, and the deposit is due for renewal again.";

        [Shown("Cancel renewal", "pressed for a renewal already submitted (asks first)")]
        public static string ConfirmCancelRenewal(object? number) =>
            $"Cancel the renewal request for deposit {number}? Its application is cancelled, and the deposit is due for renewal again.";
    }

    /// <summary>A window or a notice that was not set.</summary>
    [Page("Console Admin")]
    public static class Admin
    {
        [Shown("Disable tiles for a window", "no tile picked, or the From is not before the To and in the future")]
        public const string WindowNotSet =
            "The window was not set: pick a tile, and a From in the future before the To.";

        [Shown("Publish a notice", "no heading, or a moment already past")]
        public const string NoticeNotPublished =
            "The notice was not published: give it a heading and a moment still to come.";

        [Shown("End a window", "the window has ended already")]
        public const string WindowAlreadyEnded =
            "That window has already ended.";

        [Shown("Remove a notice", "the notice is no longer in the bell")]
        public const string NoticeAlreadyGone =
            "That notice is no longer in the bell.";

        [Shown("Tiles to disable", "none picked", InBrowser = true)]
        public const string PickATile =
            "Pick at least one tile to disable.";

        [Shown("From", "not a complete date and time", InBrowser = true)]
        public const string FromIncomplete =
            "Enter a complete From as DD / MM / YYYY and HH : MM.";

        [Shown("To", "not a complete date and time", InBrowser = true)]
        public const string ToIncomplete =
            "Enter a complete To as DD / MM / YYYY and HH : MM.";

        [Shown("To", "not after the From", InBrowser = true)]
        public const string EndsBeforeItStarts =
            "The window has to end after it starts.";

        [Shown("Notice date and time", "not a complete date and time", InBrowser = true)]
        public const string NoticeTimeIncomplete =
            "Enter a complete date and time as DD / MM / YYYY and HH : MM.";

        [Shown("Notice heading", "left empty", InBrowser = true)]
        public const string NoticeHeadingMissing =
            "Write the notice partners will see.";
    }

    /// <summary>What a check outside the app says when it cannot answer: NSDL, IDfy, CKYC, masking, name match, the shortener.</summary>
    [Page("Outside services")]
    public static class OutsideServices
    {
        [Shown("Any outside check", "the service cannot be reached")]
        public static string NotAnswering(object? service) =>
            $"{service} is not answering. Try again in a while.";

        [Shown("Any outside check", "the service did not answer in time")]
        public static string TookTooLong(object? service) =>
            $"{service} took too long to answer. Try again in a while.";

        [Shown("Any outside check", "the answer could not be read")]
        public static string NotReadable(object? service) =>
            $"{service} answered with something that could not be read. Try again in a while.";

        [Shown("Any outside check", "the answer was empty")]
        public static string AnsweredNothing(object? service) =>
            $"{service} answered with nothing. Try again in a while.";

        [Shown("The PAN check (NSDL)", "the service is down")]
        public static string NotAvailable(object? service) =>
            $"{service} is not available just now. Try again in a while.";

        [Shown("The PAN check (NSDL)", "the service refused the check and said why")]
        public static string PanCheckFailedWith(object? service, object? why) =>
            $"{service} could not be made: {why}";

        [Shown("The PAN check (NSDL)", "the service refused the check without a reason")]
        public static string PanCheckFailed(object? service) =>
            $"{service} could not be made just now. Try again in a while.";

        [Shown("Name match", "the service refused and said why")]
        public static string NamesNotComparedWith(object? service, object? why) =>
            $"{service} could not compare the names: {why}";

        [Shown("Name match", "the service refused without a reason")]
        public static string NamesNotCompared(object? service) =>
            $"{service} could not compare the names just now. Try again in a while.";

        [Shown("Name match", "the gateway answered 403")]
        public static string RefusedByGateway(object? service) =>
            $"{service} was refused by the gateway (403). Operations have to look at its access.";

        [Shown("The document check (IDfy)", "the service cannot be reached")]
        public const string DocumentCheckNotAnswering =
            "The document check is not answering. Try again in a while.";

        [Shown("The document check (IDfy)", "the service did not answer in time")]
        public const string DocumentCheckTookTooLong =
            "The document check took too long to answer. Try again in a while.";

        [Shown("The document check (IDfy)", "the answer could not be read")]
        public const string DocumentCheckNotReadable =
            "The document check answered with something that could not be read. Try again in a while.";

        [Shown("The document check (IDfy)", "the check did not complete on the copy")]
        public const string DocumentCheckIncomplete =
            "The document check could not complete on this copy. Upload a clearer copy.";

        [Shown("The document check (IDfy)", "the copy is over the size the service takes")]
        public const string CopyTooLarge =
            "The copy is too large for the document check. Keep it under about 2 MB.";

        [Shown("The document check (IDfy)", "the file is not a kind the service takes")]
        public const string WrongKindOfFile =
            "The document check does not take this kind of file. Upload a JPEG.";

        [Shown("The document check (IDfy)", "the copy's resolution is refused and the service gives no detail")]
        public const string ResolutionOutside =
            "The copy's resolution is outside what the document check takes.";

        [Shown("The document check (IDfy)", "the service refused the copy without a reason")]
        public const string CopyNotUsable =
            "The document check could not use this copy.";

        [Shown("The document check (IDfy)", "the service is taking too many requests")]
        public const string DocumentCheckBusy =
            "The document check is busy. Try again in a minute.";

        [Shown("The document check (IDfy)", "an Aadhaar is sent without the investor's consent")]
        public const string NoConsentToRead =
            "An Aadhaar is only read or checked with the investor's consent, and none has been given.";

        [Shown("Aadhaar masking", "an Aadhaar is sent without the investor's consent")]
        public const string NoConsentToMask =
            "An Aadhaar is only masked with the investor's consent, and none has been given.";

        [Shown("Aadhaar masking", "the service refused the copy and said why")]
        public static string NotMaskedBecause(object? why) =>
            $"The Aadhaar could not be masked: {why}";

        [Shown("Aadhaar masking", "the service refused the copy without a reason")]
        public const string NotMasked =
            "The Aadhaar could not be masked. Upload a clearer copy.";

        [Shown("Aadhaar masking", "the service answered with an error status")]
        public static string MaskingFailed(object? service, object? status) =>
            $"{service} could not mask the copy ({status}). Try again in a while.";

        [Shown("The link shortener", "the service answered without a short link")]
        public const string NoShortLink =
            "The link shortener answered with no short link. Try again in a while.";

        [Shown("The document check (IDfy)", "the service refused the copy and said why")]
        public static string CopyNotUsableBecause(object? detail) =>
            $"The document check could not use this copy: {detail}";
    }
}
