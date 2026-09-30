# Uno TP

The fixed-deposit application journey for E-Sarathi partners: search the investor,
upload and check their documents, take the holders' details, the bank accounts and
the deposit, review, submit. ASP.NET Core 8 MVC, server-rendered pages, no SPA.

## Run

```sh
dotnet run --project src/UnoTP --launch-profile http
```

Then open <http://localhost:5102/unotp>. With no database configured the app runs on
its in-memory mock, and demo mode signs you in without the portal (see Data below
to run it on SQL Server). The other
E-Sarathi apps it links to run beside it: the login portal on 5100, the console on
5101 (see `src/UnoTP/appsettings.Development.json`).

```sh
dotnet test          # unit tests for the rules, and the pages driven in a test host
```

## Layout

| Project | What it is |
|---|---|
| `src/UnoTP` | The web app: controllers, view models, Razor views, `wwwroot` |
| `src/UnoTP.Backend` | The interfaces the pages read and their records, and one HTTP client per outside service (NSDL, OCR, masking, face match, PAN–Aadhaar link, decryption, IDfy, the shortener) |
| `src/UnoTP.Data` | The interfaces answered from SQL Server through Dapper, in process, and the document store |
| `src/UnoTP.Backend.Mock` | An in-memory stand-in for all of it, with the test records the demo uses |
| `db/` | The SQL Server scripts: tables, the platform seed, the masters, a development seed |
| `tests/UnoTP.Tests` | xUnit: the rules, and the routes and journey on the mock |
| `docs/backend-api.md` | What the pages ask of the data layer, interface by interface |
| `docs/BRAND_GUIDELINES.md` | The visual rules every page keeps |

Inside `src/UnoTP`: `Controllers/` one per page, the application steps sharing
`ApplicationStepController`; `ViewModels/` one per page, `DocumentsViewModel` holding
the document rules; `Views/{Page}/`; `Infrastructure/` for routing, features, caching
and the session.

### Styles and scripts, page by page

The shared layout (`Views/Shared/_Layout.cshtml`) loads everything shared; a page
adds only what its own body needs, in its `Styles` and `Scripts` sections.

