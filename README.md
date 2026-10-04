# Uno TP

The fixed-deposit application journey for E-Sarathi partners: search the investor,
upload and check their documents, take the holders' details, the bank accounts and
the deposit, review, submit. ASP.NET Core 10 MVC, server-rendered pages, no SPA.

## Run

```sh
dotnet run --project UnoTP --launch-profile http
```

The app runs only on its database: set `ConnectionStrings__UnoTP` first (see Data
below), or it stops at startup and says so. Nothing stands in for the database or
for an outside service, and there is no demo sign-in: the only way in is from the
portal, at `/Home/Index?UserId=…&SysCode=…` with the values it encrypts. Opened any
other way, the app shows Session Expired. The other
E-Sarathi apps it links to run beside it: the login portal on 5100, the console on
5101 (see `UnoTP/appsettings.Development.json`).

## Build and test

```
dotnet build UnoTP.sln                 # the everyday build
dotnet build UnoTP.sln -p:Strict=true  # the strict build: any warning fails it, unused code included
dotnet test UnoTP.sln                  # the tests: no database, gateway or running app needed
```

Every push runs the strict build and the tests (`.github/workflows/build.yml`). The
rules the strict build holds the code to are in `.editorconfig`; what makes a build
strict is in `Directory.Build.props`. The tests (`UnoTP.Tests/`) check each backend
API client against a stand-in for the network - what it sends, how it reads the
answer, and that an outage is told apart from a refusal - the limit on documents,
and the deposit maths.

## Waiting, and slow connections

The server decides everything and draws every page; the browser only shows at once
what the server already sent (a choice that shows or hides a part, a list filtered
or paged) and carries posts there and back without reloading the page. So nothing
on the screen waits where it need not, and where it must wait it says so:

- A press that goes to an outside service (an upload, a check, Proceed) puts the
  wait up at once, with the words on the button (`data-loader`).
- An upload is several outside services asked in turn. The wait says which one it
  is on - identifying, OCR, the issuer or NSDL, the PAN-Aadhaar link, the face
  match, filing - asked of the server once a second (`UploadProgress`,
  `data-loader-progress`).
- A change that redraws the page (a drop-down, a toggle), a quote and a search as
  it is typed leave the page usable; if one lasts over a second, a line at the
  foot of the screen says the connection is slow (`whenSlow` in `loader.js`).
- A move to another page that lasts over a second puts the wait up by itself.

## Layout

| Folder | What it is |
|---|---|
| `UnoTP/` | The web app: `Controllers/`, `ViewModels/`, `Views/`, `wwwroot/`, `Infrastructure/`, and `Services/` |
| `UnoTP/Services/` | One folder per backend API, each with its settings and its client: `Auth`, `Pan`, `UidMasking`, `Ckyc`, `Idfy`, `NameScreening`, `NameMatch`, `Shortener`. What they share (the gateway address, the switches) is at its top |
| `UnoTP.Data/` | The database logic, in a project of its own: the `Sql*` classes that answer the pages from SQL Server through Dapper. A change to a query is deployed by replacing `UnoTP.Data.dll` alone |
| `UnoTP.Data/Models/` | The interfaces the pages read and the records they pass. Both projects use them |
| `UnoTP.Tests/` | The tests of the code alone: the API clients, the limit on documents, the deposit maths |
| `db/` | The SQL Server scripts: three create scripts (the new tables, the tables the database already has, the masters), and the numbered scripts for the settings and lists |
| `docs/backend-api.md` | What the pages ask of the data layer, interface by interface |
| `docs/BRAND_GUIDELINES.md` | The visual rules every page keeps |

Inside `UnoTP`: `Controllers/` one per page, the application steps sharing
`ApplicationStepController`; `ViewModels/` one per page, `DocumentsViewModel` holding
the document rules, in one file a concern (`DocumentsViewModel.Slots.cs`, `.Upload.cs`, `.PanChecks.cs` ...); `Views/{Page}/`; `Infrastructure/` for routing, features, caching
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
| The deposits list on Investor Identification (Renew FD) | `pages/renew.css` | — |
| Session Expired, Unauthorized, Error (`_StatusLayout`) | `pages/status.css` | — |

