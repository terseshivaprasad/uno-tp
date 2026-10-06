# Uno TP: validations and error messages

Every validation and error message the app shows, page by page: the field it
belongs to, when it is shown, and its words.

**This file is written from the master, `UnoTP.Data/Messages.cs`. Do not edit it.**

To change what a message says:

1. Find it below, and note its page and name (for example `BankDetails.AccountInvalid`).
2. Open `UnoTP.Data/Messages.cs`, go to that page and name, and change the words.
   Keep any `{value}` in braces: the app puts the value there.
3. Write this file again: `UPDATE_VALIDATIONS=1 dotnet test UnoTP.sln --filter ValidationListTests`
4. Build, and replace `UnoTP.dll` and `UnoTP.Data.dll`.

"Browser: yes" marks a message a script shows as the partner types. It is still
written only in the master, which hands it to the page.

302 messages.

## Every page

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| A code | anything but letters and digits | Letters and digits only | `Shared.LettersAndDigitsOnly` | yes |
| A date | a date still to come | Enter a date that is not in the future | `Shared.DateInFuture` | yes |
| A date | not a real date | Enter a real date, DD/MM/YYYY | `Shared.DateNotReal` | yes |
| A name | anything but letters | Enter letters only | `Shared.LettersOnly` | yes |
| A search-as-you-type field | the search could not be answered | Could not search just now. Check the connection and type again. | `Shared.SearchFailed` | yes |
| An address line | a character an address does not take | No special characters | `Shared.NoSpecialCharacters` | yes |
| An upload or a check | more tries in a minute than are allowed | That is more than {perMinute} tries in a minute. Wait a minute, then do it again. Nothing was sent this time, and no attempt was used. | `Shared.TooManyTries` |  |
| Any request | an error nobody caught, and the error page failed too | Uno TP could not finish that. Reference: {reference} | `Shared.CouldNotFinish` |  |
| Any request | the request carries input no page takes | The request carried input this page does not take. | `Shared.InputRefused` |  |
| Any request | an error nobody caught, answered to a script | Something went wrong. | `Shared.SomethingWentWrong` |  |
| Any typed text | a character other than a letter, digit, space or - , & / . | Only letters, digits, spaces and - , & / . are allowed | `Shared.OnlyAllowedCharacters` |  |
| Any typed text | longer than the field takes | At most {max} characters | `Shared.TooLong` |  |
| Leaving a step | the step has changes that are not saved (asks before leaving) | This step has changes that are not saved yet. Leave it without saving them? | `Shared.UnsavedChanges` | yes |
| The error page | an error nobody caught | Uno TP could not finish that. Nothing you saved is lost: go back and try again, and if it happens again, give support the reference below. | `Shared.CouldNotFinishPage` |  |
| The whole step | the application was changed in another window while this one was being sent | This application changed somewhere else while that was being sent, so it was not kept. The page shows it as it stands now — do it again. | `Shared.ChangedElsewhere` |  |

## Coming in from the portal

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| A page | the user has no access to it | You do not have access to this page. | `SignIn.NoAccess` |  |
| The Session Expired page | the session ended, or the app was not opened from the portal | Open Uno TP from the portal again. A draft you saved is kept, and opens from Investor Identification. | `SignIn.SessionEndedWhatNext` |  |
| The Session Expired page | the session ended, or the app was not opened from the portal | For your security, Uno TP signs you out when your session ends, or when it was not opened from the portal. | `SignIn.SessionEndedWhy` |  |
| The Too Many Requests page | more requests in a minute than are taken | Wait a minute, then go back and do it again. Everything saved before it is kept. | `SignIn.TooManyRequestsWhatNext` |  |
| The Too Many Requests page | more requests in a minute than are taken | Uno TP received more requests from you in a minute than it takes, so the last one was not carried out. | `SignIn.TooManyRequestsWhy` |  |
| The link from the portal | its user or system code is missing | The link from the portal is missing its user or system code. | `SignIn.LinkIncomplete` |  |
| The link from the portal | what it carries could not be decrypted | The link from the portal could not be read. Open Uno TP from the portal again. | `SignIn.LinkNotRead` |  |
| The link from the portal | the decryption service did not answer | The portal's details could not be read just now. Try opening Uno TP from the portal again in a while. | `SignIn.PortalNotReadNow` |  |
| The menu | Uno TP is not on the user's menu | Your menu does not include Uno TP. Ask your administrator for access. | `SignIn.NotOnMenu` |  |
| The session | the portal started none for this user | The portal did not start a session for you on Uno TP. Ask your administrator for access. | `SignIn.NoSessionFromPortal` |  |
| The session | the sign-in service did not answer | Uno TP could not start your session just now. Try again in a while. | `SignIn.SessionNotStarted` |  |

