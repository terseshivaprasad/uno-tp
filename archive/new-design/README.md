# Archived: the new design

The new-design screens of Uno TP, set aside so the solution builds and serves only
the classic pages. Nothing here is compiled, served or deployed: it sits outside
`src/UnoTP`, which is the only project in `UnoTP.slnx` and the only folder the
Dockerfile builds from.

Paths mirror the repository, so a file goes back by moving it to the same path
without the `archive/new-design/` prefix.

| What | Where it lived |
|---|---|
| Uno TP dashboard (new design) | `src/UnoTP/Pages/Dashboard.cshtml` - `/Apps/UnoTp/Dashboard` |
| DPDP consent tracker | `src/UnoTP/Pages/ConsentTracker.cshtml` - `/Apps/UnoTp/ConsentTracker` |
| Application wizard, holder identification and upload steps | `src/UnoTP/Pages/Application/{HolderIdentification,UploadDocuments}.*` - `/Apps/UnoTp/Application/*` |
| Their scripts | `src/UnoTP/wwwroot/js/holder-identification.js`, `consent-tracker.js` |
| Consent plan (DPDP, CKYC and digital consent) | `src/UnoTP/Features/ConsentPlan.cs` |
| The full mock data they read | `src/UnoTP/Models/MockData.cs` |
| The design boards (standalone HTML) | `prototype/` |

The wizard's later steps - Investor Information, Bank Details & Payment, FD
Configuration, Review Summary and Submitted - are back, following on from the classic
Investor Identification and Upload Documents: `ApplicationController`, with their views
in `src/UnoTP/Views/Application` and `Views/Shared/_WizardLayout.cshtml`.

The app is ASP.NET Core MVC now, and what is kept here is still Razor Pages, so a
page comes back as a controller action and a view rather than by moving it.

## Bringing a page back

1. Turn the page into a controller action (its `@page` route as the action's route)
   and move its markup into `src/UnoTP/Views/<Controller>/`, dropping `@page`,
   `@namespace` and the page model; `asp-page` links become `asp-controller` and
   `asp-action`.
2. `MockData.cs` here is the whole of the original; the live one keeps only the
   in-flight applications the classic pages read. Put back whichever lists the
   page uses.
3. For the consent tracker or the wizard's holder step, move `ConsentPlan.cs` back
   and register it again in `Program.cs`:
   `builder.Services.AddScoped<ConsentPlan>();`
   The consent switches it reads (`DpdpConsent`, `CkycConsent`, `DigitalConsent`)
   were taken out of `FeatureFlags`, `FeatureSet` and `appsettings.json` as
   unused, so they have to be added back too.
4. Remove the page's address from the `archived` redirects in `Program.cs`, which
   currently send it to the classic dashboard.
5. Their styles were never moved: they are still in `wwwroot/css/site.css`.
