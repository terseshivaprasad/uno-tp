# UI upgrade: Bootstrap 5.3, page by page

Makes every page follow the research rules in [BRAND_GUIDELINES.md](BRAND_GUIDELINES.md#foundation-bootstrap-53-themed-to-the-brand),
keeping each page's layout and the brand. Order: dashboard, Upload Documents, Investor Information, then the
rest of the purchase journey, then the registers.

Counts are from the page's stylesheet: **sizes** = distinct font sizes, **<12px** = rules below 12px,
**muted-2** = uses of `--muted-2` (2.6:1, too faint to read).

## Phase 1 — foundation (done)

- [x] Vendor Bootstrap 5.3.8 into `wwwroot/lib/bootstrap`, loaded by `_Layout` and `_StatusLayout`
- [x] Brand theme in `css/shared/bootstrap-theme.css` (colours, Georama, radii, buttons, inputs, cards, badges, tables, focus ring)
- [x] Rename the classes that clashed with Bootstrap (`modal-dialog` → `app-dialog`, `toast` → `app-toast`)
- [x] Screenshot all 22 pages before and after; nothing moved beyond ~1px icon alignment
- [x] Tests 97/97
- [x] Rules written into BRAND_GUIDELINES.md
- [ ] Commit phase 1 and the drafts-list redesign (uncommitted on `sql-backend`)

## Every page — the same checklist

Do these for each page below, then tick the page off.

1. Screenshot the page (baseline) at 1440px and 390px.
2. Type: only 12 / 14 / 16 / 20px; nothing below 12px; line heights on the 4px grid.
3. Spacing: margins, paddings and gaps on 4/8px steps (Bootstrap spacers where they fit).
4. Colour: no read text in `--muted-2`; `--muted` is the lightest text.
5. No cut-off text: remove ellipsis truncation; let text wrap.
6. Targets: everything clickable at least 24×24px; the blue 2px focus ring shows.
7. Swap hand-made styles for Bootstrap components/utilities (`btn`, `form-control`, `form-select`, `card`, `badge`,
   `table`, `list-group`, grid) where the page draws the same thing; delete the CSS that's no longer used.
8. Keep the layout, the brand, and the disable-don't-hide rule.
9. Screenshot again, compare, and check the page by keyboard.
10. Tests pass; README/BRAND_GUIDELINES updated if a rule changed.

## Phase 2 — pages

### Priority

| # | Page | View | Styles | Sizes | <12px | muted-2 | Status |
|---|------|------|--------|------:|------:|--------:|--------|
| 1 | Dashboard | `Views/Dashboard/Index.cshtml` | `pages/dashboard.css` | 8 | 3 | 1 | [x] |
| 2 | Upload Documents | `Views/Documents/Index.cshtml`, `_DocSlot` | `pages/documents.css` | 6 | 9 | 1 | [x] |
| 3 | Investor Information | `Views/Investor/Index.cshtml` | `pages/investor.css` | 7 | 5 | 2 | [x] |

### Rest of the purchase journey

| # | Page | View | Styles | Sizes | <12px | muted-2 | Status |
|---|------|------|--------|------:|------:|--------:|--------|
| 4 | Investor Identification | `Views/NewApplication/Index.cshtml` | `pages/new.css` | 3 | 1 | 0 | [x] |
| 5 | Bank Details & Payment | `Views/Payment/Index.cshtml` | `pages/payment.css` | 2 | 1 | 0 | [x] |
| 6 | FD Configuration | `Views/Deposit/Index.cshtml` | `pages/deposit.css` | 7 | 6 | 0 | [x] |
| 7 | Review Summary | `Views/Review/Index.cshtml` | `pages/review.css` | 9 | 9 | 1 | [x] |
| 8 | Submitted | `Views/Submitted/Index.cshtml` | `pages/submitted.css` | 9 | 14 | 1 | [x] |
| 9 | Renew FD | `Views/Renew/Index.cshtml` | `pages/renew.css` | 0 | 0 | 0 | [x] |

### Registers and admin

| # | Page | View | Styles | Sizes | <12px | muted-2 | Status |
|---|------|------|--------|------:|------:|--------:|--------|
| 10 | Applications | `Views/Applications/Index.cshtml` | `pages/applications.css` | 4 | 3 | 1 | [x] |
| 11 | Links | `Views/Links/Index.cshtml` | `pages/links.css` | 1 | 0 | 0 | [x] |
| 12 | Pay-in Slips | `Views/PayInSlips/Index.cshtml` | `pages/pay-in-slips.css` | 2 | 0 | 0 | [x] |
| 13 | Admin | `Views/Admin/Index.cshtml` | `pages/admin.css` | 5 | 2 | 3 | [x] |
| 14 | Status pages | `Views/Entry/*`, `_StatusLayout` | `pages/status.css` | 3 | 0 | 0 | [x] |

Pages 4–14 were reviewed by measurement (hierarchy outline, type, edge spacing, phone
width) rather than by screenshot. Submitted was reviewed from its markup: no application
in the development data has been submitted.

### Shared styles (as the pages that use them move)

| Styles | Sizes | <12px | muted-2 | Used by | Status |
|--------|------:|------:|--------:|---------|--------|
| `shared/layout-and-controls.css` (incl. drafts list, dialogs, toasts) | 14 | 29 | 9 | every page | [x] |
| `shared/journey-steps.css` (steps, fields, cards) | 14 | 44 | 7 | purchase journey | [x] |
| `shared/topbar.css` | 10 | 12 | 3 | every page | [x] |
| `shared/register-pages.css` | 10 | 10 | 3 | registers | [x] |

## Done alongside

- [x] Header hierarchy on Upload Documents: the name leads (20px), captions 12px, a rule under the head
- [x] Document cards: label and toolbar on one header line (`DocSlotBlock.Head`), on Upload Documents, Investor Information and FD Configuration
- [x] Step rail: 12px inset on every step, only the current step marked out; register side notes off the card
- [x] Back to dashboard above every non-journey page (`ViewData["BackToDashboard"]`); not on journey steps
- [x] View Application status from entry to FDR: entry, short link, acceptance, pay-in slip, penny drop,
      payment, KYC verification, FDR (`ApplicationStages`, `db/005_unotp_status.sql`, 17 tests)
- [x] Tables and search forms: cells padded on every side, rows and form rows 24px apart
- [x] Audits: type (`audit.js`), edge spacing (`edges.js`: content never closer than 8px to a box's
      side), phone width (`wide.js`)

- [x] Spacing: every padding, margin and gap on the 4px grid (259 values snapped)
- [x] Hierarchy: one lead per page - Review's later cards and Admin's forms are 16px sections;
      Admin's and Submitted's titles 20px semibold
- [x] Bootstrap components: 30 buttons (`btn btn-primary`, `btn btn-outline-primary`), 8 chips
      (`badge`, via `Tones.Badge`), 22 notes and banners (`alert alert-*`, the brand's accent in the theme),
      the dialogs' close buttons (`btn-close`); the old `.csi-btn`, chip and note looks deleted
- [x] One dialog frame (`app-dialog__head/__title/__sub/__close/__foot`) for every sheet dialog
- [x] A redraw keeps the page where it was (Safari has no scroll anchoring); smooth scroll off
- [x] Row spacing: document rows 32px apart, field rows 24px

## Phase 3 — clean-up

- [ ] Remove the compat block at the end of `bootstrap-theme.css` (legend, hr, heading line height) once no page relies on it
- [ ] Set `--bs-body-line-height` to the type scale instead of `normal`
- [ ] Delete CSS no page uses any more
- [ ] Final screenshot pass of all pages, desktop and phone width; keyboard and contrast check
- [ ] Update README and BRAND_GUIDELINES; commit