## Investor Identification

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Cancel (an incomplete application) | pressed (asks first) | Cancel application {applicationNumber}? It is dropped and cannot be continued again. | `InvestorIdentification.ConfirmCancelDraft` |  |
| Cancel (an incomplete application) | the application is not there to cancel | There is no such incomplete application to cancel. | `InvestorIdentification.NoDraftToCancel` |  |
| Date of Birth | not a real date | Enter a valid date | `InvestorIdentification.DateInvalid` |  |
| Date of Birth | a date still to come | The date of birth cannot be in the future | `InvestorIdentification.DobInFuture` |  |
| Date of Birth | a part of the date left empty | Enter the date of birth | `InvestorIdentification.DobRequired` |  |
| Date of Birth | the depositor is younger than the minimum age | The depositor must be {minimumAge} years or above | `InvestorIdentification.UnderAge` |  |
| Date of Birth | the year has fewer than four digits | Enter the year in full | `InvestorIdentification.YearInFull` |  |
| Folio Number | the register holds no such folio | No record against that folio number — check it, or search by PAN instead | `InvestorIdentification.FolioNotFound` |  |
| Folio Number | left empty | Enter the folio number | `InvestorIdentification.FolioRequired` |  |
| PAN | not five letters, four digits and a letter | Enter a valid PAN, like ABCDE1234F | `InvestorIdentification.PanInvalid` | yes |
| PAN | left empty | Enter the PAN | `InvestorIdentification.PanRequired` |  |
| The record found | the date of birth searched is not the folio's | The DOB for the existing folio does not match the DOB provided for the search. | `InvestorIdentification.DobDoesNotMatchFolio` |  |
| The record found | a folio is held against the PAN, so the investor is not new | A folio is already held against the provided PAN, so the investor cannot go on as new. Please check with Mahindra Finance Fixed Deposit Team. | `InvestorIdentification.ExistingHolder` |  |
| The record found | the folio has a deposit but no record of its own | Folio {folio} has a deposit against PAN {pan}, but the folio's own record (name and address) could not be found. Operations has to look at the record before a deposit can be booked. | `InvestorIdentification.FolioRecordMissing` |  |
| The record found | the PAN is first holder of a folio with no deposit on it | The folio master holds PAN {pan} as the first holder of folio {folio}, but no deposit of theirs was found on it. Operations has to look at the record before a deposit can be booked. | `InvestorIdentification.FolioWithoutDeposit` |  |
| The record found | several folios are held against the PAN | Multiple folios found against the provided PAN. Please check with Mahindra Finance Fixed Deposit Team. | `InvestorIdentification.ManyFolios` |  |
| The record found | several folios are held against the PAN | Folios {folios} and {lastFolio} are all held against PAN {pan}. Operations has to merge them before a deposit can be booked against the PAN. | `InvestorIdentification.ManyFoliosToMerge` |  |
| The record found | the folio has no date of birth | DOB is not updated for Existing Folio. | `InvestorIdentification.NoDobOnFolio` |  |
| The record found | the register holds the PAN without a date of birth | The register holds PAN {pan} (folio {folio}) without a date of birth, so the investor's age cannot be checked. Operations has to add it to the record before a deposit can be booked. | `InvestorIdentification.NoDobOnRegister` |  |
| The record found | the folio belongs to a non-individual | Deposits from non-individual entities are prohibited. | `InvestorIdentification.NonIndividual` |  |

