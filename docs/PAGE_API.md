# What each page asks of the backend

The app talks to the backend through the interfaces in `src/UnoTP.Backend` and nothing
else. They are grouped by what they are about, not by page, so a page uses a few of them
and most are shared. This is the map, page by page, as the code stands. Each call's
request and answer is in [backend-api.md](backend-api.md); `src/UnoTP.Backend.Mock` is a
working implementation of every one.

## Every page

| Call | Used for |
|---|---|
| `ISessionApi.IsOpenAsync` | The session check on every request (the backend's session still open; answered once a minute at most). |
| `IPartnerApi.MeAsync` | Who is signed in, for the header, the sourcing rules and the lists' ownership. |
| `IReferenceApi.ReferenceAsync`, `ConfigAsync` | The drop-down lists, notes and documents; the limits, ages and days. Kept for `Backend:ReferenceCacheMinutes`. |
| `IConsoleApi.BoardAsync` | Which features are on, and the notices in the bell. |

## The way in and out (`EntryController`)

| Call | Used for |
|---|---|
| `IDecryptionService.DecryptAsync` | The portal's encrypted user id and system code. |
| `ISessionApi.StartAsync`, `MenuAsync` | Start the session; the features the partner's menu opens. |

## Dashboard

| Call | Used for |
|---|---|
| `IApplicationApi.ListAsync`, `DraftsAsync` | Work waiting, and applications to pick up again. |
| `ILinkApi.PendingAsync` | Applications waiting on the investor. |
| `IPayInSlipApi.SlipsAsync` | Slips still to be presented. |

## Investor Identification (`NewApplicationController`, `HolderSearch`)

| Call | Used for |
|---|---|
| `IInvestorApi.FoliosByPanAsync`, `FolioAsync` | The search by PAN and date of birth, or by folio. |
| `IApplicationApi.OpenAsync`, `DraftsAsync` | Opening the application; the drafts offered instead. |

## Upload Documents (`DocumentsController`, `DocumentsViewModel`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SaveUploadAsync` | The application; the step saved. |
| `IDocumentApi.FileAsync`, `DeleteAsync`, `CopyAsync`, `KeepRefusedAsync` | Filing a copy with DMS, replacing one, showing one, keeping a refused one aside. |
| `IDocumentIdentifier.IdentifyAsync` | What the copy is. |
| `IOcrService.ReadAsync` | What it says. |
| `INsdlService.VerifyAsync` | The PAN, date of birth and name. |
| `IVerificationService.ConfirmProofAsync`, `ConfirmAccountAsync` | The proof with its issuer; the cheque's account with its bank. |
| `IMaskingService.MaskAsync` | An Aadhaar masked before it is filed. |
| `INameMatchService.MatchAsync` | The name on a proof against the PAN's; an Aadhaar's name against the PAN's. |
| `IPanAadhaarLinkService.CheckAsync` | The PAN-Aadhaar link. |
| `IFaceMatchService.CompareAsync` | The faces on the PAN copy and the proof. |
| `ISourcingApi.BrokersAsync`, `StaffAsync` | The sourcing registers searched as a code is typed. |

The joint holders' documents on Investor Information go through the same calls.

## Investor Information (`InvestorController`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SaveDetailsAsync`, `SavePageAsync`, `SaveUploadAsync` | The application; the details saved after every post; the page's working state; the category moved to a women's one, or a joint holder's documents. |
| `IInvestorApi.FoliosByPanAsync`, `FolioAsync`, `NomineesByFolioAsync` | A joint holder's search; the nominees on the folio. |
| `IPlaceApi.PinCodeAsync` | The district and state of a PIN code. |
| `INameScreeningService.ScreenAsync` | Every holder, before Proceed. |

## Bank Details & Payment (`PaymentController`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SavePaymentAsync` | The application; the step saved. |
| `IDepositApi.BranchAsync`, `SearchBranchesAsync` | The branch an IFSC names; the bank search. |
| `IInvestorApi.AccountsByFolioAsync` | The repayment accounts on the folio. |

## FD Configuration (`DepositController`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SaveDepositAsync`, `SaveUploadAsync` | The application; the step saved; the Form 121 filed. |
| `IDepositApi.RatesAsync`, `QuoteAsync` | The rate card for the category, gender and purchase or renewal; the quote for the deposit as it stands. |
| `IRenewalApi.DepositsByFolioAsync`, `DepositsByPanAsync` | The investor's active deposits, for the source-of-funds rule. |

## Review Summary and Application Submitted (`ReviewController`, `SubmittedController`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SubmitAsync`, `ResendLinkAsync` | The application; submit with the link; a new link. |
| `IDepositApi.QuoteAsync`, `BranchAsync` | The quote and the branches shown. |
| `IShortLinkService.ShortenAsync` | The payment link shortened. |
| `IRenewalApi.DepositsByFolioAsync`, `DepositsByPanAsync` | The source-of-funds rule, checked once more before submit. |

## View Application (`ApplicationsController`, `ApplicationDetailsController`)

| Call | Used for |
|---|---|
| `IApplicationApi.ListAsync`, `FindAsync` | The list; one application's details in the pop-up. |
| `IDepositApi.QuoteAsync`, `BranchAsync` | The details' figures. |

## Short URL (`LinksController`) and Pay-in Slips (`PayInSlipsController`)

| Call | Used for |
|---|---|
| `ILinkApi.SentAsync`, `PendingAsync`, `SendAsync` | Links sent; applications waiting; a link sent again. |
| `IPayInSlipApi.SlipsAsync`, `GenerateAsync` | Slips; a slip generated or reprinted. |

## Renew FD (`RenewController`)

| Call | Used for |
|---|---|
| `IRenewalApi.DepositsByFolioAsync`, `DepositsByPanAsync`, `StartAsync`, `CancelAsync` | The deposits a folio or PAN holds; a renewal opened; a renewal request cancelled. |

## Console Admin (`AdminController`)

| Call | Used for |
|---|---|
| `IConsoleApi.BoardAsync` and its changes | Windows and notices. |
