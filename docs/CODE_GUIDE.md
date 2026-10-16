# Uno TP code guide: where things are and how they are named

For anyone changing the UI, CSS or JavaScript by hand. Old names are listed in
[RENAMES.md](RENAMES.md); the visual rules (type scale, spacing, colours) in
[BRAND_GUIDELINES.md](BRAND_GUIDELINES.md).

## One page, four files

Every page is a Razor page (`Index.cshtml`), its page model beside it
(`Index.cshtml.cs`), one stylesheet and (when it needs one) one script. The layout
loads everything shared; a page adds only its own two files through
`@section Styles` and `@section Scripts` at the foot of its `.cshtml`.

| Page | URL | Page model | Razor page | Stylesheet | Script |
|---|---|---|---|---|---|
| Dashboard | `/Dashboard` | `DashboardModel` | `Pages/Dashboard/Index` | `pages/dashboard.css` | – |
| Investor Identification | `/SearchInvestor` | `NewApplicationModel` | `Pages/NewApplication/Index` | `pages/new.css` | – |
| Upload Documents | `/UploadInvestorDocuments/{appNo}` | `DocumentsModel` | `Pages/Documents/Index` | `pages/documents.css` | `pages/documents.js` |
| Investor Information | `/InvestorInformation/{appNo}` | `InvestorModel` | `Pages/Investor/Index` | `pages/investor.css` | `pages/investor.js` |
| Bank Details & Payment | `/BankDetails/{appNo}` | `PaymentModel` | `Pages/Payment/Index` | `pages/payment.css` | `pages/payment.js` |
| FD Configuration | `/FDConfiguration/{appNo}` | `DepositModel` | `Pages/Deposit/Index` + `_Quote` | `pages/deposit.css` | `pages/deposit.js` |
| Review Summary | `/ReviewSummary/{appNo}` | `ReviewModel` | `Pages/Review/Index` + `Shared/_ApplicationSummary` | `pages/review.css` | `pages/review.js` |
| Application submitted | `/ApplicationSubmitted/{appNo}` | `SubmittedModel` | `Pages/Submitted/Index` | `pages/submitted.css` | – |
| View Application | `/ViewApplication` | `ApplicationsModel`, `ApplicationDetailsModel` (the pop-up's details) | `Pages/Applications/Index`, `Details` | `pages/applications.css` | `pages/applications.js` |
| Short URL | `/ShortUrl` | `LinksModel` | `Pages/Links/Index` | `pages/links.css` | `pages/links.js` |
| Document utilities | `/Utilities` | `UtilitiesModel` | `Pages/Utilities/Index` | `pages/utilities.css` | `pages/utilities.js` (an ES module; draws PDFs with `js/vendor/pdfjs`) |
| Pay-in Slips | `/PayInSlip` | `PayInSlipsModel` | `Pages/PayInSlips/Index` | `pages/pay-in-slips.css` | `pages/pay-in-slips.js` |
| Renew FD | `/RenewalDashboard` | `RenewModel` | `Pages/Renew/Index` (draws nothing: it opens Investor Identification) | `pages/renew.css` | – |
| Console Admin | `/Admin` | `AdminModel` | `Pages/Admin/Index` | `pages/admin.css` | `pages/admin.js` |
| The way in, and back to the portal | `/Home`, `/Home/Index`, `/Home/Home`, `/Home/LogOut` | `EntryModel`, `PortalHomeModel`, `LogOutModel` | `Pages/Home/Index`, `Home`, `LogOut` | `pages/status.css` | – |
| Error, Session expired, Unauthorized, Too many requests | `/Home/Error` … | `ErrorModel`, `SessionExpiredModel` … | `Pages/Home/*` (on `_StatusLayout`) | `pages/status.css` | – |

Paths are under `UnoTP/`; stylesheets and scripts under `UnoTP/wwwroot/css/` and `wwwroot/js/`.

### How a page is put together

- **Its address is the first line of its `.cshtml`**: `@page "/UploadInvestorDocuments/{appNo}/{handler?}"`.
  These are the old app's addresses; the folder a page is in does not change them.
- **What the page shows is `Model.View`**: the page model builds the page's view model
  (`ViewModels/`), keeps it in its `View` property and returns `Page()`.
- **Opening the page is `OnGetAsync`. Everything else the page does is a handler**
  named after it: `OnPostUploadAsync` answers a post to `…/{appNo}/Upload`,
  `OnGetSourcingAsync` a get of `…/{appNo}/Sourcing`. A form or button names it with
  `asp-page-handler="Upload"`; a script is handed its address with `Url.Handler("Upload")`.
  A handler that takes a number has it after its name: `…/JointCheck/2`.
- **A link to another page names the page**: `asp-page="/Payment/Index"`, or
  `RedirectToPage("/Payment/Index")` in a page model. The application number is carried
  into it by itself (`Infrastructure/ApplicationNumberInUrls.cs`).
- An address that names a handler the page does not have is a 404
  (`Infrastructure/HandlerInAddress.cs`).

## Shared files (loaded by `Pages/Shared/_Layout.cshtml`, in this order)

| File | What is in it |
|---|---|
| `css/shared/bootstrap-theme.css` | Bootstrap 5.3 pointed at the brand: buttons, badges, alerts, form controls. Change a Bootstrap component's look here. |
| `css/shared/fonts.css` | Georama, served locally. |
| `css/shared/layout-and-controls.css` | The design tokens (`:root`: colours, the page frame, the type roles, `--doc-card-h`), the page shell, the two-column journey grid (`page-layout`, `page-rail`, `page-card`), form fields (`field-label`, `field-input`, `field-hint`, `field-error`, `date-input`), the dialog frame (`app-dialog__*`), tables, drafts list, hover hints. |
| `css/shared/journey-steps.css` | What the journey steps share: the step rail (`page-rail__step`), the action bar at the foot (`page-action-bar`), the two-option switch (`choice-switch`), and the document cards of Upload Documents and Investor Information (`doc-slot`, `doc-drop`, `doc-on-file`, `doc-read-result`, `doc-history`…). |
| `css/shared/register-pages.css` | What the list pages share: filters, search box, status pills, pager, table (`register-*`). |
| `css/shared/topbar.css` | The header and the phone menu. |
| `css/shared/phone.css` | What a phone (560px and under) gets differently: sizes, the back arrow, the foot bar, folds, sheets. Loaded after the page's own stylesheet. |
| `css/shared/motion.css` | Everything that moves: the fade between pages, what opens, what changes state. Loaded last. |
| `js/shared/messages.js` | Hands the scripts their validation and error messages from the master: `message('Lists.FromAfterTo')`. Loaded first. |
| `js/shared/partial-forms.js` | Forms marked `data-partial` post with fetch and swap in the answer's `<main>` without a reload, keeping scroll, focus and typed values. Also `data-show-when` (a choice shows or hides a part at once) and `data-guard` (warn before leaving with unsaved changes). |
| `js/shared/field-checks.js` | Client-side checks: `data-required`, `data-check`, `data-chars` on a field. |
| `js/shared/date-input.js` | The DD / MM / YYYY boxes: digits only, roll on to the next box. |
| `js/shared/loader.js` | The full-screen wait (`showLoader`, `hideLoader`, `setLoaderHint`). |
| `js/shared/pdf-upload.js` | A one-page PDF picked for a document box is drawn as a JPEG within what IDfy takes before upload, with pdf.js from `js/vendor/pdfjs`; more pages go as picked (`data-shrink`). |
| `js/shared/image-shrink.js` | Shrinks a large photo before upload (`data-shrink`). |
| `js/shared/file-drop.js` | Takes a file dropped on a document card, empty or filed, and marks the box while a file is over it (`is-over`). |
| `js/shared/sticky-app-strip.js` | The application strip under the header once the page head scrolls away (`data-pin-head`). |
| `js/shared/notices-bell.js`, `js/shared/topbar.js` | The bell panel and the phone menu. |
| `js/shared/console.js` | Toast and list search carried over from the console. |

Shared partials in `Pages/Shared/`: `_DocSlot` (one document card), `_DocLog` (a document's history), `_HolderIdentification` (the PAN / date of birth / folio search block), `_Drafts` (applications to pick up again), `_ApplicationSummary` (the six review sections, editable or read-only), `_RequiredDocs` (the dashboard's document-list dialog), `_ReadCard(s)`, `_ProofType`, `_ChoiceSwitch` (a choice between two, drawn as a switch), `_NsdlRetry`, `_AadhaarNumber`, `_Notices`, `_ClassicSteps`.

## Naming

**CSS classes** are `block__part--variant`. The block starts with a plain word for the
page or the thing it is:

| Prefix | Used for |
|---|---|
| `page-` | the journey shell shared by every step: `page-layout`, `page-rail`, `page-card`, `page-title`, `page-head`, `page-note`, `page-action-bar` |
| `field-`, `date-input`, `choice-switch`, `radio-option` | form controls |
| `identify-` | the PAN / folio search and its result |
| `doc-` | Upload Documents' cards: `doc-slot`, `doc-drop` (the picker), `doc-on-file`, `doc-read-result`, `doc-history`, `doc-tool` |
| `investor-` | Investor Information |
| `deposit-` | FD Configuration |
| `review-`, `submitted-`, `pay-link-` | Review Summary, the submitted page, the payment link dialog |
| `view-app-` | View Application's status list and details |
| `register-`, `payin-slip-` | the list pages, and Pay-in Slips' own parts |
| `dashboard-`, `admin-`, `public-` | the dashboard, Console Admin, the public pages' nav |
| `hover-hint`, `required-docs`, `key-value`, `app-dialog` | the tooltip, the document-list dialog, label/value pairs, the dialog frame |
| `is-` | a state, never a thing: `is-invalid`, `is-busy`, `is-locked`, `is-saving` |

Bootstrap's own classes (`btn`, `badge`, `alert`, `form-select`…) are used as they
are and themed in `bootstrap-theme.css`.

**Element ids** are camelCase with the page's word first: `docsForm`, `investorGuardian`,
`viewAppModal`, `payinSlipError`, `adminNoticeForm`, `depositRefresh`. Per-holder ids
carry the holder's number: `investor1Income`, `h2-pan`.

**`data-` attributes are the contract between a view and its script.** A script finds
its elements by `data-*` and ids, almost never by class. Before renaming one, search
`wwwroot/js/` for it and change both sides.

**C#**: page models stay thin and hand the work to a view model in `ViewModels/`.
`Services/` holds one folder per backend API (the way in, the PAN check, masking, IDfy, the
shortener); `UnoTP.Data` is the SQL layer over the `t_Unotp_` tables, with the models both share. `Infrastructure/` holds the cross-cutting parts: feature gate, security headers,
partner session, URL building. Every method has a `///` summary or a comment above it.

## Where the SQL is

Every SQL statement is a named constant in a file that holds nothing but SQL, and the
files are by page: to change what a page reads or writes, open the file named after
it. The code that runs a statement uses it by name.

**Every query is one whole statement.** None is built from another constant, from a
piece passed to it, or by a method: what you read in the constant is exactly what
SQL Server is sent, and it can be copied into a query window and run once its
`@parameters` are declared. So a query is changed on its own, without looking
anywhere else. `WholeQueryTests` fails the build's tests when a query file gets a
method, a private piece, or a string built from another (`$"..."`).

The cost is that what several queries share is written out in each of them, and a
change to it is made in each. Search the `Queries` folder for the table or column
to find them all. What is shared today:

| Shared | Written in |
|---|---|
| The application header's columns (`HeaderRow`) | `ApplicationQueries.Header`, `HeaderLocked`; `ReviewSummaryQueries.Submit`, `RecordLink`; `ApplicationSubmittedQueries.ResendLink` |
| The two link tables read as one, and when an investor accepted (the four flags on the newest link row) | `ViewApplicationQueries.Applications`, the two Short URL queries (`PayInSlipQueries.StagedSlips` reads the purchase table alone: only a purchase is paid by cheque) |
| The application's current deposit row (`OUTER APPLY ... i`) | the same four queries, and `InvestorIdentificationQueries.Drafts` |
| The link tables' names | those, and the six link queries of `ReviewSummaryQueries` and `ApplicationSubmittedQueries` (one each for a purchase and a renewal) |
| A master read through an inner `SELECT` (bank branches, Axis CMS branches, a register, a rate card, a folio holder's KYC or documents) | the two queries that read it: one row by its key, and the search or the list |
| Everything a link send writes (the payment link recorded, the link row, the same link again, the count behind the limits, a message to the investor in `t_FD_PGW_Comm_Hdr`) | each page that sends a link has its own full copy: `ReviewSummaryQueries`, `ApplicationSubmittedQueries`, `ShortUrlQueries` (`LinkQuerySet` in `LinkRequestTables.cs` names each page's set; the page says which it is with `LinkPage`) |
| A step's rows going out of use on a save | one `Retire...` query to a table, in the step's own file |

| File (`UnoTP.Data/Queries/`) | Page | Its SQL |
|---|---|---|
| `InvestorIdentificationQueries.cs` | Investor Identification | the folio check; the holder's row on the folio; the KYC details, address and documents on record; the drafts to continue, and cancelling one |
| `UploadDocumentsQueries.cs` | Upload Documents | the step's state; the document rows (`t_FD_BT_KYC_document`); the broker register and the staff each sourcing mode takes |
| `InvestorInformationQueries.cs` | Investor Information | what is typed and not yet saved; a folio holder's KYC details; the PIN code master; the nominee on a deposit; the KYC, address and nominee rows |
| `BankDetailsQueries.cs` | Bank Details and Payment | bank branches and Axis CMS branches, by code and as typed; the account on a deposit; the payment and repayment rows |
| `FdConfigurationQueries.cs` | FD Configuration | the rate cards and the rates off them; the investor's active deposits; the deposit row; the source of funds log |
| `ReviewSummaryQueries.cs` | Review Summary (Submit) | the application becomes APR; the pay-in slip staging row; the link recorded and written to its table; the messages that carry a link (`t_FD_PGW_Comm_Hdr`) and the count behind the limits on them |
| `ApplicationSubmittedQueries.cs` | Application Submitted | the same link sent again; its own copies of everything a first link writes (after Try later): the link recorded, the link row, the count behind the limits, the messages |
| `PayInSlipQueries.cs` | Pay In Slip Generation | the cheque applications a search reaches, in one query (by the days raised, by number, or the one application; the newest 200); a slip made as the FD system's procedure makes it: the number from `USP_FD_BTP_GetPayinSlipNo`, the header and detail rows copied from the staging row, the staging row and the header marked done; a slip's number, header and detail rows; the staging row marked done; the slip as printed |
| `ShortUrlQueries.cs` | Short URL | the applications a link can go to and the live link of each, in one query apiece (those submitted in the days an unpaid one stands, or the one application); its own copies of everything Send link, Resend and New link write: the link row, the same link again, the count behind the limits, the messages |
| `ViewApplicationQueries.cs` | View Application | the partner's applications a search reaches, in one query: by the days raised, by application number or by folio, the newest 200 |
| `DashboardQueries.cs` | Dashboard | the open applications close to cancelling |
| `RenewFdQueries.cs` | Renew FD | the deposits a holder can renew, from the FD system's deposit register as its own Renew FD procedure lists them, the renewal window from its web config, the joint holders on a folio; cancelling a renewal and retiring its row in the renewal transaction table |
| `ConsoleAdminQueries.cs` | Console Admin | the maintenance windows and the notices |
| `ApplicationQueries.cs` | every step | finding, opening and versioning an application; everything saved on it, read in one go; the column list of each table; the two link tables |
| `ListQueries.cs` | every page | the settings, the features, the lists, the payment gateway's banks |
| `ErrorLogQueries.cs` | every page | the error log |

A query more than one page runs is in the file of the page it belongs to first, and
the other page's file says so in its heading: the dashboard lists Investor
Identification's drafts and Pay In Slip Generation's slips; Investor Information
saves a joint holder's documents with Upload Documents' queries; Application
Submitted and Short URL send a first link with Review Summary's.

### A query on an FD table

The folio master, the deposit register, brokers, staff, bank branches, PIN codes, the
Axis CMS branches and the rate cards are the FD system's own tables, read where they
are. Change such a query when its table has another name, needs a join, or needs a condition.

- Keep the names after `AS`: they are what the app reads. A column may come from any
  table or expression, so long as it comes back under the same name.
- A table in another database on the same server is written by its full name:
  `OtherDb.dbo.Table`.
- A query that finds rows by a key carries that key as a parameter, written in its
  own `WHERE` (`@Folio`, `@Pan`, `@Dob`, `@Pin`). It is run as it stands, so the table
  is read by that key and never whole. Keep the parameter names, and compare them
  with the table's own column as it is (`f.FOLIO_NO = @Folio`): a column wrapped in a
  function cannot be found by its index.
- Where a query reads a master through an inner `SELECT`, the inner one is the
  master's own read and the outer one is the app's: change the inner one, and keep
  what the outer one orders by.
- The registers searched as they are typed (brokers, staff, bank branches, Axis CMS
  branches) take two parameters, both written in their own `WHERE`: the key of one
  row (`@Code` or `@Ifsc`), and what was typed - `@Search`, a `LIKE` pattern
  (`%word%word%`), or for the bank `@Words`, the words themselves. The app gives one
  and leaves the other empty (`NULL`): a row is found by its key, or a handful are
  found by what was typed. Neither ever brings back the whole register.

**One query for every search of a list.** View Application and Pay In Slip Generation
each have a single list query. Every search condition in it is written
`AND (@Value IS NULL OR column ... @Value)`: the app gives the values of the search
that was asked for and sends the others as `NULL`, and a condition whose value is
`NULL` lets every row through. To add a search, add its condition and its parameter
(`SearchParameters.cs`, and where the query is run). The query ends with
`OPTION (RECOMPILE)`, so SQL Server plans each read for the values it was given.

**Database names.** An FD system table is written with its database, so the app
reads it wherever its own connection points:

| Tables | Database |
|---|---|
| `t_FD_BT_*` (the application's rows, the pay-in slip tables), `t_FD_BOTC_SCHEME`, `t_FD_BTP_Broker_Mst`, `t_FD_Axis_CMS_Branch_Mst` | `MMFSL_APP_FD_BTP` |
| `t_FD_PGW_PaymentURLRequest_dtl` (a purchase's link), `T_FD_PGW_RW_DECLARATION_URL_REQUEST_DTL` (a renewal's), `t_FD_PGW_Comm_Hdr` (a message to the investor), `t_FD_PGW_Payment_Log` (a payment made online) | `MMFSL_APP_FDPGW` |
| `t_FD_CMN_*` (the PIN master, the source of funds log) | `MMFSL_APP_FD_Common` |
| the deposit register, the folio holding and the `..._ORA` tables | `FD` |
| the employee view | `MMFSL_DATA_IMPORTED` |

Still written with `dbo.` alone, in the app's own database: the app's own `t_Unotp_*`
tables and sequences, and `T_FD_RBI_BANK_MSTR`, `FD_SCHEME` and `USP_FD_BTP_GetApplicationNo`, whose database has
not been confirmed.

To add a column to a table the app writes:

1. Add it to the table's INSERT in the query file: the column in the list, and its `@Name` in VALUES.
2. Give `@Name` its value in the save that runs the INSERT (`SaveStep.*.cs`, or the row in `Models/` for pay-in slips and links).
3. If it is read back, add it to the table's statement in `ApplicationQueries.WholeApplication` (`f_Column AS Name`) and add the `Name` property to the row class in `ApplicationTableRows.cs`.

## Validation and error messages

Every validation and error message is written once, in `UnoTP.Data/Messages.cs`,
grouped by page. `docs/VALIDATIONS.md` is the same list as a table (field, when it
is shown, the words, its name), written from that file.

- **To change a message's words**, change them in `Messages.cs` and nowhere else,
  then write the list again:
  `UPDATE_VALIDATIONS=1 dotnet test UnoTP.sln --filter ValidationListTests`.
  Build, and replace `UnoTP.dll` and `UnoTP.Data.dll`.
- **To add a message**, add it to its page in `Messages.cs` with a `[Shown("field",
  "when")]` line over it, and use it by name: `Messages.BankDetails.AccountInvalid`
  in C# and in a view (`data-required="@Messages.BankDetails.AccountInvalid"`).
- **A message a script shows** also takes `InBrowser = true` on its `[Shown]` line;
  the script asks for it with `message('BankDetails.AccountInvalid')`. A value goes
  in by name: `message('BankDetails.NoBranchMatches', { typed: text })`.
- **A message with a value in it** is a small method: `Messages.Shared.TooLong(40)`.
- Never write an error's words in a page model, a view model, a page or a script.
  `ValidationListTests` fails when the list is out of step, when a script asks for
  a message the master does not give it, or when a message is spelt out again.

Not in the master: labels, hints, loader texts, success notes, status words and
the history of a document. They stay where they are shown.

## Rules to keep

- **No inline `style=` or `<script>` in a view.** The Content-Security-Policy blocks
  them; put styles in the page's stylesheet and behaviour in its script.
- **Sizes come from the design** (BRAND_GUIDELINES.md). The page frame and the type
  are tokens: `var(--field-gap)`, `font: var(--font-label)`. Text is 12 / 14 / 16px
  (20px for a dashboard's or a dialog's lead), fields and buttons 36px, spacing
  between parts a multiple of 8. `--doc-card-h` keeps every document
  card in a row the same height.
- **Disable, don't hide.** Something the partner cannot use yet is shown disabled with
  its reason (a `hover-hint` or an `alert`), not removed.
- **Razor writes a `data-*` attribute even when its value is null.** Render two
  variants of the element instead of passing null.
- **Nothing outside is retried**: an IDfy or NSDL call may be charged.

## Running and checking

```
dotnet run --project UnoTP --launch-profile http      # port 5102; PORT=5103 for a second copy
```

Stylesheets and scripts are linked with `asp-append-version`, so a change shows on
the next reload. After a UI change, run `dotnet test` (`UiGuidelineTests` holds the
design's numbers, the text sizes and the no-inline-styles rule), then open the page
at 1360px and at a phone's width and go down the checklist at the end of
BRAND_GUIDELINES.md.
