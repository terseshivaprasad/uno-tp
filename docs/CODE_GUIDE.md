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
| Dashboard | `/unotp` | `DashboardController` | `Views/Dashboard/Index` | `pages/dashboard.css` | – |
| Investor Identification | `/unotp/new` | `NewApplicationController` | `Views/NewApplication/Index` | `pages/new.css` | – |
| Upload Documents | `/unotp/applications/{appNo}/documents` | `DocumentsController` | `Views/Documents/Index` | `pages/documents.css` | `pages/documents.js` |
| Investor Information | `…/investor` | `InvestorController` | `Views/Investor/Index` | `pages/investor.css` | `pages/investor.js` |
| Bank Details & Payment | `…/payment` | `PaymentController` | `Views/Payment/Index` | `pages/payment.css` | `pages/payment.js` |
| FD Configuration | `…/deposit` | `DepositController` | `Views/Deposit/Index` + `_Quote` | `pages/deposit.css` | `pages/deposit.js` |
| Review Summary | `…/review` | `ReviewController` | `Views/Review/Index` + `Shared/_ApplicationSummary` | `pages/review.css` | `pages/review.js` |
| Application submitted | `…/submitted` | `SubmittedController` | `Views/Submitted/Index` | `pages/submitted.css` | – |
| View Application | `/unotp/applications` | `ApplicationsController`, `ApplicationDetailsController` (the pop-up's details) | `Views/Applications/Index` | `pages/applications.css` | `pages/applications.js` |
| Short URL | `/unotp/links` | `LinksController` | `Views/Links/Index` | `pages/links.css` | `pages/links.js` |
| Pay-in Slips | `/unotp/pay-in-slips` | `PayInSlipsController` | `Views/PayInSlips/Index` | `pages/pay-in-slips.css` | `pages/pay-in-slips.js` |
| Renew FD | `/unotp/renew` | `RenewController` | `Views/Renew/Index` | `pages/renew.css` | – |
| Console Admin | `/unotp/admin` | `AdminController` | `Views/Admin/Index` | `pages/admin.css` | `pages/admin.js` |
| Error, Session expired, Unauthorized | `/unotp/error` … | `EntryController` | `Views/Entry/*`, `Shared/Error` (on `_StatusLayout`) | `pages/status.css` | – |

Paths are under `src/UnoTP/`; stylesheets and scripts under `src/UnoTP/wwwroot/css/` and `wwwroot/js/`.

## Shared files (loaded by `Views/Shared/_Layout.cshtml`, in this order)

| File | What is in it |
|---|---|
| `css/shared/bootstrap-theme.css` | Bootstrap 5.3 pointed at the brand: buttons, badges, alerts, form controls. Change a Bootstrap component's look here. |
| `css/shared/fonts.css` | Georama, served locally. |
| `css/shared/layout-and-controls.css` | The design tokens (`:root` colours, `--doc-card-h`), the page shell, the two-column journey grid (`page-layout`, `page-rail`, `page-card`), form fields (`field-label`, `field-input`, `field-hint`, `field-error`, `date-input`), the dialog frame (`app-dialog__*`), tables, drafts list, hover hints. |
| `css/shared/journey-steps.css` | What the journey steps share: the step rail (`page-rail__step`), the action bar at the foot (`page-action-bar`), the choice toggle, and the document cards of Upload Documents and Investor Information (`doc-slot`, `doc-drop`, `doc-on-file`, `doc-read-result`, `doc-history`…). |
| `css/shared/register-pages.css` | What the list pages share: filters, search box, status pills, pager, table (`register-*`). |
| `css/shared/topbar.css` | The 44px header and the phone menu. |
| `js/shared/partial-forms.js` | Forms marked `data-partial` post with fetch and swap in the answer's `<main>` without a reload, keeping scroll, focus and typed values. Also `data-show-when` (a choice shows or hides a part at once) and `data-guard` (warn before leaving with unsaved changes). |
| `js/shared/field-checks.js` | Client-side checks: `data-required`, `data-check`, `data-chars` on a field. |
| `js/shared/date-input.js` | The DD / MM / YYYY boxes: digits only, roll on to the next box. |
| `js/shared/loader.js` | The full-screen wait (`showLoader`, `hideLoader`, `setLoaderHint`). |
| `js/shared/image-shrink.js` | Shrinks a large photo before upload (`data-shrink`). |
| `js/shared/sticky-app-strip.js` | The application strip under the header once the page head scrolls away (`data-pin-head`). |
| `js/shared/notices-bell.js`, `js/shared/topbar.js` | The bell panel and the phone menu. |
| `js/shared/console.js` | Toast and list search carried over from the console. |

Shared partials in `Views/Shared/`: `_DocSlot` (one document card), `_DocLog` (a document's history), `_HolderIdentification` (the PAN / date of birth / folio search block), `_Drafts` (applications to pick up again), `_ApplicationSummary` (the six review sections, editable or read-only), `_RequiredDocs` (the dashboard's document-list dialog), `_ReadCard(s)`, `_ProofType`, `_NsdlRetry`, `_AadhaarNumber`, `_Notices`, `_ClassicSteps`.

## Naming

**CSS classes** are `block__part--variant`. The block starts with a plain word for the
page or the thing it is:

| Prefix | Used for |
|---|---|
| `page-` | the journey shell shared by every step: `page-layout`, `page-rail`, `page-card`, `page-title`, `page-head`, `page-note`, `page-action-bar` |
| `field-`, `date-input`, `choice-toggle`, `radio-option` | form controls |
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
`UnoTP.Backend` holds the rules and the outside services (NSDL, IDfy, the shortener),
`UnoTP.Data` the SQL layer over the `t_Unotp_` tables, `UnoTP.Backend.Mock` the demo
data. `Infrastructure/` holds the cross-cutting parts: feature gate, security headers,
partner session, URL building. Every method has a `///` summary or a comment above it.

## Rules to keep

- **No inline `style=` or `<script>` in a view.** The Content-Security-Policy blocks
  them; put styles in the page's stylesheet and behaviour in its script.
- **Sizes are on the scale** in BRAND_GUIDELINES.md: text 12 / 14 / 16 / 20px, fields
  and buttons 36px, spacing in multiples of 4. `--doc-card-h` keeps every document
  card in a row the same height.
- **Disable, don't hide.** Something the partner cannot use yet is shown disabled with
  its reason (a `hover-hint` or an `alert`), not removed.
- **Razor writes a `data-*` attribute even when its value is null.** Render two
  variants of the element instead of passing null.
- **Nothing outside is retried**: an IDfy or NSDL call may be charged.

## Running and checking

```
dotnet run --project src/UnoTP --launch-profile http      # port 5102; PORT=5103 for a second copy
dotnet test                                               # routes, journey rules, status stages, links
```

Stylesheets and scripts are linked with `asp-append-version`, so a change shows on
the next reload. The tests do not look at the pages: after a UI change, open the page
at desktop and phone width and check it by eye against BRAND_GUIDELINES.md.
