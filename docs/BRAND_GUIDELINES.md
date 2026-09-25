# Uno TP / E-Sarathi — Brand & UI Guidelines

Source of truth for visual consistency across all pages. Every value here is taken
directly from `wwwroot/css/site.css` and cross-checked against the prototype boards
in `prototype/` (Board 00, 01, 02, 02A, 02B, 03, 04, 05, 06 — laptop and mobile).
When building a new page or fixing an existing one, match these values rather than
introducing new ones. If a prototype board shows a value that conflicts with this
doc, the prototype wins — update this doc to match and note which board you checked.

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
`system-ui, sans-serif` fallback. Base body: `letter-spacing: .25px`, color `--ink`.

| Role | Size | Weight | Color | Class |
|---|---|---|---|---|
| Card / section title | 16px | 500 | `--text` | `.card__title` |
| Card subtitle | 11.5px | 400 | `--muted` | `.card__subtitle` |
| Holder / block title | 14–14.5px | 700 | `--ink` | inline / `.holder-block__title` |
| Body / field text | 12.5–14px | 400–500 | `--ink` / `--text` | — |
| Field label | 12px | 500 | `--text` | `.field label` |
| Hint / helper text | 11px | 400 | `--muted` | `.field-hint` |
| Uppercase eyebrow label | 11.5px* | 600–700 | `--muted` / `--muted-2` | `.section-label` (see note) |
| Button text | 14px | 600 | — | `.btn` |
| Small button text | 12.5px | 600 | — | `.btn-sm` |
| Small status chip | 10.5px | 700 | varies | `.chip` |
| Action link | 11.5px | 700 | `--red` | `.action-link` |
| Table body | 11–11.5px | 400 | `--ink` | `.data-table` |
| Table header | 9.5px | 700, uppercase | `--muted-2` | `.data-table thead th` |

*\*`.section-label` itself defaults to 10px — that's correct for the small uppercase
labels it was designed for (e.g. "Pinned" on the dashboard). But some prototype
section headers ("Holders on this application", "DPDP consent") render at **11.5px**
instead — check the specific board before assuming 10px is always right.*

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