## Upload Documents

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| A document | Proceed pressed without it | This document is required | `UploadDocuments.DocumentRequired` |  |
| A document (upload) | the copy cannot be read | The copy could not be read. Upload a sharper scan, with the whole card in view. | `UploadDocuments.CopyUnreadable` |  |
| A document (upload) | the file is not really a PDF or JPEG | That file is not a readable PDF or JPEG | `UploadDocuments.FileNotReadable` |  |
| A document (upload) | over the size the document takes | The file is over {maxMb} MB — {size} | `UploadDocuments.FileTooLarge` |  |
| A document (upload) | not a file type the document takes | That file type is not accepted here | `UploadDocuments.FileTypeNotAccepted` |  |
| A document (upload) | no file chosen | Choose a file to upload | `UploadDocuments.NoFileChosen` |  |
| A document (upload) | the copy is not the document asked for | That does not read as {document} | `UploadDocuments.NotReadAs` |  |
| A document (upload) | an outside check could not answer, so nothing was filed | {why} Nothing was filed, and it does not count as a refusal. | `UploadDocuments.NothingFiled` |  |
| A document (upload) | the copy is another document than the one asked for | It reads as {found}. Upload {wanted} itself. | `UploadDocuments.ReadsAsAnother` |  |
| A document (upload) | refused too many times in a row | Refused too many times in a row: try again after some time. | `UploadDocuments.RefusedTooOften` |  |
| A document (upload) | the copy is not the document asked for, and nothing more is known | Upload a clearer copy. | `UploadDocuments.UploadClearerCopy` |  |
| A refused copy | said of the first holder, inside "Upload … own PAN card" | the investor's | `UploadDocuments.TheInvestors` |  |
| A refused copy | said of a joint holder, inside "Upload … own PAN card" | this holder's | `UploadDocuments.ThisHolders` |  |
| Aadhaar number | not 12 digits | Enter the 12-digit Aadhaar number | `UploadDocuments.Aadhaar12Required` |  |
| Aadhaar number | the first 8 digits are not all typed | Enter the first 8 digits of the Aadhaar number | `UploadDocuments.AadhaarFirst8Required` |  |
| Aadhaar number | not a valid number | That is not a valid Aadhaar number — check it against the card | `UploadDocuments.AadhaarInvalid` |  |
| Aadhaar number | not a valid number, with the last 4 read off the copy | That is not a valid Aadhaar number — check the first 8 digits against the card. If its last 4 are not {lastFour}, upload the Aadhaar again | `UploadDocuments.AadhaarInvalidCheckFirst8` |  |
| Broker / employee code | nothing on the register matches what is typed | Nothing on the register matches “{typed}” | `UploadDocuments.NothingOnRegister` | yes |
| Broker / employee code | left empty | Enter the {codeLabel} | `UploadDocuments.SourceCodeRequired` |  |
| Communication Address | switching to Same as Permanent with a proof filed (asks first) | Post will go to the permanent address, and the communication address proof uploaded will be removed. Switch to Same as Permanent? | `UploadDocuments.ConfirmDropMailProof` |  |
| Communication address proof (upload) | no proof type chosen yet | Choose the communication address proof first | `UploadDocuments.ChooseMailTypeFirst` |  |
| Communication address proof type | not chosen | Choose the communication address proof | `UploadDocuments.MailTypeRequired` |  |
| Deposit Category | none is set because no sourcing mode is chosen | No deposit category is set: choose the sourcing mode | `UploadDocuments.CategoryNotSet` |  |
| Employee Code | left empty | Enter the employee code | `UploadDocuments.EmployeeCodeRequired` |  |
| Employee Proof | not chosen | Choose the employee proof | `UploadDocuments.EmployeeProofRequired` |  |
| Employee company | left empty | Enter the employee company name | `UploadDocuments.EmployeeCompanyRequired` |  |
| Employee holder | not chosen | Choose which holder is the employee | `UploadDocuments.EmployeeHolderRequired` |  |
| Employee proof (upload) | no proof type chosen yet | Choose the employee proof first | `UploadDocuments.ChooseEmployeeProofFirst` |  |
| Fetch from CKYC | CERSAI holds no record for the PAN and date of birth | CERSAI holds no CKYC record for this PAN and date of birth{why}. Upload the proof of address and the photograph instead. | `UploadDocuments.CkycNoRecord` |  |
| Fetch from CKYC | the CKYC service could not answer | {why} The KYC was not fetched; upload the proof of address and the photograph, or try again. | `UploadDocuments.CkycNotFetched` |  |
| Fetch from CKYC | the CKYC service is switched off | Unavailable: the CKYC service is switched off for now. Upload the proof of address and the photograph instead. | `UploadDocuments.CkycSwitchedOff` |  |
| Fetch from CKYC | NSDL has not verified the PAN copy yet | Available once NSDL verifies the uploaded PAN copy. | `UploadDocuments.CkycWaitsOnPan` |  |
| Instrument copy (upload) | no payment mode chosen yet | Choose the payment mode first | `UploadDocuments.ChoosePayModeFirst` |  |
| Name as on the PAN | NSDL does not hold the PAN against the name typed | NSDL does not hold PAN {pan} against that name | `UploadDocuments.NsdlNameNotHeld` |  |
| Name as on the PAN | NSDL holds no such PAN and date of birth | NSDL holds no record of PAN {pan} against the date of birth searched | `UploadDocuments.NsdlNoRecord` |  |
| Name as on the PAN | anything but letters | Enter the name as printed on the PAN: letters only | `UploadDocuments.PanNameLettersOnly` |  |
| Name as on the PAN | fewer than three letters typed | Enter the name as printed on the PAN | `UploadDocuments.PanNameRequired` |  |
| PAN copy | OCR cannot read the date of birth | OCR could not read the date of birth on it | `UploadDocuments.DobNotReadOnCopy` |  |
| PAN copy | the date of birth read is not the one entered | The date of birth on it does not match the one entered ({entered}) | `UploadDocuments.DobNotTheOneEntered` |  |
| PAN copy | OCR cannot read the PAN number | OCR could not read the PAN number on it | `UploadDocuments.PanNotReadOnCopy` |  |
| PAN copy | the PAN read is not the one on the application | The PAN on it reads as {read}, not {entered} | `UploadDocuments.PanReadsAsAnother` |  |
| PAN copy | the PAN or date of birth read off it is not the one on the application | Upload {whose} own PAN card, clear enough to read. | `UploadDocuments.UploadOwnPan` |  |
| PAN-Aadhaar link | the link service could not answer | The PAN-Aadhaar link could not be asked just now: {why} | `UploadDocuments.LinkNotAskedNow` |  |
| Payment Mode | not chosen | Choose the payment mode | `UploadDocuments.PayModeRequired` |  |
| Photograph | the file cannot be read as a picture | That file could not be read as a photograph. | `UploadDocuments.PhotoNotReadable` |  |
| Photograph | fewer pixels a side than the minimum | That photograph is {width}×{height} pixels. It must be at least {minimumPixels} pixels on both sides. | `UploadDocuments.PhotoTooFewPixels` |  |
| Photograph | over the largest size a photograph may be | That photograph is {kb} KB, over the {maximumKb} KB a photograph may be. | `UploadDocuments.PhotoTooLarge` |  |
| Photograph | more pixels a side than can be handled | That photograph is {width}×{height} pixels, over the {maximumPixels} a side that can be handled. | `UploadDocuments.PhotoTooManyPixels` |  |
| Photograph | under the smallest size a face can be compared at | That photograph is {kb} KB. A face cannot be compared against anything under {minimumKb} KB. | `UploadDocuments.PhotoTooSmall` |  |
| Physical Application Form No. | left empty on a physical application | Enter the physical form number | `UploadDocuments.FormNoRequired` |  |
| Proceed | the PAN-Aadhaar link is not confirmed | The PAN-Aadhaar link must be confirmed before proceeding. | `UploadDocuments.LinkMustBeConfirmed` |  |
| Proceed | the PAN-Aadhaar link could not be checked | The PAN-Aadhaar link could not be checked. Upload the Aadhaar again, or type its number, to ask again. | `UploadDocuments.LinkNotChecked` |  |
| Proceed | the name or date of birth on the proof of address is not the PAN's | The name and date of birth on the proof of address must match the PAN's before proceeding. Upload a clearer copy of the holder's own proof. | `UploadDocuments.NameAndDobMustMatch` |  |
| Proceed | the name on the proof of address is not matched with the PAN's yet | The name on the proof of address must be matched with the PAN's before proceeding. | `UploadDocuments.NameMustBeMatched` |  |
| Proceed | NSDL holds no such PAN and date of birth | NSDL holds no such PAN and date of birth. Upload the PAN copy again if both are right. If not: {whatElse} | `UploadDocuments.NsdlFailedUploadAgain` |  |
| Proceed | NSDL could not be asked about the PAN | NSDL could not be asked about the PAN. Upload the PAN copy again | `UploadDocuments.NsdlNotAsked` |  |
| Proceed | the PAN is not linked with Aadhaar | The PAN is not linked with Aadhaar. The investor links it with the Income Tax department; the application cannot proceed until it is. | `UploadDocuments.PanNotLinked` |  |
| Proceed | NSDL holds no such PAN and date of birth, for a joint holder | Remove this holder and search again. | `UploadDocuments.RemoveHolderSearchAgain` |  |
| Proceed | NSDL holds no such PAN and date of birth, for the first holder | Start again from Investor Identification with the right PAN and date of birth. | `UploadDocuments.StartAgainWithRightPan` |  |
| Proceed | the Aadhaar number is still to be typed | Type the Aadhaar number in the row under the proofs of address, so the PAN-Aadhaar link can be asked. | `UploadDocuments.TypeAadhaarNumber` |  |
| Proceed | NSDL holds the PAN under another name | Type the name as printed on the PAN, and ask NSDL again | `UploadDocuments.TypeNameAskAgain` |  |
| Proof of address | the address cannot be read | The address could not be read off it | `UploadDocuments.AddressNotRead` |  |
| Proof of address | a PAN card is filed as the proof | It reads as a PAN card, which is not a proof of address. Upload an Aadhaar, passport, driving licence or voter ID. | `UploadDocuments.NotAProofOfAddress` |  |
| Proof of address | the proof is one that proves only a communication address | A {proof} is not taken as proof of the permanent address | `UploadDocuments.NotProofOfPermanentAddress` |  |
| Proof of address | the PIN code cannot be read | The PIN code could not be read off it | `UploadDocuments.PinNotRead` |  |
| Proof of address | it cannot be told which proof it is | It could not be told which proof of address it is. Upload a clearer copy of {proofs}. | `UploadDocuments.ProofNotIdentified` |  |
| Proof of address | the kind of proof is clear but it cannot be read | It reads as {document}, but could not be read. Upload a sharper scan, with the whole card in view. | `UploadDocuments.ReadsAsButUnreadable` |  |
| Proof of address | the proof is one that proves only a communication address | Upload {whose} {proofs} — a {proof} proves only a communication address. | `UploadDocuments.UploadProofOfPermanentAddress` |  |
| Proof of address | the address or its PIN code cannot be read | Upload a copy of the {proof} that shows the address with its PIN code, clear enough to read. | `UploadDocuments.UploadProofWithAddress` |  |
| Proof of address (Aadhaar) | its date of birth is not the PAN's | The date of birth on the Aadhaar does not match the PAN's | `UploadDocuments.AadhaarDobMismatch` |  |
| Proof of address (Aadhaar) | its date of birth cannot be read | The date of birth on the Aadhaar could not be read | `UploadDocuments.AadhaarDobNotRead` |  |
| Proof of address (Aadhaar) | neither its name nor its date of birth is the PAN's | The name and date of birth on the Aadhaar do not match the PAN's | `UploadDocuments.AadhaarNameAndDobMismatch` |  |
| Proof of address (Aadhaar) | its name is not the PAN's | The name on the Aadhaar does not match the PAN's | `UploadDocuments.AadhaarNameMismatch` |  |
| Proof of address (Aadhaar) | its name cannot be read | The name on the Aadhaar could not be read | `UploadDocuments.AadhaarNameNotRead` |  |
| Proof of address (Aadhaar) | neither the number nor its last 4 digits can be read | The Aadhaar number could not be read off it | `UploadDocuments.AadhaarNumberNotRead` |  |
| Proof of address (Aadhaar) | no PAN copy is filed yet to match the name with | The PAN's name is not known yet, so the Aadhaar cannot be matched with it — file the PAN copy first | `UploadDocuments.PanNameNotKnown` |  |
| Proof of address (Aadhaar) | neither the number nor its last 4 digits can be read | Upload a copy of the Aadhaar that shows its number — all 12 digits, or the last 4 of a masked one — clear enough to read. | `UploadDocuments.UploadAadhaarWithNumber` |  |
| Proof of address (Aadhaar) | its name or date of birth is not the PAN's | Upload {whose} own Aadhaar, clear enough to read the name and date of birth. | `UploadDocuments.UploadOwnAadhaar` |  |
| Proof of address (upload) | no proof type chosen yet | Choose the proof of address first | `UploadDocuments.ChoosePoaTypeFirst` |  |
| Proof of address type | not chosen | Choose the proof of address | `UploadDocuments.PoaTypeRequired` |  |
| Relation with the holder | not chosen | Choose the relation with the holder | `UploadDocuments.EmployeeRelationRequired` |  |
| Sourcing Mode | not chosen | Choose the sourcing mode | `UploadDocuments.SourcingRequired` |  |
| Still missing | no deposit category set | the deposit category | `UploadDocuments.MissingCategory` |  |
| Still missing | no employee code typed | the employee code | `UploadDocuments.MissingEmployeeCode` |  |
| Still missing | no employee company typed | the employee company | `UploadDocuments.MissingEmployeeCompany` |  |
| Still missing | the employee holder is not chosen | the employee holder | `UploadDocuments.MissingEmployeeHolder` |  |
| Still missing | no employee proof filed | the employee proof | `UploadDocuments.MissingEmployeeProof` |  |
| Still missing | no employee proof type chosen | the employee proof type | `UploadDocuments.MissingEmployeeProofType` |  |
| Still missing | the relation with the holder is not chosen | the relation with the holder | `UploadDocuments.MissingEmployeeRelation` |  |
| Still missing | no application form filed | the application form | `UploadDocuments.MissingForm` |  |
| Still missing | no form number typed on a physical application | the form number | `UploadDocuments.MissingFormNo` |  |
| Still missing | no instrument copy filed | the instrument copy | `UploadDocuments.MissingInstrument` |  |
| Still missing | no communication address proof filed | the communication address proof | `UploadDocuments.MissingMail` |  |
| Still missing | no communication address proof type chosen | the communication address proof type | `UploadDocuments.MissingMailType` |  |
| Still missing | NSDL has not verified the PAN | the PAN verified with NSDL | `UploadDocuments.MissingNsdl` |  |
| Still missing | no PAN copy filed | the PAN copy | `UploadDocuments.MissingPan` |  |
| Still missing | no payment mode chosen | the payment mode | `UploadDocuments.MissingPayMode` |  |
| Still missing | no photograph filed | the photograph | `UploadDocuments.MissingPhoto` |  |
| Still missing | no proof of address filed | the proof of address | `UploadDocuments.MissingPoa` |  |
| Still missing | no proof of address type chosen | the proof of address type | `UploadDocuments.MissingPoaType` |  |
| Still missing | no sourcing mode chosen | the sourcing mode | `UploadDocuments.MissingSourcing` |  |
| Still missing | no sub broker code typed | the sub broker code | `UploadDocuments.MissingSubBroker` |  |
| Sub Broker Code | left empty where the sourcing mode asks for it | Enter the sub broker code | `UploadDocuments.SubBrokerRequired` |  |

