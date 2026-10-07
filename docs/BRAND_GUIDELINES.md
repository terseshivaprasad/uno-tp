# Uno TP: UI guidelines

The one place that says how a page should look. Read it before changing a view or a
stylesheet, and before adding a page.

## Where the rules come from

The design is the Figma file "FD Broker portal". Its guideline boards were received
on 6 Oct 2026 as photos of the screen: Spacing, Common Building Block, Typography,
Buttons and Icons. Every number below marked **design** was read off those boards.

- The design wins. If a board and this file disagree, change this file, the token
  and the test together, and say which board you read.
- Where the boards say nothing (colours as values, button sizes, phone layouts),
  the app keeps what it had. Those are listed under "Not settled yet".

## How the rules are kept

1. **One value, one place.** The frame and the type are named values (`:root` in
   `UnoTP/wwwroot/css/shared/layout-and-controls.css`). A rule uses the name, never
   the number: `gap: var(--field-gap)`, `font: var(--font-label)`.
2. **A test holds them.** `UnoTP.Tests/UiGuidelineTests.cs` runs with `dotnet test`.
   It fails when a design value is changed, when text is given a size off the scale,
   or when a view gets a `style=` attribute or a `<style>` block.
3. **A checklist for what a test cannot judge** (at the end of this file), and
   before-and-after screenshots of the page at 1360px and at a phone's width.

## Spacing

### The building block (design: Common Building Block)

Spacing between parts of a page is a multiple of 8px:

`8  16  24  32  40  48  56  64  72  80`

### The page frame (design: Spacing, drawn at a 1360px window)

| What | Value | Token |
|---|---|---|
| Header height | 46px | `--header-height` |
| Header to the first card | 32px | `--page-gap-top` |
| Window edge to the rail, and to the card on the right | 47px | `--page-gap-side` |
| Step rail width | 242px | `--rail-width` |
| Rail to the card beside it | 24px | `--rail-gap` |
| Card width | 1000px at a 1360px window; it takes what is left on a wider or narrower one | none |
| One card to the next under it | 18px | `--card-gap` |
| Last card to the action bar | 24px | `--page-gap-bottom` |
| Action bar height | 64px | `--action-bar-height` |
| Card edge to what is in it | 24px | `--card-padding` |
| One field to the next, across and down | 32px | `--field-gap` |

46, 47 and 18 are not multiples of 8. They are what the board says, so they are kept
as written.

The frame is for a laptop. Below 900px the rail no longer fits beside the card, so
the margins drop to 16px and the field gap to 24px; below 560px the card padding and
the field gap are 16px. These are the app's own, not the design's: no phone board
has been received.

The header's side margin is the page's, so the logo starts where the rail does and
Logout ends where the card does. The action bar's buttons line up the same way.

## Type (design: Typography)

Georama throughout, served from `wwwroot/fonts`. "500 16px/19px" reads: medium
weight, 16px text on a 19px line.

| Design name | Used for | Value | Token |
|---|---|---|---|
| Display 1 | A card's or a section's title | 500 16px/19px | `--font-title` |
| Display 2-3 | The label over a field or over a fact | 500 12px/24px | `--font-label` |
| Display 2-3 | What a field or a fact holds | 500 14px/24px | `--font-value` |
| Display 2 | A question put to the partner | 500 14px/20px | `--font-question` |
| Display 2 | An answer to choose (Yes, No) | 400 14px/17px | `--font-option` |

A rule takes a role in one line:

```css
.my-page__title {
  font: var(--font-title);
  color: var(--ink);
}
```

`font:` sets the size, weight, line height and family together. Write it before any
other `font-` line in the rule, or it undoes that line.

Text is 12, 14 or 16px. 20px is kept for the one lead of the dashboard and of a
dialog. Nothing is below 12px and nothing is uppercase. Text is never cut off with
an ellipsis: it wraps.

Fields and buttons are 36px high.

## A row of fields

The boxes in a row sit on one line, always: as the page opens, with an error under
one of them, with a hint under another.

