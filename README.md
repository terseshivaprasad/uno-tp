# Uno TP

The fixed-deposit application journey for E-Sarathi partners: search the investor,
upload and check their documents, take the holders' details, the bank accounts and
the deposit, review, submit. ASP.NET Core 8 MVC, server-rendered pages, no SPA.

## Run

```sh
dotnet run --project src/UnoTP --launch-profile http
```

Then open <http://localhost:5102/unotp>. With no backend configured the app runs on
its in-memory mock, and demo mode signs you in without the portal. The other
E-Sarathi apps it links to run beside it: the login portal on 5100, the console on
5101 (see `src/UnoTP/appsettings.Development.json`).

```sh
dotnet test          # unit tests for the rules, and the pages driven in a test host
```

## Layout

| Project | What it is |
|---|---|
| `src/UnoTP` | The web app: controllers, view models, Razor views, `wwwroot` |
| `src/UnoTP.Backend` | The typed client for the backend API, and one client per outside service (NSDL, OCR, masking, face match, PAN–Aadhaar link, IDfy) |
| `src/UnoTP.Backend.Mock` | An in-memory stand-in for all of it, with the test records the demo uses |
| `tests/UnoTP.Tests` | xUnit: the rules, and the routes and journey on the mock |
| `docs/backend-api.md` | Every call the app makes, for the backend team (also as PDF) |
| `docs/BRAND_GUIDELINES.md` | The visual rules every page keeps |

Inside `src/UnoTP`: `Controllers/` one per page, the application steps sharing
`ApplicationStepController`; `ViewModels/` one per page, `DocumentsViewModel` holding
the document rules; `Views/{Page}/`; `Infrastructure/` for routing, features, caching
and the session; `wwwroot/js` one small script per behaviour, `wwwroot/css/site.css`.

The app never touches a database. Everything an application holds is saved against
it on the backend, under its number (the `appNo` in every URL), so a page can be
reopened from any browser and every change is audited. The server session holds only
the sign-in.

## Pages

| Address | Page |
|---|---|
| `/unotp` | Dashboard |
| `/unotp/new` | Investor Identification: opens an application |
| `/unotp/applications/{appNo}/documents` | Step 1, Upload Documents |
| `/unotp/applications/{appNo}/investor` | Step 2, Investor Information |
| `/unotp/applications/{appNo}/payment` | Step 3, Bank Details & Payment |
| `/unotp/applications/{appNo}/deposit` | Step 4, FD Configuration |
| `/unotp/applications/{appNo}/review`, `…/submitted` | Review Summary, Submitted |
| `/unotp/renew` | Renew FD: a folio's deposits; Renew opens an application through the same steps |
| `/unotp/applications`, `/unotp/pay-in-slips`, `/unotp/links`, `/unotp/admin` | The other dashboard tiles |
| `/unotp/entry`, `/unotp/session-expired`, `/unotp/logout` | The way in and out |

The old addresses (`/Home`, `/Dashboard`, `/Apps/UnoTp/...`) redirect to these.

## Configuration

Settings are in `src/UnoTP/appsettings.json`; in the environment use a double
underscore (`Backend__BaseUrl`).

| Setting | Meaning |
|---|---|
| `Backend:BaseUrl` | The backend API. Blank runs the mock. |
| `Idfy:BaseUrl` | Idfy.Api for the document checks it has an endpoint for. |
| `Apps:eSarathiLogin`, `Apps:eSarathiConsole`, … | The other apps' addresses, for the links out and the session-expired redirect. |
| `Entry:DemoUserId`, `Entry:DemoSysCode` | The user demo mode signs in as. |

Feature switches, under `Features`:

| Switch | Default | What it does |
|---|---|---|
| `NewFd`, `PisGeneration`, `ViewApplication`, `ShortUrl`, `RenewFd` | on | A dashboard tile and its pages. Off, the tile is greyed and says why. |
| `ApplicationStatus`, `Admin` | off | The same, for tiles not built yet. |
| `DemoData` | on | Demo sign-in, the test data cards, `?agency=` to show the app as another partner. **Off in production.** |
| `DocIdentification` | on | A proof's type is what the copy is identified as on upload; off, it is chosen from a drop-down first. |
| `CommProofUpload` | off | A different communication address is proved with an upload; off, it is typed on Investor Information. |
| `AllowOverrides` | off | `?ff=pis:off` may change switches for a browser. Never on in production. |

## Deploy

`render.yaml` builds the `Dockerfile` and checks `/health`, which asks nothing of the
backend. Before a deployment reaches a real backend: set `Backend__BaseUrl`, turn
`Features__DemoData` off and `Entry__DemoUserId` blank, and set `Apps__eSarathiLogin`
so an expired session returns to the portal.