## Investor Information

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| A PEP question | neither Yes nor No chosen | Choose Yes or No | `InvestorInformation.YesOrNoRequired` |  |
| Address Line 1 | left empty | Enter the first line of the address | `InvestorInformation.AddressLine1Required` |  |
| Annual Income | not chosen | Select the annual income | `InvestorInformation.IncomeRequired` |  |
| City | left empty | Enter the city | `InvestorInformation.CityRequired` |  |
| Clear All | pressed (asks first) | Clear everything typed on this step, and remove the joint holders and the nominee? | `InvestorInformation.ConfirmClearAll` |  |
| E-Mail | not an e-mail address | Enter a valid e-mail | `InvestorInformation.EmailInvalid` | yes |
| E-Mail | left empty | Enter the e-mail | `InvestorInformation.EmailRequired` |  |
| FATCA questions | a holder is a tax or permanent resident of another country | This investments needs to be done through offline mode. Kindly reach out to the nearest Mahindra branch. A list of all our branches is available on our website. | `InvestorInformation.FatcaOffline` |  |
| Father / Mother / Spouse name | left empty | Enter the father's, mother's or spouse's name | `InvestorInformation.ParentNameRequired` |  |
| Gender | not chosen | Select the gender | `InvestorInformation.GenderRequired` |  |
| Guardian name | left empty for a minor nominee | Enter the guardian's name | `InvestorInformation.GuardianNameRequired` |  |
| Joint holder search | the PAN is already a holder on the application | This PAN is already on the application | `InvestorInformation.PanAlreadyOn` |  |
| Marital Status | not chosen | Select the marital status | `InvestorInformation.MaritalStatusRequired` |  |
| Mobile Number | not ten digits starting 6 to 9 | Enter a 10-digit mobile number | `InvestorInformation.MobileInvalid` | yes |
| Mobile Number | left empty | Enter the mobile number | `InvestorInformation.MobileRequired` |  |
| Name type | not chosen | Select the name type | `InvestorInformation.NameTypeRequired` |  |
| Nominee date of birth | not a real date, or one still to come | Enter a real date of birth, not a future one | `InvestorInformation.NomineeDobNotReal` |  |
| Nominee date of birth | a part of the date left empty | Enter the nominee's date of birth | `InvestorInformation.NomineeDobRequired` |  |
| Nominee name | left empty | Enter the nominee's name | `InvestorInformation.NomineeNameRequired` |  |
| Occupation | not chosen | Select the occupation | `InvestorInformation.OccupationRequired` |  |
| PIN code | no district is found for it | No district is found for this PIN code; check it | `InvestorInformation.PinHasNoDistrict` |  |
| PIN code | not six digits | Enter a 6-digit PIN code | `InvestorInformation.PinInvalid` | yes |
| PIN code | no district is found for it, as it is typed | Not found for this PIN code | `InvestorInformation.PinNotFound` | yes |
| PIN code | left empty | Enter the PIN code | `InvestorInformation.PinRequired` |  |
| Proceed | a holder is a tax or permanent resident of another country | Cannot proceed: the investment has to be made offline | `InvestorInformation.StopOfflineTitle` |  |
| Proceed | a holder is a tax or permanent resident of another country | Kindly ask the investor to visit the nearest Mahindra Finance branch to invest offline; a list of all branches is on our website. | `InvestorInformation.StopOfflineWhatNext` |  |
| Proceed | a holder is a tax or permanent resident of another country | A holder is a tax or permanent resident of a country other than India. | `InvestorInformation.StopOfflineWho` |  |
| Proceed | name screening does not allow a holder to invest online | Cannot proceed: not allowed to invest online | `InvestorInformation.StopScreeningTitle` |  |
| Proceed | name screening does not allow a holder to invest online | This investment cannot be made here. Kindly ask the investor to visit the nearest Mahindra Finance branch to invest offline; a list of all branches is on our website. | `InvestorInformation.StopScreeningWhatNext` |  |
| Proceed | name screening does not allow a holder to invest online | Name screening does not allow {holders} to invest online. | `InvestorInformation.StopScreeningWho` |  |
| Relation with primary holder | not chosen | Select the relation with the primary holder | `InvestorInformation.NomineeRelationRequired` |  |
| Sub Occupation | not one that goes with the occupation | Select a sub occupation that goes with the occupation | `InvestorInformation.SubOccupationMismatch` |  |
| Sub Occupation | not chosen | Select the sub occupation | `InvestorInformation.SubOccupationRequired` | yes |