| | Shared - loaded by the layout | A page's own |
|---|---|---|
| CSS | Bootstrap 5.3 (`lib/bootstrap`) and its brand theme `css/shared/bootstrap-theme.css`, then `css/shared/`: `fonts.css`, `layout-and-controls.css` (tokens, header, controls, cards), `journey-steps.css` (what the purchase journey's steps share: rail, form controls, document and read cards), `register-pages.css` (what the list pages share), `topbar.css` | `css/pages/{page}.css` |
| JS | Bootstrap's bundle (`lib/bootstrap/js`, vanilla - no jQuery), then `js/shared/`: `loader.js`, `field-checks.js`, `notices-bell.js`, `topbar.js`, `date-input.js`, `partial-forms.js`, `image-shrink.js`, `sticky-app-strip.js`, `console.js` | `js/pages/{page}.js` |

| Page | CSS | JS |
|---|---|---|
| Dashboard | `pages/dashboard.css` | — |
| Investor Identification | `pages/new.css` | — |
| Upload Documents | `pages/documents.css` | `pages/documents.js` (code search) |
| Investor Information | `pages/investor.css` | `pages/investor.js` (PIN code, guardian, nominee prompt) |
| Bank Details & Payment | `pages/payment.css` | `pages/payment.js` (bank search) |
| FD Configuration | `pages/deposit.css` | `pages/deposit.js` (quote) |
| Review Summary, Submitted | `pages/review.css`, `pages/submitted.css` | `pages/review.js` |
| View Application, Pay-in Slips, Short URL, Console Admin | `pages/applications.css`, `pay-in-slips.css`, `links.css`, `admin.css` | the same names |
| Renew FD | `pages/renew.css` | — |
| Session Expired, Unauthorized, Error (`_StatusLayout`) | `pages/status.css` | — |

No page has a script or style block of its own inside its markup; what a script
needs from the server is on the page as `data-` attributes.

The pages never touch the database: they read and write through the interfaces in
`UnoTP.Backend`, which `UnoTP.Data` answers. Everything an application holds is saved
under its number (the `appNo` in every URL), so a page can be reopened from any
browser and every change is audited. The server session holds only the sign-in.

## Pages

| Address | Page |
|---|---|
| `/Dashboard` | Dashboard |
| `/SearchInvestor` | Investor Identification: opens an application |
| `/UploadInvestorDocuments/{appNo}` | Step 1, Upload Documents |
| `/InvestorInformation/{appNo}` | Step 2, Investor Information |
| `/BankDetails/{appNo}` | Step 3, Bank Details & Payment |
| `/FDConfiguration/{appNo}` | Step 4, FD Configuration |
| `/ReviewSummary/{appNo}`, `/ApplicationSubmitted/{appNo}` | Review Summary, Submitted |
| `/RenewalDashboard` | Renew FD: a folio's deposits; Renew opens an application through the same steps |
| `/ViewApplication`, `/PayInSlip`, `/ShortUrl`, `/Admin` | The other dashboard tiles |
| `/Home/Index?UserId=…&SysCode=…`, `/Home/Home`, `/Home/LogOut`, `/Home/SessionExpired` | The way in from the portal, back to it, and out |

These are the old app's addresses, so the portal's links and saved links still work. There is no app prefix in any route: the IIS virtual directory (`/WA_FD_UNOTP/`) is the path base and is added to every address automatically; on another host set `PathBase`. Addresses the old app had that name no page here (`/Apps/UnoTp/...`, a step with no application number) redirect to the page that took their place.

## Data

One app: the pages and the data layer run in the same process, behind a WAF or
reverse proxy, with SQL Server in its own zone. `UnoTP.Data` answers the pages'
interfaces from SQL Server through Dapper: the purchase journey end to end, the
dashboard's lists, sign-in and menus, the lists and rules, and the console. Still on
the mock: the deposits Renew FD lists. The outside services (NSDL, OCR,
identification, verification, masking, the PAN-Aadhaar link, face match, the
portal's decryption) each answer at `Backend:External:{name}`, or IDfy's for the
checks it has. Documents are kept under `Dms:Root` until DMS is wired in, and no SMS
or e-mail gateway is: sends are logged.

```sh
sqlcmd -d UnoTP -i db/000_unotp_rename.sql       # a database made before the t_Unotp_ prefix: renames its tables (first)
sqlcmd -d UnoTP -i db/001_unotp_tables.sql       # the application tables
sqlcmd -d UnoTP -i db/002_unotp_platform.sql     # config, features, lists, partners, sessions, links, slips, console
sqlcmd -d UnoTP -i db/003_unotp_seed.sql         # config, features and lists: review with the business
sqlcmd -d UnoTP -i db/004_unotp_masters.sql      # folios, brokers, staff, IFSC, PIN codes, rate card
sqlcmd -d UnoTP -i db/005_unotp_status.sql       # penny drop and KYC verification, written by the Operations feed
sqlcmd -d UnoTP -i db/006_unotp_logs.sql         # the error log, t_Unotp_Logs
sqlcmd -d UnoTP -i db/007_unotp_rate_chart.sql   # the rate card by chart line, and the Samruddhi chart of 3 Aug 2026
sqlcmd -d UnoTP -i db/008_unotp_source_of_funds.sql # the source of funds on FD Configuration: columns, settings, list
sqlcmd -d UnoTP -i db/009_unotp_category_extra_rate.sql # what each category earns over the public rate, for the page's wording
sqlcmd -d UnoTP -i db/010_unotp_name_screening.sql # name screening's answer on the KYC row
sqlcmd -d UnoTP -i db/011_unotp_any_amount.sql     # any whole-rupee amount, not only multiples of 1,000
sqlcmd -d UnoTP -i db/012_unotp_amount_limit_and_sub_occupations.sql # the 5 crore maximum and its message; sub occupations by occupation
sqlcmd -d UnoTP -i db/013_unotp_link_validity.sql  # the payment link runs 3 days; a new one until the application cancels itself
sqlcmd -d UnoTP -i db/900_dev_seed.sql           # development only: demo partners and master records
export ConnectionStrings__UnoTP='Server=...;Database=UnoTP;...'   # never in a committed file
dotnet run --project src/UnoTP --launch-profile http
```

Every script is safe to run again. No table has a foreign key: each row is written
inside the transaction that locks its application. Every table has `f_Active`; only
active rows are read. The masters in `004` are loaded from their sources of record
before go-live (the FD system, the broker and staff masters, the RBI's IFSC list,
India Post, the rate card), as is `cmsLocations` in `t_Unotp_Ref_List`. Every page checks
the partner's session in `t_Unotp_User_Session` (at most 30 seconds old), so ending it, or
taking the partner out of use, signs them out. The tests run on the database with
`ConnectionStrings__UnoTP` set.

Outside Development (or a demo, `Features:DemoData`) the app will not start without
`ConnectionStrings:UnoTP`, or with an outside service left without an address.

**How an application is kept.** `t_Unotp_Application_Mst` holds one row per application:
its number, its partner, who it was opened for, and its version. Everything entered
on it is rows in the detail tables, which are only ever inserted into:

| Step | Tables |
|---|---|
| Upload Documents | `t_Unotp_Upload_State` (the step as JSON), `t_Unotp_Kyc_Documents` (one row per holder and document; `00` for the application's own) |
| Investor Information | `t_Unotp_Kyc_Dtls` (per holder, with NSDL and CKYC), `t_Unotp_Address_Dtls` (per holder and address type: `PER`, `COR`), `t_Unotp_Nominee_Dtls` |
| Bank Details & Payment | `t_Unotp_Payment_Bank_Dtls` (the payment account and instrument), `t_Unotp_Bank_Dtls` (the repayment account) |
| FD Configuration | `t_Unotp_Investment_Dtls` (the deposit, with category, sourcing and employee details) |

Each save is checked against the version the page read (`If-Match`), moves the
version on, and inserts that step's rows afresh with status `PEN`; the header points
each step at the version that is current. Submitting inserts every step once more
with status `APR`, with the rate, interest and maturity locked on `t_Unotp_Investment_Dtls`,
and the application takes no saves after it. Earlier versions stay as the audit
trail. A page's working state (`t_Unotp_Page_State`) is scratch and is overwritten.

## Configuration

Settings are in `src/UnoTP/appsettings.json`; in the environment use a double
underscore (`ConnectionStrings__UnoTP`).

| Setting | Meaning |
|---|---|
| `ConnectionStrings:UnoTP` | The database. Blank runs the mock, which only Development or a demo (`Features:DemoData`) may do. Set it in the environment or a secret store, never in appsettings. |
| `ConnectionStrings:UnoTP_Masters`, `UnoTP_Folios`, `UnoTP_Links`, `UnoTP_Errors` | Where an area lives in a database of its own, as the old portal keeps them: the masters (brokers, staff, IFSC, PIN codes, rate card, config, features, lists), the investor folios, the payment links behind Short URL, and the error log. Blank, the area's tables are in the main database. No query joins across areas. |
| `Backend:Switches:{Identify, Ocr, Verification, PanAadhaarLink, FaceMatch, NameScreening, NameMatch}` | `false` switches the service off: it is not called, the page goes on, and the check is marked as not asked for Operations. Masking, NSDL and decryption have no switch. |
| `Backend:External:{Nsdl, Identify, Masking, Ocr, Verification, PanAadhaarLink, FaceMatch, Decrypt, NameScreening, NameMatch}` | Each outside service's address. Blank in Development, its mock answers; elsewhere each must be set (IDfy covers Masking, PanAadhaarLink and FaceMatch). |
| `Dms:Root` | Where filed copies are kept until DMS is wired in. |
| `Idfy:BaseUrl`, `Idfy:ClientId` | Idfy.Api for the document checks it has an endpoint for, and the name the app calls itself by on its `X-Client-Id` header (`unotp`). |
| `Shortener:BaseUrl`, `Shortener:ClientId` | UrlShortener.Api, which shortens the payment link on submit, and the `X-Client-Id` it is called with. Blank, the mock shortens; on the database the link goes in full. |
| `PaymentLink:Template` | The page the investor pays on, with `{appNo}` for the application's number. Blank, the app sends no link and the backend makes its own. |
| `Apps:eSarathiLogin`, `Apps:eSarathiConsole`, … | The other apps' addresses, for the links out and the session-expired redirect. |
| `Entry:DemoUserId`, `Entry:DemoSysCode` | The user demo mode signs in as. |
| `Portal:Home`, `Portal:Logout` | The portal's dashboard (where "Portal" goes back to, with the encrypted UserId and SysCode) and its logout page. Blank: the console's `/Classic` and the login portal's root. |
| `Backend:TimeoutSeconds`, `Idfy:TimeoutSeconds`, `Shortener:TimeoutSeconds` | How long an outside service may take to answer: 55 s each. Nothing is retried - every IDfy call may be charged. |
| `Logging:Sql:MinLevel` | The least serious entry written to `t_Unotp_Logs` (`Error`): every error and critical error, with the request, the application number and the partner signed in, in the background. Only on the database. |
| `Security:FrameAncestors` | Other sites allowed to show the pages in a frame (space-separated origins). Blank: none but the app itself. |

Every response carries a Content-Security-Policy (scripts, styles, fonts and images
only from the app; no inline script), `X-Content-Type-Options: nosniff`,
`X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` and no `Server` header
(`Infrastructure/SecurityHeaders.cs`). A page that needs something from another
site, or an inline script, is refused by the browser until the policy allows it.

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
with a demo user. Before a deployment reaches real data: set `ConnectionStrings__UnoTP` and the outside services,
take those demo settings out, and set `Apps__eSarathiLogin` so an expired session
returns to the portal.
