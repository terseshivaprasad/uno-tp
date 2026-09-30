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
| `src/UnoTP.Api` | The backend API itself: applications kept in SQL Server through Dapper, everything else answered by the mock for now (see Backend API below) |
| `db/` | The SQL Server scripts for the application tables |
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

## Backend API

`src/UnoTP.Api` answers every route in `docs/backend-api.md` from SQL Server through
Dapper: the purchase journey end to end, the dashboard's lists, sign-in and menus,
the lists and rules, and the console. Still on the mock: the deposits Renew FD lists,
and the outside services (NSDL, OCR, identification, verification, masking, the
PAN-Aadhaar link, face match, the portal's decryption). Documents are kept under
`Dms:Root` until DMS is wired in, and no SMS or e-mail gateway is: sends are logged.

```sh
sqlcmd -d UnoTP -i db/001_unotp_tables.sql       # the application tables
sqlcmd -d UnoTP -i db/002_unotp_platform.sql     # config, features, lists, partners, sessions, links, slips, console
sqlcmd -d UnoTP -i db/003_unotp_seed.sql         # config, features and lists: review with the business
sqlcmd -d UnoTP -i db/004_unotp_masters.sql      # folios, brokers, staff, IFSC, PIN codes, rate card
sqlcmd -d UnoTP -i db/900_dev_seed.sql           # development only: demo partners and master records
dotnet run --project src/UnoTP.Api --launch-profile http          # on 5110
Backend__BaseUrl=http://localhost:5110 dotnet run --project src/UnoTP --launch-profile http
```

Every script is safe to run again. No table has a foreign key: each row is written
inside the transaction that locks its application. The masters in `004` are loaded
from their sources of record before go-live (the FD system, the broker and staff
masters, the RBI's IFSC list, India Post, the rate card), as is `cmsLocations` in
`t_Ref_List`. The API needs `ConnectionStrings:UnoTP`, and refuses a call without an
open session (`X-Session-Id`) except entry, `reference` and `config`. The tests run
against it with `Backend__BaseUrl=http://localhost:5110 dotnet test`.

**How an application is kept.** `t_Application_Mst` holds one row per application:
its number, its partner, who it was opened for, and its version. Everything entered
on it is rows in the detail tables, which are only ever inserted into:

| Step | Tables |
|---|---|
| Upload Documents | `t_Upload_State` (the step as JSON), `t_Kyc_Documents` (one row per holder and document; `00` for the application's own) |
| Investor Information | `t_Kyc_Dtls` (per holder, with NSDL and CKYC), `t_Address_Dtls` (per holder and address type: `PER`, `COR`), `t_Nominee_Dtls` |
| Bank Details & Payment | `t_Payment_Bank_Dtls` (the payment account and instrument), `t_Bank_Dtls` (the repayment account) |
| FD Configuration | `t_Investment_Dtls` (the deposit, with category, sourcing and employee details) |

Each save is checked against the version the page read (`If-Match`), moves the
version on, and inserts that step's rows afresh with status `PEN`; the header points
each step at the version that is current. Submitting inserts every step once more
with status `APR`, with the rate, interest and maturity locked on `t_Investment_Dtls`,
and the application takes no saves after it. Earlier versions stay as the audit
trail. A page's working state (`t_Page_State`) is scratch and is overwritten.

## Configuration

Settings are in `src/UnoTP/appsettings.json`; in the environment use a double
underscore (`Backend__BaseUrl`).

| Setting | Meaning |
|---|---|
| `Backend:BaseUrl` | The backend API. Blank runs the mock, which only Development or a demo (`Features:DemoData`) may do: anywhere else the app will not start without it. |
| `Idfy:BaseUrl`, `Idfy:ClientId` | Idfy.Api for the document checks it has an endpoint for, and the name the app calls itself by on its `X-Client-Id` header (`unotp`). |
| `Shortener:BaseUrl`, `Shortener:ClientId` | UrlShortener.Api, which shortens the payment link on submit, and the `X-Client-Id` it is called with. Blank, the mock shortens; on a real backend the link goes in full. |
| `PaymentLink:Template` | The page the investor pays on, with `{appNo}` for the application's number. Blank, the app sends no link and the backend makes its own. |
| `Apps:eSarathiLogin`, `Apps:eSarathiConsole`, … | The other apps' addresses, for the links out and the session-expired redirect. |
| `Entry:DemoUserId`, `Entry:DemoSysCode` | The user demo mode signs in as. |

Feature switches, under `Features`:

| Switch | Default | What it does |
|---|---|---|
| `NewFd`, `PisGeneration`, `ViewApplication`, `ShortUrl`, `RenewFd` | on | A dashboard tile and its pages. Off, the tile is greyed and says why. |
| `ApplicationStatus`, `Admin` | off | The same, for tiles not built yet. |
| `DemoData` | off (on in Development) | Demo sign-in, the test data cards, `?agency=` to show the app as another partner. |
| `DocIdentification` | on | A proof's type is what the copy is identified as on upload; off, it is chosen from a drop-down first. |
| `CommProofUpload` | off | A different communication address is proved with an upload; off, it is typed on Investor Information. |
| `AllowOverrides` | off | `?ff=pis:off` may change switches for a browser. Never on in production. |

## Deploy

`render.yaml` builds the `Dockerfile` and checks `/health`, which asks nothing of the
backend. That deployment is the demo, so `render.yaml` turns `Features__DemoData` on
with a demo user. Before a deployment reaches a real backend: set `Backend__BaseUrl`,
take those demo settings out, and set `Apps__eSarathiLogin` so an expired session
returns to the portal.