## Bank Details & Payment

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Account Number | not 6 to 18 digits | Enter the account number, 6 to 18 digits | `BankDetails.AccountInvalid` | yes |
| Axis CMS Branch | typed but not picked from the search | Pick an Axis CMS branch from the search | `BankDetails.CmsBranchNotPicked` |  |
| Axis CMS Branch | none searched and picked | Search for the Axis CMS branch and pick it | `BankDetails.CmsBranchRequired` |  |
| Bank | no bank searched and picked | Search for the bank and pick its branch | `BankDetails.BankRequired` |  |
| Bank | no branch has the IFSC typed | No branch has this IFSC — pick one from the search, or check it against the cheque | `BankDetails.IfscNotFound` |  |
| Bank | no branch matches what is typed | No branch matches “{typed}” | `BankDetails.NoBranchMatches` | yes |
| Cheque Date | not a real date | Enter the cheque date as a real date, DD/MM/YYYY | `BankDetails.ChequeDateNotReal` |  |
| Cheque Date | left empty | Enter the cheque date | `BankDetails.ChequeDateRequired` |  |
| Cheque Number | not six digits | Enter the six-digit cheque number | `BankDetails.ChequeNumberInvalid` |  |
| Re-enter Account Number | left empty | Enter the account number again | `BankDetails.AccountAgainRequired` |  |
| Re-enter Account Number | not the same as the account number | Does not match the account number | `BankDetails.AccountMismatch` | yes |
| Repayment bank | paid online, and the bank is not on the payment gateway | {bank} is not available on our payment gateway for online payment. What to do: choose a repayment account with a bank that is, or change the payment mode to RTGS or Cheque on Upload Documents. | `BankDetails.BankNotOnGateway` |  |
| Repayment bank | said in place of the bank's name when it is not known | This bank | `BankDetails.ThisBank` |  |

