# Uno TP code guide: where things are and how they are named

For anyone changing the UI, CSS or JavaScript by hand. Old names are listed in
[RENAMES.md](RENAMES.md); the visual rules (type scale, spacing, colours) in
[BRAND_GUIDELINES.md](BRAND_GUIDELINES.md).

## One page, four files

Every page is a controller, a Razor view, one stylesheet and (when it needs one)
one script. The layout loads everything shared; a page adds only its own two files
through `@section Styles` and `@section Scripts` at the foot of its view.

| Page | URL | Controller | View | Stylesheet | Script |
|---|---|---|---|---|---|
| Dashboard | `/Dashboard` | `DashboardController` | `Views/Dashboard/Index` | `pages/dashboard.css` | – |
| Investor Identification | `/SearchInvestor` | `NewApplicationController` | `Views/NewApplication/Index` | `pages/new.css` | – |
| Upload Documents | `/UploadInvestorDocuments/{appNo}` | `DocumentsController` | `Views/Documents/Index` | `pages/documents.css` | `pages/documents.js` |
| Investor Information | `/InvestorInformation/{appNo}` | `InvestorController` | `Views/Investor/Index` | `pages/investor.css` | `pages/investor.js` |
| Bank Details & Payment | `/BankDetails/{appNo}` | `PaymentController` | `Views/Payment/Index` | `pages/payment.css` | `pages/payment.js` |
| FD Configuration | `/FDConfiguration/{appNo}` | `DepositController` | `Views/Deposit/Index` + `_Quote` | `pages/deposit.css` | `pages/deposit.js` |
| Review Summary | `/ReviewSummary/{appNo}` | `ReviewController` | `Views/Review/Index` + `Shared/_ApplicationSummary` | `pages/review.css` | `pages/review.js` |
| Application submitted | `/ApplicationSubmitted/{appNo}` | `SubmittedController` | `Views/Submitted/Index` | `pages/submitted.css` | – |
| View Application | `/ViewApplication` | `ApplicationsController`, `ApplicationDetailsController` (the pop-up's details) | `Views/Applications/Index` | `pages/applications.css` | `pages/applications.js` |
| Short URL | `/ShortUrl` | `LinksController` | `Views/Links/Index` | `pages/links.css` | `pages/links.js` |
| Pay-in Slips | `/PayInSlip` | `PayInSlipsController` | `Views/PayInSlips/Index` | `pages/pay-in-slips.css` | `pages/pay-in-slips.js` |
| Renew FD | `/RenewalDashboard` | `RenewController` | `Views/Renew/Index` | `pages/renew.css` | – |
| Console Admin | `/Admin` | `AdminController` | `Views/Admin/Index` | `pages/admin.css` | `pages/admin.js` |
| Error, Session expired, Unauthorized | `/Home/Error` … | `EntryController` | `Views/Entry/*`, `Shared/Error` (on `_StatusLayout`) | `pages/status.css` | – |

Paths are under `UnoTP/`; stylesheets and scripts under `UnoTP/wwwroot/css/` and `wwwroot/js/`.

## Shared files (loaded by `Views/Shared/_Layout.cshtml`, in this order)

| File | What is in it |
|---|---|
| `css/shared/bootstrap-theme.css` | Bootstrap 5.3 pointed at the brand: buttons, badges, alerts, form controls. Change a Bootstrap component's look here. |
| `css/shared/fonts.css` | Georama, served locally. |
| `css/shared/layout-and-controls.css` | The design tokens (`:root`: colours, the page frame, the type roles, `--doc-card-h`), the page shell, the two-column journey grid (`page-layout`, `page-rail`, `page-card`), form fields (`field-label`, `field-input`, `field-hint`, `field-error`, `date-input`), the dialog frame (`app-dialog__*`), tables, drafts list, hover hints. |
| `css/shared/journey-steps.css` | What the journey steps share: the step rail (`page-rail__step`), the action bar at the foot (`page-action-bar`), the two-option switch (`choice-switch`), and the document cards of Upload Documents and Investor Information (`doc-slot`, `doc-drop`, `doc-on-file`, `doc-read-result`, `doc-history`…). |
| `css/shared/register-pages.css` | What the list pages share: filters, search box, status pills, pager, table (`register-*`). |
| `css/shared/topbar.css` | The header and the phone menu. |
| `js/shared/messages.js` | Hands the scripts their validation and error messages from the master: `message('Lists.FromAfterTo')`. Loaded first. |
| `js/shared/partial-forms.js` | Forms marked `data-partial` post with fetch and swap in the answer's `<main>` without a reload, keeping scroll, focus and typed values. Also `data-show-when` (a choice shows or hides a part at once) and `data-guard` (warn before leaving with unsaved changes). |
| `js/shared/field-checks.js` | Client-side checks: `data-required`, `data-check`, `data-chars` on a field. |
| `js/shared/date-input.js` | The DD / MM / YYYY boxes: digits only, roll on to the next box. |
| `js/shared/loader.js` | The full-screen wait (`showLoader`, `hideLoader`, `setLoaderHint`). |
| `js/shared/image-shrink.js` | Shrinks a large photo before upload (`data-shrink`). |
| `js/shared/sticky-app-strip.js` | The application strip under the header once the page head scrolls away (`data-pin-head`). |
| `js/shared/notices-bell.js`, `js/shared/topbar.js` | The bell panel and the phone menu. |
| `js/shared/console.js` | Toast and list search carried over from the console. |

Shared partials in `Views/Shared/`: `_DocSlot` (one document card), `_DocLog` (a document's history), `_HolderIdentification` (the PAN / date of birth / folio search block), `_Drafts` (applications to pick up again), `_ApplicationSummary` (the six review sections, editable or read-only), `_RequiredDocs` (the dashboard's document-list dialog), `_ReadCard(s)`, `_ProofType`, `_ChoiceSwitch` (a choice between two, drawn as a switch), `_NsdlRetry`, `_AadhaarNumber`, `_Notices`, `_ClassicSteps`.

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

**C#**: controllers stay thin and hand the work to a view model in `ViewModels/`.
`Services/` holds one folder per backend API (the way in, the PAN check, masking, IDfy, the
shortener); `UnoTP.Data` is the SQL layer over the `t_Unotp_` tables, with the models both share. `Infrastructure/` holds the cross-cutting parts: feature gate, security headers,
partner session, URL building. Every method has a `///` summary or a comment above it.

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
- Never write an error's words in a controller, a view model, a view or a script.
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
