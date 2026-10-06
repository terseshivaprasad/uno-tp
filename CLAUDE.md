# Uno TP

The fixed-deposit application journey for E-Sarathi partners. ASP.NET Core MVC,
server-rendered pages, Bootstrap 5.3 themed to the brand, vanilla JavaScript.

## Before changing a view, a stylesheet or a script

Read `docs/BRAND_GUIDELINES.md` (how a page must look: the design's spacing, type,
buttons and colours, and the checklist for a page) and `docs/CODE_GUIDE.md` (where
each page's files are and how things are named).

- The page frame and the type are tokens on `:root` in
  `UnoTP/wwwroot/css/shared/layout-and-controls.css`. Use the token
  (`var(--field-gap)`, `font: var(--font-label)`), never the number.
- A number from the design changes in three places together: the token,
  `docs/BRAND_GUIDELINES.md`, and `UnoTP.Tests/UiGuidelineTests.cs`.
- No `style=` attribute and no `<style>` block in a view.
- Do not add to the "still to move" lists in `UiGuidelineTests.cs`. When a page is
  moved onto the guidelines, take its entries out and update the table of pages in
  `docs/BRAND_GUIDELINES.md`.
- Where the design says nothing, keep what the app has and ask. Do not guess a
  colour or a size from a photo of a board.

## Validation and error messages

Every validation and error message is in `UnoTP.Data/Messages.cs`, by page
(`docs/CODE_GUIDE.md` says how to add and change one; `docs/VALIDATIONS.md` is the
list, written from the master).

- Never write an error's or a validation's words in a controller, view model, view
  or script. Add the message to `Messages.cs` and use it by name.
- After changing `Messages.cs`, write the list again:
  `UPDATE_VALIDATIONS=1 dotnet test UnoTP.sln --filter ValidationListTests`.

## Checking a change

```
dotnet build UnoTP.sln -p:Strict=true
dotnet test UnoTP.sln
```

Then look at the page in the running app at 1360px and at 390px, beside a
screenshot from before the change.
