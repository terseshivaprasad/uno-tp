# What each page asks of the backend

The app talks to the backend through the interfaces in `UnoTP.Data/Models` and nothing
else. They are grouped by what they are about, not by page, so a page uses a few of them
and most are shared. This is the map, page by page, as the code stands. Each call's
request and answer is in [backend-api.md](backend-api.md); `UnoTP.Data` answers
the ones kept in SQL Server, and the outside services have an HTTP client each in
`UnoTP/Services`.

## Every page

| Call | Used for |
|---|---|
| `IPartnerApi.MeAsync` | Who is signed in, for the header, the sourcing rules and the lists' ownership: what the auth API said when the session started, kept with the sign-in. |
| `IReferenceApi.ReferenceAsync`, `ConfigAsync` | The drop-down lists, notes and documents; the limits, ages and days. Kept for `Backend:ReferenceCacheMinutes`. |
| `IConsoleApi.BoardAsync` | Which features are on, and the notices in the bell. |

## The way in and out (`EntryModel`)

| Call | Used for |
|---|---|
| `IDecryptionService.DecryptAsync` | The portal's encrypted user id and system code (`POST cipher/decrypt` on the auth API). |
| `ISessionApi.StartAsync`, `MenuAsync` | Start the session (`POST auth/sessions`); the partner's Uno TP menus (`GET app-menus/{userId}/{sysCode}`): a partner with none is refused. Which features are on is the `Features` section of appsettings alone. |

## Dashboard

| Call | Used for |
|---|---|
| `IApplicationApi.ListAsync`, `DraftsAsync` | Work waiting, and applications to pick up again. |
| `ILinkApi.PendingAsync` | Applications waiting on the investor. |
| `IPayInSlipApi.SlipsAsync` | Slips still to be presented. |

## Investor Identification (`NewApplicationModel`, `HolderSearch`)

| Call | Used for |
|---|---|
| `IInvestorApi.FolioDepositsByPanAsync`, `FolioDepositsByFolioAsync`, `FolioAsync` | The search by PAN and date of birth, or by folio: the deposits the folio check (`FolioCheck`) looks at, then the details of the folio it finds. |
| `IApplicationApi.OpenAsync`, `DraftsAsync` | Opening the application; the drafts offered instead. |
| `IApplicationApi.CancelDraftAsync` | Cancel on a row of the incomplete applications (here and on the dashboard): the draft is marked cancelled, leaves the list, and its steps no longer open or save. |

## Upload Documents (`DocumentsModel`, `DocumentsViewModel`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SaveUploadAsync` | The application; the step saved. |
| `IDocumentApi.FileAsync`, `CopyAsync`, `KeepRefusedAsync` | Filing a copy with DMS as a new named file, showing one, keeping a refused one aside. Nothing filed is replaced or deleted. |
| `IDocumentIdentifier.IdentifyAsync` | What the copy is. |
| `IOcrService.ReadAsync` | What it says. |
| `ICkycService.SearchAsync` | Whether CERSAI holds a record, when Fetch from CKYC is chosen (`Ckyc:SearchPath`). |
| `IPanVerificationService.VerifyAsync` | The PAN, date of birth and name, checked with NSDL (`PanApi:VerifyPath`). |
| `IVerificationService.ConfirmProofAsync`, `ConfirmAccountAsync` | The proof with its issuer; the cheque's account with its bank. |
| `IMaskingService.MaskAsync` | An Aadhaar masked before it is filed. |
| `INameMatchService.MatchAsync` | The name on a proof against the PAN's; an Aadhaar's name against the PAN's. |
| `IPanAadhaarLinkService.CheckAsync` | The PAN-Aadhaar link. |
| `IFaceMatchService.CompareAsync` | The faces on the PAN copy and the proof. |
| `ISourcingApi.BrokersAsync`, `StaffAsync` | The sourcing registers searched as a code is typed. |

The joint holders' documents on Investor Information go through the same calls.

## Investor Information (`InvestorModel`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SaveDetailsAsync`, `SavePageAsync`, `SaveUploadAsync` | The application; the details saved after every post; the page's working state; the category moved to a women's one, or a joint holder's documents. |
| `IInvestorApi.FolioDepositsByPanAsync`, `FolioDepositsByFolioAsync`, `FolioAsync`, `KycOnFolioAsync`, `NomineesOnDepositAsync` | A joint holder's search; the KYC details held for a holder of a folio (by folio, PAN and date of birth) where their latest KYC is kept (their data source), filled in so they are not typed again and validated like anything typed; in a renewal, the nominees on the deposit being renewed. |
| `IPlaceApi.PinCodeAsync` | The district and state of a PIN code. |
| `INameScreeningService.ScreenAsync` | Every holder, before Proceed. |

## Bank Details & Payment (`PaymentModel`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SavePaymentAsync` | The application; the step saved. |
| `IDepositApi.BranchAsync`, `SearchBranchesAsync`, `SearchCmsLocationsAsync`, `CmsLocationAsync` | The branch an IFSC names; the bank search; the Axis CMS branch search, and the location picked checked against the master. |
| `IInvestorApi.AccountsOnDepositAsync` | In a renewal, the repayment accounts on the deposit being renewed. |

## FD Configuration (`DepositModel`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SaveDepositAsync`, `SaveUploadAsync` | The application; the step saved; the Form 121 filed. |
| `IDepositApi.RatesAsync`, `QuoteAsync` | The rate card for the category, gender and purchase or renewal; the quote for the deposit as it stands. |
| `IInvestorApi.ActiveDepositsAsync` | The investor's active deposits in the FD system's register (`FDR_MST`, `DEP_STATUS = 'L'`, by `PAN1`), added up for the source-of-funds rule. |

## Review Summary and Application Submitted (`ReviewModel`, `SubmittedModel`)

| Call | Used for |
|---|---|
| `IApplicationApi.FindAsync`, `SubmitAsync`, `RecordPaymentLinkAsync`, `ResendLinkAsync` | The application; saved as submitted; then - on Submit & send link, or later for one submitted with Try later - its link put on record (the payment link table for a purchase, the re-payment link table for a renewal); a new link. |
| `IDepositApi.QuoteAsync`, `BranchAsync` | The quote and the branches shown. |
| `IShortLinkService.ShortenAsync` | The payment link shortened, once the application is saved. |
| `IInvestorApi.ActiveDepositsAsync` | The source-of-funds rule, checked once more before submit. |

## View Application (`ApplicationsModel`, `ApplicationDetailsModel`)

| Call | Used for |
|---|---|
| `IApplicationApi.ListAsync`, `FindAsync` | The list; one application's details in the pop-up. |
| `IDepositApi.QuoteAsync`, `BranchAsync` | The details' figures. |

## Short URL (`LinksModel`) and Pay-in Slips (`PayInSlipsModel`)

| Call | Used for |
|---|---|
| `ILinkApi.SentAsync`, `PendingAsync`, `SendAsync` | Links sent; applications waiting; a link sent again. |
| `IPayInSlipApi.SlipsAsync`, `GenerateAsync` | Slips; a slip generated or reprinted. |

## Renew FD (`RenewModel`)

| Call | Used for |
|---|---|
| `IRenewalApi.DepositsByFolioAsync`, `DepositsByPanAsync`, `StartAsync`, `CancelAsync` | The deposits a folio or PAN holds; a renewal opened; a renewal request cancelled. |

## Console Admin (`AdminModel`)

| Call | Used for |
|---|---|
| `IConsoleApi.BoardAsync` and its changes | Windows and notices. |