## FD Configuration

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Delivery type | not chosen | Choose the delivery type | `FdConfiguration.DeliveryTypeRequired` |  |
| Deposit amount | over the maximum, when the settings carry no message of their own | Above the {maximum} maximum | `FdConfiguration.AboveMaximum` |  |
| Deposit amount | over the maximum while typing, when the settings carry no message of their own | Above the maximum | `FdConfiguration.AboveMaximumShort` | yes |
| Deposit amount | left empty | Required — enter the deposit amount | `FdConfiguration.AmountRequired` |  |
| Deposit amount | under the minimum | Below the {minimum} minimum | `FdConfiguration.BelowMinimum` |  |
| Deposit amount | not a multiple of the step | Not a multiple of {step} — {amountInWords} | `FdConfiguration.NotAMultiple` |  |
| Deposit amount | not a multiple of the step, while typing | Not a multiple of ₹ {step} | `FdConfiguration.NotAMultipleOf` | yes |
| Form 121 | no TDS is claimed and the form is not filed | Upload the Form 121 before proceeding, or turn the switch off | `FdConfiguration.TdsFormRequired` |  |
| Interest payout | not on the rate card for the amount | A {payout} payout is not offered for this amount | `FdConfiguration.PayoutNotOffered` |  |
| Interest payout | not chosen | Choose the interest payout | `FdConfiguration.PayoutRequired` |  |
| Proceed | the rate card has no row for the payout, tenure and amount | The rate card offers no {payout} payout for {months} months on {amount}. Change the tenure, the payout or the amount. | `FdConfiguration.NoSuchPayoutOnCard` |  |
| Proceed | no scheme in effect on the rate card fits the deposit | This deposit is not on the rate card: no {scheme} scheme is in effect for category {category} and {applicationKind} that pays {payout} for {months} months at {rate}% on {amount}. Check the category on Upload Documents, and the amount, tenure and payout here. | `FdConfiguration.NotOnRateCard` |  |
| Source of funds | asked for and not chosen | Required — choose the source of funds | `FdConfiguration.SourceOfFundsRequired` |  |
| Source of funds remark | the source chosen takes a remark and none is typed | Required — say what the source of funds is | `FdConfiguration.SourceOfFundsRemarkRequired` |  |
| Tenure | not on the rate card for the amount | A {months}-month deposit is not offered for this amount | `FdConfiguration.TenureNotOffered` |  |
| Tenure | not chosen | Choose the tenure | `FdConfiguration.TenureRequired` |  |
| The rate note | the payout is not on the rate card for the amount | A {payout} payout is not offered for this amount. Choose another payout, or change the amount. | `FdConfiguration.PayoutNotOfferedChooseAnother` |  |
| The rate note | the quote could not be fetched | The quote could not be fetched just now — it is asked for again with the next change. | `FdConfiguration.QuoteNotFetched` | yes |
| The rate note | the tenure is not on the rate card for the amount | A {months}-month deposit is not offered for this amount. Choose another tenure, or change the amount. | `FdConfiguration.TenureNotOfferedChooseAnother` |  |
| What auto renewal renews | auto renewal is on and nothing is chosen | Required — choose what auto renewal renews | `FdConfiguration.AutoRenewalOfRequired` |  |
| What of the deposit is renewed | principal chosen, but the deposit's principal is not known | The deposit's principal is not known — renew principal and interest | `FdConfiguration.PrincipalNotKnown` |  |
| What of the deposit is renewed | not chosen on a renewal | Required — choose what of the deposit is renewed | `FdConfiguration.RenewalOfRequired` |  |