No page has a script or style block of its own inside its markup; what a script
needs from the server is on the page as `data-` attributes.

The pages never touch the database: they read and write through the interfaces in
`UnoTP.Data/Models`, which `UnoTP.Data` answers. Everything an application holds is saved
under its number (the `appNo` in every URL), so a page can be reopened from any
browser and every change is audited. The server session holds only the sign-in.

## Pages

| Address | Page |
|---|---|
| `/Dashboard` | Dashboard |
| `/SearchInvestor` | Investor Identification, for a new deposit and a renewal: Proceed opens an application; the investor's deposits are listed, and Renew on a due one opens its renewal |
| `/UploadInvestorDocuments/{appNo}` | Step 1, Upload Documents |
| `/InvestorInformation/{appNo}` | Step 2, Investor Information |
| `/BankDetails/{appNo}` | Step 3, Bank Details & Payment |
| `/FDConfiguration/{appNo}` | Step 4, FD Configuration |
| `/ReviewSummary/{appNo}`, `/ApplicationSubmitted/{appNo}` | Review Summary, Submitted |
| `/RenewalDashboard` | The old Renew FD address: opens Investor Identification. Its posts open and cancel a renewal |
| `/ViewApplication`, `/PayInSlip`, `/ShortUrl`, `/Admin` | The other dashboard tiles |
| `/Home/Index?UserId=…&SysCode=…`, `/Home/Home`, `/Home/LogOut`, `/Home/SessionExpired` | The way in from the portal, back to it, and out |

These are the old app's addresses, so the portal's links and saved links still work. There is no app prefix in any route: the IIS virtual directory (`/WA_FD_UNOTP/`) is the path base and is added to every address automatically; on another host set `PathBase`. Addresses the old app had that name no page here (`/Apps/UnoTp/...`, a step with no application number) redirect to the page that took their place.

## Data

