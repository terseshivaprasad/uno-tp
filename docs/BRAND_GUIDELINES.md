# Uno TP / E-Sarathi — Brand & UI Guidelines

Source of truth for visual consistency across all pages. Every value here is taken
directly from `wwwroot/css/site.css` and cross-checked against the prototype boards
in `prototype/` (Board 00, 01, 02, 02A, 02B, 03, 04, 05, 06 — laptop and mobile).
When building a new page or fixing an existing one, match these values rather than
introducing new ones. If a prototype board shows a value that conflicts with this
doc, the prototype wins — update this doc to match and note which board you checked.

## Foundation: Bootstrap 5.3, themed to the brand

Pages are built on **Bootstrap 5.3** (`wwwroot/lib/bootstrap`, MIT, served locally,
never from a CDN) with **vanilla JavaScript** - Bootstrap 5 needs no jQuery, and the
app uses none. `css/shared/theme.css` points Bootstrap's variables at the brand
tokens below, so a Bootstrap button, form control, card, badge, table or list group
comes out in the brand: Mahindra red for the main action and for what is wrong,
Georama, ink text, 8px cards, 6px buttons and controls, 3px chips, a blue focus ring.

Page by page, a page's hand-made styles give way to Bootstrap's components and
utilities, keeping its layout: use `.btn .btn-primary`, `.form-control`,
`.form-select`, `.card`, `.badge`, `.table`, `.list-group`, the spacing utilities
(`p-3`, `gap-2`, `mb-4` - Bootstrap's 4/8px steps) rather than a new rule. Name a
class of our own with a prefix of its own (`cud-`, `cii-`, `app-`): never a
Bootstrap name (`modal-dialog`, `toast`, `card` ...) for something that is not that
Bootstrap component - `app-dialog` and `app-toast` are ours for exactly that reason.

The rules the research behind this settled on (NN/g, GOV.UK, IBM Carbon, Material,
Apple HIG, WCAG 2.2):

- **Type:** a few sizes with clear roles - 12px captions, helper text, chips,
  field labels, step-rail labels and the small labels over a fact; 14px body,
  controls and buttons; 16px section and step titles (the investor's name on a
  step); 20px only for the dashboard's title - never below 12px; line heights on a 4px grid (16/20/24/28); headings semibold, body
  regular. No uppercase eyebrows: a caption is sentence case in `--muted`.
- **Hierarchy:** one thing leads each block. A step's head is caption (the step),
  title (the name), then the facts; a document card's head is its label at the
  left and its toolbar at the right, on one line.
- **Spacing:** Bootstrap's 4/8px scale for padding, margins and gaps; related
  things close together, groups apart (proximity).
- **Contrast:** text at least 4.5:1 - `--muted` (#6B7280) is the lightest text
  colour; `--muted-2` is for placeholders and disabled controls only (2.6:1).
- **Targets and focus:** anything pressed at least 24px square; focus always shows
  the 2px blue ring.
- **Text is never cut off** with an ellipsis: it wraps, so enlarged or re-spaced
  text stays readable.
- **Edges:** anything drawn as a box - a card, a panel, a highlighted row, a table
  row's hover, a button - keeps its content at least 8px from its sides (6px in a
  chip or small tool) and 4px from top and bottom. A highlight is never added to a
  row that has no inset of its own.
- **Supporting content steps back:** help beside a page sits on the page, not on a
  card, in muted text; navigation marks out only where you are.

## Colors

All colors are CSS custom properties on `:root` in `site.css`. Always reference the
variable, never hardcode the hex, so a future palette change is a one-line edit.

| Token | Hex | Use |
|---|---|---|
| `--red` | `#E31837` | Primary brand red — CTAs, links, active states, primary accents |
| `--red-dark` | `#B3132B` | Hover state for red buttons; "danger" emphasis text |
| `--ink` | `#231F20` | Primary text, headings |
| `--text` | `#4D4D4F` | Secondary body text |
| `--muted` | `#6B7280` | Tertiary text, hints, captions |
| `--muted-2` | `#9AA1AB` | Quietest text — placeholders, uppercase eyebrow labels |
| `--border` | `#E7EAEE` | Default hairline border |
| `--border-2` | `#D9DEE5` | Slightly darker hairline (card outlines) |
| `--border-3` | `#ced4da` | Form input / button-secondary border |
| `--page-bg` | `#E9EDF2` | `<body>` background — not `--tint` |
| `--card-bg` | `#fff` | Cards, wizard content, modals |
| `--tint` | `#F5F8FB` | Lighter inline highlight panels *inside* a card (e.g. "record found" box) — distinct from page background |
| `--divider` | `#EDEFF3` | Light row/section divider |
| `--divider-2` | `#DCDCDC` | Slightly stronger divider (card headers, action bars) |
| `--amber` / `--amber-bg` | `#664d03` / `#FFF3CD` | Warning text / warning chip background |
| `--green` / `--green-2` | `#146C34` / `#1E9E52` | Success text / success icon-fill (e.g. done badges) |
| `--blue` | `#1063A8` | Informational accent (info callouts, "primary holder" chip) |
| `--pink-bg` | `#FDECEF` | Danger chip background |
| `--green-bg` / `--green-border` | `#E8F5EC` / `#CBE7D5` | Success panels and "verified" tags |
| `--blue-bg` / `--blue-border` | `#E8F1FB` / `#CBDFF3` | Info panels and chips |
| `--pink-tint` / `--pink-border` | `#FFF8F9` / `#F3C9D1` | A row or card being pointed at; refused copies |
| `--amber-tint` / `--amber-border` / `--amber-accent` | `#FFFBF0` / `#F0D98A` / `#E8A700` | Warning panels, their outline, and their left rule |

The tints were added when every raw hex in `site.css` and `topbar.css` was
replaced by a token: near-duplicates (a dozen greys, five greens, six pinks) were
folded into the nearest token. There is no raw hex left outside `:root`; keep it
that way.

**`--page-bg` vs `--tint`:** these look similar but are not interchangeable.
`--page-bg` (`#E9EDF2`) is the body background behind every card. `--tint`
(`#F5F8FB`) is a lighter panel used *inside* white cards to highlight a block of
content without a border (confirmed via Board 02A's "record found" panel). Getting
this backwards was a real bug caught in this project — don't reintroduce it.
The classic pages (dashboard, Investor Identification, Upload Documents, Investor
Information and the list pages) once set their own `#F5F7FA`; every page now sits
on `--page-bg`.

## Typography

Font: **Georama** (Google Fonts, weights 300–700), loaded in `_Layout.cshtml` with
`system-ui, sans-serif` fallback. Base body: `letter-spacing: 0`, color `--ink`; headings `-.01em`.

| Role | Size / line | Weight | Color | Example |
|---|---|---|---|---|
| Dashboard title | 20 / 28px | 400 | `--text` | `.classic-steps__title` |
| Step / page title | 16 / 24px | 600 | `--ink` | `.cud-head__name`, `.cii-title` |
| Section title | 16 / 24px | 600 (400 on the dashboard) | `--ink` | `.cud-extra__title`, `.classic-actions__title` |
| Body, field text, controls | 14 / 20px | 400–500 | `--ink` / `--text` | `.csi-control`, `.seg-toggle__opt` |
| Field label, rail label | 12 / 16px | 600 | `--ink` | `.csi-label`, `.form-label`, `.csi-rail__label` |
| Button | 14px | 500–600 | — | `.btn` (36px high, as tall as a field) |
| Caption, helper, meta | 12 / 16px | 400–500 | `--muted` | `.csi-hint`, `.cud-head__step` |
| Chip / status tag | 12 / 16px | 600 | varies | `.badge`, `.cud-held__tag` |

Sizes follow the old eSarathi screens' scale (measured at 1470px): fields and
buttons 36px, the step rail 224px with 16px icons, document boxes 136px, dashboard
tiles 128px with 48px icons.
| Small tool | 12 / 16px | 600 | `--red` | `.cud-tool` (26px high) |

Nothing is set below 12px and nothing is uppercase. Icon glyphs (the "i" of an info
mark) are drawings, not text, and are exempt.

### Chips vs. tags — don't mix these up

Two different small-label components exist and they are **not** interchangeable:

- **`.tag`** — uppercase, pill-shaped (`border-radius: 9px`), letter-spaced. Used for
  the app-tile badges and a few legacy spots.
- **`.chip`** — normal case, tighter rectangle (`border-radius: 3px`), no letter
  spacing. This is what the prototype actually uses for inline status labels like
  "Primary", "Optional", "Identified", "Consent complete". **Use `.chip`, not
  `.tag`, for any small inline status label next to a name or heading** — using
  `.tag` here was a real bug (it rendered "PRIMARY" in caps when the design shows
  "Primary").

Both come in the same semantic variants: `--primary`/`--muted` (or `--optional`),
`--success`, `--warn`, `--danger`.

## Colour carries meaning

Solid `--red` is for the one main action on a screen (Proceed, Search) and for what
is wrong (errors, refusals). A *selected* option - a chip, a toggle - is drawn with
`--pink-bg`, a `--red` outline and `--red-dark` text, so selection never reads as an
error. The card that holds the next thing to do is outlined in `--blue` with a
`--blue-bg` ring. A card with nothing to do (not applicable, done) takes the lighter
`--border`. Status is a chip tinted by what it means: green done, amber waiting,
red refused or cancelled, grey neutral.

Explain less in words: a check that passed says so behind an "How it was checked"
info tip; warnings and failures stay written out. Trade terms (CKYC, NSDL, POA, CMS,
IFSC, MICR, TDS, 15G/15H, FATCA, OCR) go through `Glossary.Term`, which explains them
on hover and focus.

## Spacing & radius

- Card padding: `18px 20px` (`.card`) or `24px` (`.wizard__content`); `14px 16px` on
  mobile (`<560px`).
- Card-to-card gap: `16px` vertical.
- Grid gaps: `16px` (form grids, wizard body), `11px` (app tile grid), `12px 20px`
  (kv-grid).
- Border radius: **8px** for cards/panels/dialogs/sheets, **6px** for buttons and
  icon buttons, **3px** for chips (the agency badge, a card's status tag), **9px**
  for tag pills, **999px** for a full capsule (toggles, filter pills), **50%** for
  avatars/circular badges. Nothing else: 10px, 12px and 14px cards were folded into 8px.
  There is no 4px radius anywhere in the current design — if you see one, it's a
  leftover bug (this project had one on `.btn` that's since been fixed).
- Buttons: `padding: 12px 22px` (default), `8px 14px` (`.btn-sm`). A `<button>`
  inherits Georama from `.btn`; without it a browser draws the button in Arial.

## Layout chrome

- `.app-header`: fixed 44px top bar, white background, sticky.
- `.app-shell`: `max-width: 1536px`, centered, `16px` padding — everything below the
  header lives inside this.
- Wizard pages: `.wizard__body` is a `240px` rail + fluid content two-column grid,
  `16px` gap. The rail and content are both white rounded (`8px`) panels sitting on
  the page background.
- **No "Application No / Step X of 5" topbar** above the wizard rail+content grid —
  no prototype board has this. If you see one, remove it (this was a real
  discrepancy fixed in this project).
- **CTA / action bar on multi-step pages**: the prototype fixes the Back/Clear
  All/Proceed bar to the *bottom of the browser window*, as its own bar separate
  from the content card (not nested inside it, not just "sticky within the card").
  Implement this as `position: fixed; left:0; right:0; bottom:0` on a bar that is a
  **sibling of the wizard card**, with its own inner wrapper capped to
  `.app-shell`'s `max-width` so its buttons line up with the content above. See
  `.hid-footer` / `.hid-footer__inner` in `site.css` and the `Footer` `@section` in
  `_WizardLayout.cshtml` for the reference implementation. Do **not** use
  `position: sticky` nested inside the card for this — it let the bar escape the
  page's width constraint in a real regression during this project.

## Components quick-reference

| Component | Class | Notes |
|---|---|---|
| Primary/secondary button | `.btn .btn-primary` / `.btn .btn-secondary` | Small variant: add `.btn-sm` |
| Status chip | `.chip .chip--{primary,muted,success,warn,danger}` | Inline status next to a name/heading |
| Uppercase pill tag | `.tag .tag--{primary,optional,success,warn,danger}` | App-tile badges, not inline status |
| Card | `.card` | General content card |
| Data table | `.data-table` | Zebra/attention row via `.attention` class |
| Callout | `.callout` (red) / `.callout .callout--info` (blue) | Left-border accent box |
| Numbered holder badge | `.holder-num .holder-num--{current,done,todo,warn}` | Circular step/holder indicator |
| Segmented toggle | `.seg-toggle .seg-toggle__opt .seg-toggle__opt--active` | e.g. Offline/Digital consent choice |
| Radio-style toggle | `.radio-toggle .radio-toggle__dot .radio-toggle__dot--checked` | Custom radio look |

## Verification status

Confirmed against decompressed prototype boards (colors, spacing, type, components):
Board 00, 01 (dashboard/console — desktop + mobile), Board 02, 02A, 02B, 03, 04, 05,
06 (holder identification sub-steps, desktop). **Not yet checked**: Board 07–11
(later wizard steps — upload documents, bank details, FD configuration, review,
submitted) and the mobile variants of Board 02–06. Before styling those pages,
decompress their prototype exports the same way (see git history around commit
`ca8b130` for the extraction method) rather than guessing — that's what caused the
mismatches this doc exists to prevent.