## Review Summary and Application Submitted

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Generate link (Application Submitted) | the application is paid, cancelled or past its window | A link could not be sent: the application is paid, cancelled, or past its window. | `ReviewSummary.LinkNotSent` |  |
| Still missing | said of a joint holder, by their number | holder {number} | `ReviewSummary.HolderNumber` |  |
| Still missing | no bank accounts saved | the payment and repayment accounts | `ReviewSummary.MissingAccounts` |  |
| Still missing | a holder's permanent address has no first line | the first line of the permanent address | `ReviewSummary.MissingAddressLine1` |  |
| Still missing | the cheque details have a problem | the cheque details | `ReviewSummary.MissingCheque` |  |
| Still missing | a holder's permanent address has no city | the city of the permanent address | `ReviewSummary.MissingCity` |  |
| Still missing | no deposit amount saved | the deposit | `ReviewSummary.MissingDeposit` |  |
| Still missing | a holder has no date of birth | the date of birth | `ReviewSummary.MissingDob` |  |
| Still missing | a holder has no e-mail | the e-mail | `ReviewSummary.MissingEmail` |  |
| Still missing | a holder has no gender | the gender | `ReviewSummary.MissingGender` |  |
| Still missing | a holder's communication address is not typed | the communication address of {holder} | `ReviewSummary.MissingMailAddress` |  |
| Still missing | a holder has no marital status | the marital status | `ReviewSummary.MissingMaritalStatus` |  |
| Still missing | a holder has no mobile number | the mobile number | `ReviewSummary.MissingMobile` |  |
| Still missing | a holder has no name | the name | `ReviewSummary.MissingName` |  |
| Still missing | said of a holder: what is missing, then whose | {what} of {whose} | `ReviewSummary.MissingOf` |  |
| Still missing | a holder has no PAN | the PAN | `ReviewSummary.MissingPan` |  |
| Still missing | a holder has no father, mother or spouse name | the father, mother or spouse name | `ReviewSummary.MissingParentName` |  |
| Still missing | the payment account has a problem | the payment bank and its account number | `ReviewSummary.MissingPaymentBank` |  |
| Still missing | a joint holder has no photograph filed | the photograph | `ReviewSummary.MissingPhoto` |  |
| Still missing | a holder's permanent address has no PIN code | the PIN code of the permanent address | `ReviewSummary.MissingPinCode` |  |
| Still missing | a joint holder has no proof of address filed | the proof of address | `ReviewSummary.MissingPoa` |  |
| Still missing | the repayment account has a problem | the repayment bank and its account number | `ReviewSummary.MissingRepaymentBank` |  |
| Still missing | the source of funds is asked for and not chosen | the source of funds | `ReviewSummary.MissingSourceOfFunds` |  |
| Still missing | no TDS is claimed and the Form 121 is not filed | the Form 121 | `ReviewSummary.MissingTdsForm` |  |
| Still missing | said in place of "holder 2" for the first holder | the investor | `ReviewSummary.TheInvestor` |  |
| Submit Application | something is still missing on a step | Something is still missing, so the application was not submitted. | `ReviewSummary.StillMissing` |  |
| The payment link | the shortener did not answer, so the full link goes out | The payment link could not be shortened, so the investor gets it in full. | `ReviewSummary.LinkNotShortened` |  |

## View Application and Pay-in Slips

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Application Number | fewer than four characters typed | Enter at least the last four characters of the application number. | `Lists.AppNoTooShort` | yes |
| Folio Number | fewer than four characters typed | Enter at least the last four characters of the folio number. | `Lists.FolioTooShort` | yes |
| From Date | later than the To Date | The From Date cannot be after the To Date. | `Lists.FromAfterTo` | yes |
| From Date | not a complete date | Enter a complete From Date as DD / MM / YYYY. | `Lists.FromDateIncomplete` | yes |
| From Date (Pay-in Slips) | earlier than the oldest application a slip can still be made for | An application older than that has cancelled itself, so no slip can be made for it. The earliest date you can search from is {earliest}. | `Lists.TooOldForSlip` | yes |
| From Date (View Application) | earlier than a date search reaches back | A date search reaches back {days} days, to {earliest}. For anything older, search by application number or folio. | `Lists.DateSearchReach` | yes |
| Generate slip | no slip can be made for the application | No slip could be generated for this application. | `Lists.NoSlip` |  |
| Send link | the acceptance link cannot be sent for the application | The acceptance link could not be sent for this application. | `Lists.AcceptanceLinkNotSent` |  |
| The list | no application was raised between the dates searched | No application was raised between those two dates. | `Lists.NoApplicationBetweenDates` | yes |
| The list (Pay-in Slips) | no application matches the number searched | No application matches that number. Check it, or search by date instead. | `Lists.NoApplicationByNumberTryDate` | yes |
| The list (View Application) | no application matches the number searched | No application matches that number. Check it, or search by folio instead. | `Lists.NoApplicationByNumberTryFolio` | yes |
| The list (View Application) | no application is under the folio searched | No application under that folio. A new customer has no folio until their first deposit books — search by application number instead. | `Lists.NoApplicationUnderFolio` | yes |
| To Date | not a complete date | Enter a complete To Date as DD / MM / YYYY. | `Lists.ToDateIncomplete` | yes |
| To Date | a date still to come | The To Date cannot be in the future. | `Lists.ToInFuture` | yes |

## Short URL

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Send link | no link can be sent for the application | No link could be sent for this application. | `ShortUrl.NoLinkSent` |  |

## Renew FD

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Cancel renewal | the investor has accepted the renewal | The investor has accepted it, so it can no longer be cancelled here. | `RenewFd.CancelNotAfterAcceptance` |  |
| Cancel renewal | a physical application is already submitted | Not applicable to a physical application once it is submitted. | `RenewFd.CancelNotForPhysical` |  |
| Cancel renewal | another partner entered the renewal | Entered by another partner: only they can cancel it. | `RenewFd.CancelOnlyByWhoEntered` |  |
| Cancel renewal | pressed for a renewal already submitted (asks first) | Cancel the renewal request for deposit {number}? Its application is cancelled, and the deposit is due for renewal again. | `RenewFd.ConfirmCancelRenewal` |  |
| Cancel renewal | pressed for a renewal not submitted yet (asks first) | Cancel the renewal request for deposit {number}? Its application is dropped, and the deposit is due for renewal again. | `RenewFd.ConfirmCancelRenewalDraft` |  |
| Cancel renewal | the deposit has no renewal request | There is no renewal request for deposit {number} to cancel. | `RenewFd.NoRenewalToCancel` |  |
| Renew | the deposit is no longer open for renewal | Deposit {number} cannot be renewed now. The list shows it as it stands. | `RenewFd.CannotRenewNow` |  |