One app: the pages and the data layer run in the same process, behind a WAF or
reverse proxy, with SQL Server in its own zone. `UnoTP.Data` answers the pages'
interfaces from SQL Server through Dapper: the purchase journey end to end, the
dashboard's lists, the lists and rules, and the console. The way in is the
E-Sarathi auth API's: it decrypts what the portal sent, starts
the user's session and says who they are, and gives their menus. Not
answered yet: the deposits a folio holds in the FD system. Until it is wired in,
Renew FD and the source-of-funds rule see only the deposits booked through this app
(an application with its booking date and deposit number on it), and the nominees
and repayment accounts offered from a folio are those on the app's own submitted
applications. The outside services (NSDL, OCR,
identification, verification, the PAN-Aadhaar link, face match: all Idfy.Api's), name
screening, name match, the PAN check, Aadhaar masking and the link shortener are
behind one gateway (`Backend:BaseUrl`), unless a service's own section gives it an
address of its own (`BaseUrl`). Each is a service of its own, with its own
settings section and its own client; none stands in for another. Documents are kept under `Dms:Root` until DMS is wired in, and no SMS
or e-mail gateway is: sends are logged.

The schema is in three scripts, with no rows in them. Each creates a table, with
its indexes, only when it is not there yet, and never touches a table that already
exists. The numbered scripts after them put in the settings and lists. The masters'
rows, the rate card among them, are already in the database and have no script here:

```sh
sqlcmd -d UnoTP -i db/create_new_tables.sql      # new with this app: the application header, settings, lists, console, error log
sqlcmd -d UnoTP -i db/create_existing_tables.sql # already in the database: what an application holds, the rate card, partners, links, slips
sqlcmd -d UnoTP -i db/create_master_tables.sql   # already in the database: folios, brokers, staff, IFSC, PIN codes, the FD system's masters and its source of funds log
sqlcmd -d UnoTP -i db/003_unotp_seed.sql         # config, features and lists: review with the business
sqlcmd -d UnoTP -i db/008_unotp_source_of_funds.sql # the source of funds on FD Configuration: settings, list
sqlcmd -d UnoTP -i db/009_unotp_category_extra_rate.sql # what each category earns over the public rate, for the page's wording
sqlcmd -d UnoTP -i db/011_unotp_any_amount.sql     # any whole-rupee amount, not only multiples of 1,000
sqlcmd -d UnoTP -i db/012_unotp_amount_limit_and_sub_occupations.sql # the 5 crore maximum and its message; sub occupations by occupation
sqlcmd -d UnoTP -i db/013_unotp_link_validity.sql  # the payment link runs 3 days; a new one until the application cancels itself
sqlcmd -d UnoTP -i db/014_unotp_gateway_banks.sql  # the banks the payment gateway takes for online payment
sqlcmd -d UnoTP -i db/015_unotp_no_utility_bill.sql # a utility bill is not taken as a proof of address
sqlcmd -d UnoTP -i db/016_unotp_real_investment_table.sql # the deposit goes to t_FD_BT_Investment_Dtl; the quote's other figures on the header
sqlcmd -d UnoTP -i db/017_unotp_document_sub_types.sql # which document master sub-type each document the app files is: review with the business
sqlcmd -d UnoTP -i db/018_unotp_interest_frequency.sql # the rate card's name for each payout, written to f_Int_Freq: review with the business
sqlcmd -d UnoTP -i db/019_unotp_rate_card_names.sql # the rate card's name for each category and each payout's scheme: review with the business
sqlcmd -d UnoTP -i db/020_unotp_data_source.sql    # where a folio's KYC came from, for f_Data_Source: on the folio and on the application
sqlcmd -d UnoTP -i db/021_unotp_master_lists.sql   # marital status, relations and source of funds come from the FD system's masters: two settings they need
sqlcmd -d UnoTP -i db/022_unotp_occupation_master.sql # occupations come from the FD system's occupation master: which types are left out
sqlcmd -d UnoTP -i db/023_unotp_renewal_notes.sql  # Renew FD's two notes on changes to the depositors, as one
export ConnectionStrings__UnoTP='Server=...;Database=UnoTP;...'   # never in a committed file
dotnet run --project UnoTP --launch-profile http
```

Every script is safe to run again. No table has a foreign key or a CHECK
constraint: each row is written inside the transaction that locks its application, and the app keeps the rules itself. Every table has `f_Active`; only
active rows are read. The masters are loaded from their sources of record
before go-live (the FD system, the broker and staff masters, the RBI's IFSC list,
India Post, the rate card), as is `cmsLocations` in `t_Unotp_Ref_List`. A session
lasts `AuthApi:SessionHours` from entry; after that the partner comes in from the
portal again.

The app will not start without `ConnectionStrings:UnoTP`, nor while an outside
service it calls has no address: neither a `BaseUrl` of its own nor the gateway's
(`Backend:BaseUrl`). The error names the services left without one.

**How an application is kept.** `t_Unotp_Application_Mst` holds one row per application:
its number, its partner, who it was opened for, and its version. Everything entered
on it is rows in the FD system's own tables, exactly as the database has them:

| Step | Tables |
|---|---|
| Upload Documents | `t_Unotp_Upload_State` (the step as JSON), `t_FD_BT_KYC_document` (one row per holder and document, coded as the FD system's document master codes it; the application's own go under `01`) |
| Investor Information | `t_FD_BT_Kyc_Data_Dtl` (per holder), `t_FD_BT_Address_Dtl` (per holder and address type: `PER`, which also carries the mobile and e-mail, and `MAIL`), `t_FD_BT_Nominee_Dtl`. The gender is kept as the name prefix; the FATCA answers stay with the page's typed fields |
| Bank Details & Payment | `t_FD_BT_Payment_Dtl` (the payment account and instrument), `t_FD_BT_Investor_Bank_Dtl` (the repayment account) |
| FD Configuration | `t_FD_BT_Investment_Dtl` (the deposit, with category, sourcing and employee details), and `t_FD_CMN_AML_Source_Of_Funds_Log` where a source of funds is given |

The FD system's tables have no version column. Each save is checked against the
version the page read (`If-Match`), moves the version on, takes that step's rows
out of use (`f_Active = 0`) and inserts them afresh with status `PEN`: a step's
current rows are the application's active ones. Submitting writes every step once
more with status `APR`, with the rate locked on `t_FD_BT_Investment_Dtl` and the
interest and maturity on `t_Unotp_Application_Mst`, and the application takes no
saves after it. Nothing is deleted, so the earlier rows stay as the audit trail.
Every row carries `f_Source = 'UNO_TP'`, the user's `Agency_Usr_Clustered_ID`, and
the session and address it was written from. A page's working state
(`t_Unotp_Page_State`) is scratch and is overwritten.

## Configuration

Settings are in `UnoTP/appsettings.json`; in the environment use a double
underscore (`ConnectionStrings__UnoTP`).

| Setting | Meaning |
|---|---|
| `ConnectionStrings:UnoTP` | The database. Blank, the app does not start. Set it in the environment or a secret store, never in appsettings. |
| `Backend:BaseUrl` | The API gateway the backend APIs are behind. `appsettings.json` carries it as `https://<gateway-host>/`, the host left as a placeholder; the real host is set in the environment (`Backend__BaseUrl`) and never in a committed file. Each API's own settings hold its `BasePath` under the gateway, and the path of each call under that (no leading slash). |
| `AuthApi:BaseUrl`, `PanApi:BaseUrl`, `UidMasking:BaseUrl`, `Ckyc:BaseUrl`, `Idfy:BaseUrl`, `NameScreening:BaseUrl`, `NameMatch:BaseUrl`, `Shortener:BaseUrl` | A service's own address, for one that is not behind the gateway: it is then called at `{BaseUrl}/{BasePath}/` and the gateway is not used for it. Blank (as committed), the service is called at `Backend:BaseUrl`. Set in the environment (`Ckyc__BaseUrl` and the like), never in a committed file. With every service given its own address, `Backend:BaseUrl` need not be set. |
| `Backend:ClientId` | The name the app calls itself by on every API's `X-Client-Id` header (`unotp`). |
| `ConnectionStrings:UnoTP_Masters` | The masters database, the second of the app's two connections. Blank, the masters are looked for in the main database. |
| (in code) | Which of the two connections a table is read on, and the SQL that reads each master, are in the code, not in settings: `UnoTP.Data/MasterQueries.cs` has one query per master. A table in another database on the same server is written there by its full name, `OtherDb.dbo.Table`. |
| `Backend:Switches:{Identify, Ocr, Verification, PanAadhaarLink, FaceMatch, NameMatch}` | `false` switches the check off: it is not called, the page goes on, and the check is marked as not asked for Operations. Masking and the PAN check have no switch; name screening has its own (`NameScreening:ApiCall`). |
| `AuthApi:DecryptPath`, `AuthApi:SessionPath`, `AuthApi:MenuPath`, `AuthApi:SessionHours` | The way in: the path of each call under the gateway (`{userId}` and `{sysCode}` in the menu path are filled in), and how many hours a session lasts. |
| `PanApi:VerifyPath` | Where a holder's PAN is checked with NSDL. Everything else the request carries - the app code, the sourcing type and sub type, who is asking - comes from the signed-in partner and the application. |
| `UidMasking:MaskPath`, `UidMasking:MaskLength`, `UidMasking:OutputJpegQuality`, `UidMasking:CheckDocumentType` | Where an Aadhaar copy is masked, how many digits are masked, how good the masked JPEG comes back, and whether the API checks the copy is an Aadhaar first. |
| `AuthApi:SessionHours`, `AuthApi:TimeoutSeconds` | Hours a session lasts after entry (8), and seconds the API is given to answer (55). |
| `NameScreening:BasePath`, `NameScreening:ScreenPath` | The name screening API (NSA): whether a holder may invest online. |
| `NameScreening:ApiKey` | The key it is called with, on its `apikey` header. Set in the environment (`NameScreening__ApiKey`), never in a committed file. |
| `NameScreening:ApiCall` | `0` switches screening off: the API is not called, every holder goes on, and the KYC row says the check was skipped. Anything else is sent on as `Api_call`. |
| `NameScreening:{BlackListCheck, CustomerDataBaseCheck, RejectedListCheck, EmployeeDataBaseCheck}` | Which lists a holder is screened against, as the API takes them. |
| `NameMatch:BasePath`, `NameMatch:MatchPath` | The name match API: whether two names are the same person's. |
| `Dms:Root` | Where filed copies are kept until DMS is wired in. |
| `Ckyc:BasePath`, `Ckyc:SearchPath`, `Ckyc:IncludeImages` | The CKYC (CERSAI) search: whether a record is held for a PAN and date of birth. Asked when the partner chooses Fetch from CKYC; only an investor it holds a record for takes that route. |
| `Idfy:BasePath` | Idfy.Api, which alone answers document identification, OCR, verification with the issuer, the PAN-Aadhaar link and face match. A cheque is read with IDfy's `ind_cheque`; nobody confirms its account with the bank, so it is carried to Bank Details as read, for the partner to check. Where IDfy has no endpoint - an Aadhaar's issuer, a bank account - nothing is asked, and the page says so; an Aadhaar's address is taken as OCR read it. A utility bill is not taken as a proof of address. |
| `Idfy:ValidateDocumentPath`, `Idfy:ExtractPanPath`, `Idfy:ExtractAadhaarPath`, `Idfy:ExtractDrivingLicencePath`, `Idfy:ExtractPassportPath`, `Idfy:ExtractVoterIdPath`, `Idfy:ExtractChequePath`, `Idfy:VerifyDrivingLicencePath`, `Idfy:VerifyPassportPath`, `Idfy:VerifyVoterIdPath`, `Idfy:VerifyPanAadhaarLinkPath`, `Idfy:CompareFacesPath` | The path of each Idfy.Api call under the gateway. |
| `Shortener:ShortenPath` | Where the payment link is shortened on submit. No path, and the link goes in full. |
| `PaymentLink:Template` | The page the investor pays on, with `{appNo}` for the application's number. Blank, the app sends no link and the backend makes its own. |
| `Apps:eSarathiLogin`, `Apps:eSarathiConsole`, `Apps:UnoTP` | The other apps' addresses, for the links out and the session-expired redirect. |
| `Portal:Home`, `Portal:Logout` | The portal's dashboard (where "Portal" goes back to, with the encrypted UserId and SysCode) and its logout page. Blank: the console's `/Classic` and the login portal's root. |
| `RateLimits:EntryPerMinute` | How often one address may come in from the portal: 60 a minute. Over it, the Too Many Requests page until the minute is out. 0 takes the limit off. |
| `RateLimits:PerDocumentPerMinute` | Tries a session has, on one application, at one holder's document of one type - their PAN copy, their proof of address, the cheque - and at each check typed by hand (the name for NSDL, the Aadhaar number, CKYC, name screening): 3 a minute, counted by holder and document type within the session and the application. The next try reaches no outside service, uses no attempt, and the page says so in a popup. 0 takes the limit off. |
| `RateLimits:RefusedWaitMinutes` | How long a document waits once its copies have been refused `maxAttempts` times in a row (3, from `t_Unotp_App_Config`): 15 minutes from the last refusal. Until then its box says how long is left and takes no copy; after it the count starts again. Nothing goes to Operations. |
| `Backend:TimeoutSeconds` | How long any backend API may take to answer: 55 s. Nothing is retried - every call may be charged. |
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
| `NewFd`, `PisGeneration`, `ViewApplication`, `ShortUrl`, `RenewFd` | on | A dashboard tile and its pages. Off, the tile is greyed and says why. `NewFd` and `RenewFd` share one tile and one start page (Investor Identification): the tile is greyed only when both are off, and the page disables what is off - Proceed for `NewFd`, Renew for `RenewFd` - saying why. |
| `ApplicationStatus`, `Admin` | off | The same, for tiles not built yet. |
| `DocIdentification` | on | A proof's type is what the copy is identified as on upload; off, it is chosen from a drop-down first. |
| `CommProofUpload` | off | A different communication address is proved with an upload; off, it is typed on Investor Information. |

## Deploy

`render.yaml` builds the `Dockerfile` and checks `/health`, which asks nothing of the
backend. The deployment starts only with `ConnectionStrings__UnoTP` and each outside
service's address set in its environment. Set `Apps__eSarathiLogin` so an expired
session returns to the portal.