- The row lines its fields up from the top (`align-items: start`, a grid's and a
  flex row's default). Never from the bottom: an error under one field would then
  lift its box above the others.
- A label is one line. A note about the field ("No paper form - filed as 0000")
  goes under the box as a `field-hint`, not inside the label: a label that runs
  onto a second line pushes its box below the others.
- Errors and hints go under the box and grow downward.
- A button beside the fields has no label, so its cell takes `form-field--check`,
  which starts it one label lower, level with the boxes.
- If a row gets too narrow for its labels, give it fewer columns sooner.

## Buttons (design: Buttons)

| Design name | Looks like | Class |
|---|---|---|
| Primary | Solid red, white words. The one main action of a screen. | `btn btn-primary` |
| Secondary | White, red outline, red words. Back, Save draft. | `btn btn-outline-primary` |
| Tertiary | Words only, no box. Clear All, Edit, Add nominee. | `link-button` |

The action bar of a journey step has Back at the left, the tertiary action and the
primary action at the right.

There is no grey button: the board has only these three.

## A choice between two

A choice with two answers (Digital or Physical, Same as Permanent or Different from
Permanent) is drawn as a switch, the way the FATCA questions are: the first
option's name, the switch, the second option's name. Off is the first option, on
the second; the chosen option's name is in ink, the other's muted.

Every switch is the design's toggle (design: Buttons, Icons): grey when off, blue
when on (`--selected`), a white knob. Red is not used for a switch: on is a
choice, not the main action and not an error.

The same blue marks the tenure and the interest payout chosen on FD Configuration:
the tile is outlined in `--selected`, on `--blue-bg`, with its words in `--blue`. Use the shared
partial `Views/Shared/_ChoiceSwitch.cshtml`. An option that cannot be chosen stays,
faded, and says why on hover.

## Icons (design: Icons)

Filled document icons in slate for the dashboard tiles; outlined icons for the step
rail and the four steps. One family: do not mix in another icon set. An icon is an
inline `<svg>` that takes its colour from the text (`currentColor`).

An icon that is pressed (View, Upload, Replace over a document card: `doc-tool`)
has no box of its own. The card under it is the only box; the icon is red when it
can be pressed, grey with its reason on hover when it cannot, and tinted behind on
hover. Its padding keeps it 24px to press.

## Boxes

Keep the boxes on a page few. A box (a border or a filled panel) is for three
things only: the card, something that is typed in or picked, and a notice that
asks the user to do something. Everything else stands on the card as plain text.

- **What was read or found and cannot be typed over** (`investor-panel`, FD
  Configuration's `deposit-summary`): no fill, a line down its left.
- **A summary** (Review Summary, View Application's details:
  `investor-panel--plain`): the values straight on the card, a rule between holders.
- **The tenures and the payouts on FD Configuration stay a tile each**
  (`deposit-options`): the user asked for that design to be kept (8 Oct 2026).
- **A state or a type** ("Filed", "Manual entry", the proof type): the word in its
  colour, no box round it.
- **Why a field stands as it does** (`doc-note`), or what was read (`bank-verified`):
  hint text, not a notice.
- **A list of rows** (`doc-checks`): a line between the rows, no frame round them.
- **A small action beside a card or a value** (`doc-tool`, `doc-copy`): the icon alone.
- **A status in a list** (`status-word`, from `Tones.Word`): the word in its tone's
  colour. Not a Bootstrap `badge`.
- **A list of applications** (`draft-list`): a line between the rows; Cancel is the
  tertiary button, its word alone.
- **Pages of a list** (`register-pager`): "Page 2 of 30" between two arrows, not a
  box for every page.
- **A section inside a card** (Application Submitted's `submitted-card`): its title
  over a rule; where to go next is a link with a line under it.

Kept as boxes: the dashboard's tiles (they are on the board), the filters over a
list, and a notice that warns (the yellow notes, the amber and red banners).

A filter over a list (`register-pill`) is drawn as the tenure and payout tiles are:
6px corners, the same outline, and when chosen outlined in `--selected` on
`--blue-bg` with its words in `--blue`. No rounded pill, and no red: red is the
main action and an error.

## Colours

Named values on `:root` in `layout-and-controls.css`. Use the name, never the code.

| Token | Code | Use |
|---|---|---|
| `--red` | `#E31837` | The main action, links, what is wrong |
| `--red-dark` | `#B3132B` | Red on hover; error text |
| `--ink` | `#231F20` | Headings, labels, values |
| `--text` | `#4D4D4F` | Body text |
| `--muted` | `#6B7280` | Captions and hints: the lightest text that is read |
| `--muted-2` | `#9AA1AB` | Placeholders and disabled controls only |
| `--border`, `--border-2`, `--border-3` | `#E7EAEE`, `#D9DEE5`, `#ced4da` | Hairline, card outline, field outline |
| `--page-bg` | `#E9EDF2` | Behind every card |
| `--card-bg` | `#fff` | Cards and dialogs |
| `--tint` | `#F5F8FB` | A highlighted panel inside a card. Not the page background. |
| `--divider`, `--divider-2` | `#EDEFF3`, `#DCDCDC` | Lines between rows and sections |
| `--green`, `--green-bg`, `--green-border` | `#146C34`, `#E8F5EC`, `#CBE7D5` | Done, verified |
| `--amber`, `--amber-bg`, `--amber-border`, `--amber-accent` | `#664d03`, `#FFF3CD`, `#F0D98A`, `#E8A700` | Waiting, warning |
| `--selected` | `#0078D4` | What is chosen or switched on: a switch that is on, the tenure and payout chosen. Read off a photo of the design's toggle: to be confirmed. |
| `--blue`, `--blue-bg`, `--blue-border` | `#1063A8`, `#E8F1FB`, `#CBDFF3` | Information, focus, the next thing to do |
| `--pink-bg`, `--pink-tint`, `--pink-border` | `#FDECEF`, `#FFF8F9`, `#F3C9D1` | A selected option; a refused copy |

Colour carries meaning. Solid red is the one main action and what is wrong. A
selected option is pink with a red outline, so selection never reads as an error.
Status is a chip tinted by what it means: green done, amber waiting, red refused,
grey neutral. Focus always shows the 2px blue ring.

Still to do: about 70 places write `#fff` and four write another code directly. They
move to the names when the design's colour values are received.

## Corners

8px for cards, panels and dialogs. 6px for buttons and fields. 3px for chips. A full
capsule (999px) for toggles and filter pills. 50% for round badges.

## Not settled yet

These were seen on the boards but could not be read from a photo, or were not on
the boards at all. Nothing was changed for them.

- **Colour values.** The toggle is blue when on, as on the Buttons board, but its
  exact code is not known: `--selected` is the nearest reading of a photo. The
  second tertiary button is blue too. The design's own colour codes are needed.
- **Button size and type.** The app's are 36px high, 14px semibold.
- **Field height.** The app's is 36px.
- **18px between cards** and **46 / 47px** in the frame are off the 8px block. Kept
  as the board says.
- **Inside a component** (a chip's padding, an icon beside its word) the app still
  uses 4, 6, 12 and 20px steps. Whether these must move to the 8px block is not
  decided.
- **Phone and tablet layouts.**
- **Pages other than a journey step** (dashboard, the lists). They take the frame's
  header, margins and gaps; the design's own boards for them have not been received.

## Pages moved onto the guidelines

| Layer | Frame | Type roles | Field spacing | Own stylesheet checked |
|---|---|---|---|---|
| Shared (header, rail, cards, action bar, fields, facts) | done | done | done | 6 Oct 2026 |
| Dashboard | done | titles done | – | to do |
| Upload Documents | done | done | done | to do |
| Investor Information | done | done | done | to do |
| Bank Details & Payment | done | done | done | to do |
| FD Configuration | done | done | done | to do |
| Review Summary, Submitted | done | done | – | to do |
| View Application, Pay-in Slips, Short URL, Admin | done | done | done | to do |

On 7 Oct 2026 every page was measured in the running app against the boards, and
what did not match was fixed: card and section titles, labels, values and questions
take the `--font-` tokens; fields are `--field-gap` apart; the dashboard's and the
submitted page's cards are padded `--card-padding`; the grey button is gone.
Sub-headings of 12 and 14px inside a card (a note's title, a document box's title)
are not on the boards and are as they were.

"Own stylesheet checked" means every title, label and value in `css/pages/{page}.css`
takes a `--font-` token, its gaps are on the 8px block, and its entries are gone
from the two "still to move" lists in `UiGuidelineTests.cs`.

## Checklist for a new or changed page

- The page sits in `page-layout` with `page-rail` and `page-main` / `page-card`; it
  sets no margins, rail width or card padding of its own.
- Titles, labels, values, questions and options take a `--font-` token.
- Fields sit in a grid with `gap: var(--field-gap)`.
- In every row of fields the boxes sit on one line. Look at it with an error
  showing, and at 1360, 1000 and 390px.
- Other gaps are multiples of 8px.
- One primary button to a screen; Back and Save draft are secondary; the rest are
  tertiary.
- Colours are tokens.
- No `style=` and no `<style>` in the view.
- Something the partner cannot use yet is shown disabled with its reason, not hidden.
- `dotnet test` passes.
- Looked at in the running app at 1360px and at 390px, beside a screenshot from
  before the change.