## Console Admin

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Disable tiles for a window | no tile picked, or the From is not before the To and in the future | The window was not set: pick a tile, and a From in the future before the To. | `Admin.WindowNotSet` |  |
| End a window | the window has ended already | That window has already ended. | `Admin.WindowAlreadyEnded` |  |
| From | not a complete date and time | Enter a complete From as DD / MM / YYYY and HH : MM. | `Admin.FromIncomplete` | yes |
| Notice date and time | not a complete date and time | Enter a complete date and time as DD / MM / YYYY and HH : MM. | `Admin.NoticeTimeIncomplete` | yes |
| Notice heading | left empty | Write the notice partners will see. | `Admin.NoticeHeadingMissing` | yes |
| Publish a notice | no heading, or a moment already past | The notice was not published: give it a heading and a moment still to come. | `Admin.NoticeNotPublished` |  |
| Remove a notice | the notice is no longer in the bell | That notice is no longer in the bell. | `Admin.NoticeAlreadyGone` |  |
| Tiles to disable | none picked | Pick at least one tile to disable. | `Admin.PickATile` | yes |
| To | not after the From | The window has to end after it starts. | `Admin.EndsBeforeItStarts` | yes |
| To | not a complete date and time | Enter a complete To as DD / MM / YYYY and HH : MM. | `Admin.ToIncomplete` | yes |

## Outside services

| Field | Shown when | Message | Name | Browser |
|---|---|---|---|---|
| Aadhaar masking | the service answered with an error status | {service} could not mask the copy ({status}). Try again in a while. | `OutsideServices.MaskingFailed` |  |
| Aadhaar masking | an Aadhaar is sent without the investor's consent | An Aadhaar is only masked with the investor's consent, and none has been given. | `OutsideServices.NoConsentToMask` |  |
| Aadhaar masking | the service refused the copy without a reason | The Aadhaar could not be masked. Upload a clearer copy. | `OutsideServices.NotMasked` |  |
| Aadhaar masking | the service refused the copy and said why | The Aadhaar could not be masked: {why} | `OutsideServices.NotMaskedBecause` |  |
| Any outside check | the answer was empty | {service} answered with nothing. Try again in a while. | `OutsideServices.AnsweredNothing` |  |
| Any outside check | the service cannot be reached | {service} is not answering. Try again in a while. | `OutsideServices.NotAnswering` |  |
| Any outside check | the answer could not be read | {service} answered with something that could not be read. Try again in a while. | `OutsideServices.NotReadable` |  |
| Any outside check | the service did not answer in time | {service} took too long to answer. Try again in a while. | `OutsideServices.TookTooLong` |  |
| Name match | the service refused without a reason | {service} could not compare the names just now. Try again in a while. | `OutsideServices.NamesNotCompared` |  |
| Name match | the service refused and said why | {service} could not compare the names: {why} | `OutsideServices.NamesNotComparedWith` |  |
| Name match | the gateway answered 403 | {service} was refused by the gateway (403). Operations have to look at its access. | `OutsideServices.RefusedByGateway` |  |
| The PAN check (NSDL) | the service is down | {service} is not available just now. Try again in a while. | `OutsideServices.NotAvailable` |  |
| The PAN check (NSDL) | the service refused the check without a reason | {service} could not be made just now. Try again in a while. | `OutsideServices.PanCheckFailed` |  |
| The PAN check (NSDL) | the service refused the check and said why | {service} could not be made: {why} | `OutsideServices.PanCheckFailedWith` |  |
| The document check (IDfy) | the service refused the copy without a reason | The document check could not use this copy. | `OutsideServices.CopyNotUsable` |  |
| The document check (IDfy) | the service refused the copy and said why | The document check could not use this copy: {detail} | `OutsideServices.CopyNotUsableBecause` |  |
| The document check (IDfy) | the copy is over the size the service takes | The copy is too large for the document check. Keep it under about 2 MB. | `OutsideServices.CopyTooLarge` |  |
| The document check (IDfy) | the service is taking too many requests | The document check is busy. Try again in a minute. | `OutsideServices.DocumentCheckBusy` |  |
| The document check (IDfy) | the check did not complete on the copy | The document check could not complete on this copy. Upload a clearer copy. | `OutsideServices.DocumentCheckIncomplete` |  |
| The document check (IDfy) | the service cannot be reached | The document check is not answering. Try again in a while. | `OutsideServices.DocumentCheckNotAnswering` |  |
| The document check (IDfy) | the answer could not be read | The document check answered with something that could not be read. Try again in a while. | `OutsideServices.DocumentCheckNotReadable` |  |
| The document check (IDfy) | the service did not answer in time | The document check took too long to answer. Try again in a while. | `OutsideServices.DocumentCheckTookTooLong` |  |
| The document check (IDfy) | an Aadhaar is sent without the investor's consent | An Aadhaar is only read or checked with the investor's consent, and none has been given. | `OutsideServices.NoConsentToRead` |  |
| The document check (IDfy) | the copy's resolution is refused and the service gives no detail | The copy's resolution is outside what the document check takes. | `OutsideServices.ResolutionOutside` |  |
| The document check (IDfy) | the file is not a kind the service takes | The document check does not take this kind of file. Upload a JPEG. | `OutsideServices.WrongKindOfFile` |  |
| The link shortener | the service answered without a short link | The link shortener answered with no short link. Try again in a while. | `OutsideServices.NoShortLink` |  |
