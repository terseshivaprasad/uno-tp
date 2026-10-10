# Uno TP data layer

This document lists everything the Uno TP pages ask of their data. The pages read
and write only through the interfaces in `UnoTP.Data/Models` (`IApplicationApi`,
`IReferenceApi` and the rest). `UnoTP.Data` answers them from SQL Server, in
the same process. The tables are in `db/`. The outside services for the
document checks are reached over HTTP, one client each under `UnoTP/Services/`.

Each call is written below as a route, `GET applications/{appNo}`, with the
interface method it stands for named in the code beside it: the routes are how the
contract was first agreed, and keep each call's inputs and answers in one line.
None is served over HTTP now.

## Pages

Every page is under `/unotp`, in lowercase. The application a step works on is
in its address, never in the session.

| Address | Page |
|---|---|
| `/unotp` | Dashboard |
| `/unotp/new` | Investor Identification: search, drafts, and a new application |
| `/unotp/applications` | View Application: the partner's applications |
| `/unotp/applications/{appNo}/documents` | Upload Documents |
| `/unotp/applications/{appNo}/investor` | Investor Information |
| `/unotp/applications/{appNo}/payment` | Bank Details & Payment |
| `/unotp/applications/{appNo}/deposit` | FD Configuration |
| `/unotp/applications/{appNo}/review` | Review Summary |
| `/unotp/applications/{appNo}/submitted` | Application Submitted |
| `/unotp/pay-in-slips` | PIS Generation - Axis |
| `/unotp/links` | Short URL |
| `/unotp/renew`, `/unotp/renew/{deposit}`, `…/done` | Renew FD: the deposits on a folio, a renewal chosen and quoted, and sent for the investor to accept |
| `/unotp/admin` | Console Admin |
| `/unotp/entry`, `/unotp/logout`, `/unotp/session-expired`, `/unotp/unauthorized`, `/unotp/error` | Entry and status pages |

Each page is one folder (`UnoTP/Pages/{Page}/`: the Razor page `Index.cshtml` and its
page model `Index.cshtml.cs`) and one view model (`ViewModels/{Page}ViewModel.cs`),
named after the page: `NewApplication`, `Documents`, `Investor`, `Payment`,
`Deposit`, `Review`, `Submitted`, `Applications`, `PayInSlips`, `Links`, `Admin`,
`Dashboard`, `Home` (the way in and the status pages). The plumbing (session, feature gate, errors, cache, app
addresses) is in `Infrastructure/`.

A form posts to its page's address and a verb (`.../documents/upload`,
`.../review/submit`). The old app's addresses that name no page here
(`/Dashboard/Index`, a step's address with no application number) redirect to the
page that took their place, with their query.

## Configuration

| Setting | Meaning |
|---|---|
| `ConnectionStrings:UnoTP` | The database. If blank, the app does not start. |
| `Backend:BaseUrl` | The one gateway every backend API is behind. Blank or still the placeholder, the app does not start. |
| `Backend:TimeoutSeconds` | Seconds an API is given to answer. Default 55. |
| `AuthApi:DecryptPath`, `AuthApi:SessionPath`, `AuthApi:MenuPath` | The way in's three calls, by their path under the gateway. |
| `PanApi:VerifyPath`, `UidMasking:MaskPath` | The PAN check and Aadhaar masking, by their path under the gateway. |
| `NameScreening:*`, `NameMatch:*` | The name screening and name match APIs, each with its base path and the path of its call. |
| `Idfy:*` | Idfy.Api's base path and the path of each call. It alone answers the checks listed below. |
| `Idfy:TimeoutSeconds` | Default 75. The Idfy.Api guide asks for at least 70. |
| `Backend:ReferenceCacheMinutes` | Minutes the lists and rules (`GET reference`, `GET config`) are kept for. Default 10; 0 asks every time. |
| `Features:CommProofUpload` | Default false (off for this release). On, a holder whose post goes to an address other than the permanent one uploads a proof of it on Upload Documents. Off, that box is hidden and the address is typed on Investor Information (`communication` in `ApplicationDetails`). |

In the environment, use a double underscore, for example `ConnectionStrings__UnoTP`.

## What the app keeps, and for how long

Answers that change seldom are kept in the app's memory (`UnoTP/Infrastructure/CachedBackend.cs`
and `CachedListsAndSettings.cs`), so a page load does not ask the backend for them each time.
Nothing that was not found is kept, and no failure is: the next request asks again.

| Answer | Kept for | Notes |
|---|---|---|
| `GET reference`, `GET config` (every list and rule) | `Backend:ReferenceCacheMinutes`, default 10 | The same for every partner |
| `GET sourcing/brokers/{code}`, `GET sourcing/staff/{code}` | 5 minutes | Only a code that was found. The registers are never read whole |
| `GET ifsc/{code}` | 10 minutes | Only a branch that was found; a new IFSC is found at once |
| `GET pincodes/{pin}` | 1 day | Only a PIN code that was found |
| `GET ifsc?q=` (the bank search) | 10 minutes, and 1 minute in the browser | Only a search that found something; a new bank is found by the next search |
| `GET sourcing/brokers?q=`, `GET sourcing/staff?q=` | 5 minutes, and 1 minute in the browser | The same: only a search that found something. The staff are kept by the sourcing mode's staff rule |
| `GET deposits/rates` | 1 minute, never past the day | Keyed by category, gender, application type and start date - nothing of the investor's |
| `POST deposits/quote` | 1 minute, never past the day | Keyed by amount, tenure, payout and the same card key - nothing of the investor's |
| `GET console/schedule` | 30 seconds | Dropped the moment the app adds, ends or removes a window or notice |
| `GET me` | 5 minutes | Per user and per session; a new session asks again |

A backend that changes one of these and needs it seen sooner should say so; the
times are set in `CachedBackend.cs`. The cache holds at most 50,000 entries.

## Common to every call

- **Partner:** every call is made for the user the portal sent in and the session
  started for them (`IPartner`, from the server session). Only that user's
  applications are returned; anyone else's is not found. A session lasts
  `AuthApi:SessionHours` from entry: once it has ended, a page shows Session Expired.
- **JSON:** camelCase in both directions.
- **Not found:** `404` means not found wherever a route below says "or 404".
  Any other failure status is treated as an error.

## Entry

The portal opens the app at `/Home/Index?UserId=...&SysCode=...` (or at `/`, with
the same query), both values encrypted. The app asks the E-Sarathi auth API
to decrypt each, to start a session, and for the user's menus;
it keeps who the user is and their menus in the server session, and redirects to
the Dashboard, so neither value stays in the address. Every answer of the auth API
is `{ success, message, data }`. Every other page needs that session; without one, or once it has
ended, the partner sees Session Expired. A refused entry shows Unauthorized.

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `cipher/decrypt` (auth API) | `{ Text }` | `data`: the plain text. A `4xx`, or `success` false, means it cannot be decrypted. |
| POST | `NameMatch:MatchPath` | `{ SourceName, TargetName }`: the name read off the document, and the holder's name it is compared with | `{ status, error_code, error_message }`. `SUCCESS` is a match. `FAIL` is the names not matching: it comes with an `error_code` and the message "Name match criteria does not meet". Any other status is the service not having compared them: an outage, said with its `error_message`, and never a mismatch. Asked with the name read off a proof of address and the holder's name as the PAN holds it (the PAN-POA name match), and with an Aadhaar's name against the PAN's before the Aadhaar is taken. Switched off, the proof is filed with the match marked not asked. |
| POST | `NameScreening:ScreenPath` | The API's own fields: `name1`, `dob` (`dd-MM-yyyy`), `country1` (`IN`), `appl_No`, `holderType`, `mobileNo`, `source` and `sourceSubType` (`UNO_TP`), `sourceType` (`FD`), `sessionId`, `createdBy`, `createdIP`, `Api_call` and the four list checks from `NameScreening:*`; the rest go empty. Header `apikey`. | `{ status, nameScreeingStatus, nameScreeningCode, data }`: a holder is allowed only when `status` is `SUCCESS` and `nameScreeingStatus` is `ALLOWED`. Asked of every holder on Investor Information before it goes on; a holder answered once, under the same name, is not asked again. A holder not allowed invests offline, at a branch. The answer is kept on the holder's KYC row, with `data.uniqueRequestId` as its reference. |
| POST | `auth/sessions` (auth API) | `{ userId, sysCode, serverIP, domainName, ipAddress, macAddress, browserType, browserVersion, browserMajor, browserMinor, userAgent }` | `data`: the user and their session (`AgencyUserModel`), kept whole in the session. The pages read `entity_Name` (name), `entity_Id` (code), `agency_Type` and `busi_Broker_Cd` (broker code); `pk_Session_ID` is the session id sent with every backend call. A `4xx`, or `success` false, refuses the entry. |
| GET | `app-menus/{userId}/{sysCode}` (auth API) | | `data`: the user's menus, each with `PageName` and `SubModName`. An empty menu refuses entry. The menu switches no feature on or off: that is the `Features` section of appsettings alone. |

## Lists, rules and the partner

The pages hold no data of their own: every list they offer, every limit they
check, and who is signed in come from these routes. The lists and the rules are
kept for `Backend:ReferenceCacheMinutes` (default 10) before they are asked for
again; a failed answer is not kept.

| Method | Route | Body | Returns |
|---|---|---|---|
| GET | `reference` | | `ReferenceData`: every list the pages offer (see below) |
| GET | `config` | | `AppConfig`: the limits and rules the pages check |
| GET | `me` | | `PartnerProfile`: `name`, `code`, `agencyType`, `brokerCode` of the signed-in partner. The `sourcingAgency` in `config` chooses the sourcing mode and broker code; any other type sources as a broker under `brokerCode`. Nobody chooses the deposit category: it is set from the holder's date of birth and gender, within the categories the sourcing mode allows (`PUBLIC` or `GENERAL-WOMEN` under the senior age, `SR CITIZEN` or `SR CITIZEN-WOMEN` from it; an employee mode's by gender alone). While `Features:DemoData` is on, `?agency=2001&broker=BR10874` shows the app as another kind of partner for the session. |
| GET | `deposits/rates` | `{ category, gender, applicationType, startsOn? }` - `gender` M or F (M when not known), `applicationType` PURCHASE or RENEW | The rate card for that key, one `RateOption` per line: `tenureMonths`, `scheme` (CUMULATIVE or NON-CUMULATIVE), `payout` (the frequency), `rate`, `minAmount`, `maxAmount`, `asOn`. FD Configuration draws its tenures and payouts from it at the amount entered, or at `config.quoteAmount` (₹50,000) before one is; a payout under its `minAmount` is shown shut. There are two cards of the same structure, and `branchUser` on the request says which is read: a branch user's (`t_FD_BOTC_SCHEME`, with the employee and special schemes and their extra tenures) or a partner's (`FD_SCHEME`). A tenure shows only where the user's card has a row for it. What a women's or a senior citizen's category earns over the public rate is worked out from the two categories' rows for the tenure and payout chosen, so a benefit that starts at 36 months shows only from there. |
| POST | `deposits/on-rate-card` | `SchemeCheck` (`category`, `mode`, `scheme`, `interestFreq`, `tenureMonths`, `rate`, `amount`) | `true` when the rate card holds a row in effect today for exactly that category, mode (`AF` or `R`), scheme, interest frequency, tenure and rate, with the amount within the row's minimum and maximum. FD Configuration asks it on Proceed, straight off the card, and does not go on without it. A deposit quoted off the default category's rates, for a category with no rows of its own, is therefore refused. |
| POST | `deposits/quote` | `{ amount, tenureMonths, payout, card: { category, gender, applicationType, startsOn? } }` | `DepositQuote`: `rate`, `interestEach`, `maturityAmount`, `maturesOn`, `rateAsOn`. A cumulative deposit compounds `compoundingPerYear` times a year, the months after the last whole period at simple interest. |
| GET | `ifsc/{code}` | | `BankBranch`: `ifsc`, `bank`, `branch`, `micr`, or 404 |
| GET | `pincodes/{pin}` | | `PinPlace`: `pinCode`, `district`, `state` for a 6-digit PIN code, or 404 — shown beside a communication address typed on Investor Information, and saved with it. The page asks once all six digits are typed; nothing is suggested while typing. |
| GET | `ifsc?q={text}` | | `BankBranch[]`: the bank search on Bank Details &amp; Payment, by the rule the FD system's own bank search goes by. A branch is found when every word typed is in its search key (MICR, IFSC, branch and bank name), in any order; the branches found are listed by MICR code, at most 15, each shown as `(MICR >> IFSC >> branch >> bank)`. The bank master is big: nothing is searched until 3 characters are typed, and it is never sent to the page. `MasterQueries.BankBranches` carries both the IFSC of one branch (`@Ifsc`) and the words typed (`@Words`); the app gives one and leaves the other empty. |
| GET | `cms-locations?q={text}` | | `CmsLocation[]` (`code`, `name`, `label`): the Axis CMS branches whose label - name, location and PIN code - holds the text, by state and district, at most 20. It reads the FD system's Axis CMS branch master (`t_FD_Axis_CMS_Branch_Mst`) the way `usp_FD_BT_Get_User_AxisCMSBranch` does. The search shows each branch by its label; the branch picked is kept by its code (`f_Sol_Id`, saved to `f_CMS_Loc_CD`) and its name (`f_Branch_Name`, saved to `f_CMS_Loc_Desc`), and the code is checked against the master when the step proceeds. A name typed without a pick has no code and is refused. |

- **`ReferenceData`:** `applicationTypes` and `renewInstructions` and
  `deliveryTypes` as `{ code, name }`; `categories` as `{ code, name, employee,
  women, senior }`; `paymentModes` as `{ name, document }` (`document` is the
  instrument a copy is filed for, or null); `sourcingModes` as `{ code, name,
  codeLabel, nameLabel, house, search, register, sub, categories, staff }`;
  `proofsOfAddress` as `{ type, issuer, hasPhoto }` (an Aadhaar, a passport, a driving
  licence or a voter ID; a utility bill is not taken. Only a proof with `hasPhoto` -
  an officially valid document - proves the permanent address); `payouts` as `{ code, name,
  perYear, each }` (`perYear` 0 is cumulative); `tenures` in months;
  `requiredDocuments` as `{ title, items, notes }`; and plain lists for
  `employeeHolders`, `employeeRelations`, `employeeProofs`, `incomeBands`,
  `occupations`, `subOccupations`, `maritalStatuses`, `genders`, `nameTypes`,
  `nomineeRelations`, `identificationNotes`, `dashboardNotes`,
  `noticeKinds` (for Console Admin) and `declarations` (signed on Review Summary before submitting).
  Codes are what the app posts and saves.
- **`AppConfig`:** `sourcingAgency` (the agency type that chooses how an
  application is sourced), `minAge`, `seniorAge`, `maxJointHolders`,
  `maxAttempts`, `minAmount`, `maxAmount`, `amountStep` (rupees),
  `cancellationDays`, `draftDays`, and `linkValidityHours` by purpose
  (`payment`, `acceptance`). The backend checks the same rules again on save.
- **Quote:** the rate is the card rate for the tenure and category, locked
  when the application is submitted. A cumulative deposit compounds
  `compoundingPerYear` times a year; any other pays simple interest per period
  (`DepositMaths`).

## Investors

| Method | Route | Body | Returns |
|---|---|---|---|
| GET | `investors/folio-deposits?pan={pan}` or `?folio={folio}` | | `FolioDeposit[]` (`folio`, `pan`, `dob`): the deposits that are not cancelled, with their first holder, on a folio still in use. The folio check (`FolioCheck`) reads them in the FD system's order, but for a PAN whose fourth letter is not `P`, which is refused first, with a folio or without: no deposit, a new investor, unless the folio master (`MasterQueries.FirstHolders`) holds the PAN as a first holder, which is refused so that nobody with a folio goes on as new; no date of birth on record, refused; another date of birth than the one typed, refused; more than one folio, refused. A search by folio number is checked the same way but for the date of birth typed: the folio must hold a date of birth, and its PAN must not be on another folio too. |
| GET | `investors/folios/{folio}` | | `FolioRecord`, or 404 |
| GET | `investors/folios/{folio}/on-record?pan={pan}&dob={dd-MM-yyyy}` | | `FolioRecord` as its data source has it, read at Investor Identification and when a renewal opens. `source` is where the holder's latest KYC is kept: `ORA` (the common tables), `BT` (an application submitted through this app) or `FHLD` (the folio master), the newest row among them for that folio, PAN and date of birth; it is saved as the holder's `f_Data_Source`. `address` is that source's latest permanent address for the same folio, PAN and date of birth (the folio master's where it holds none). `docs` (`pan`, `photo`, `poa`) says what a holder on a folio need not upload again. `pan` is always true: a holder on a folio is never asked for the PAN copy. `photo` is true when the source holds a photograph row for that holder. `poa` is true when it holds a proof of address row for them, verified or not, and the address on record has its first line and its PIN code. A document row counts only when it has both a file name and a file path. A folio can have more than one holder, so an address or a document row is theirs only when the KYC row of its application and holder type carries their PAN and date of birth. Whatever is not on record is asked for on Upload Documents. 404 for a folio the master does not hold |
| GET | `investors/folios/{folio}/kyc?source={source}` | | `HolderDetails`: the KYC details the source holds (`ORA` or `BT`; the folio master holds none), with its mailing address as `communication` where it holds one. Filled into Investor Information the first time it opens and validated like anything typed; the mailing address only where the communication address is typed there |

`FolioRecord`: `pan`, `dob` (`dd-MM-yyyy`, or `""` when none is held),
`folio`, `name`, `gender`, `address` (`""` when none is held),
`docs { pan, photo, poa }` (booleans), `note`.

## Applications

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `applications` | `{ holder }` | `Application`. Its number comes from the FD system's `USP_FD_BTP_GetApplicationNo`: a purchase with `@BusType` `B` and `@DSource` `C` for a partner or `B` for a branch user; a renewal with `@DSource` `R` and `@BusType` `C` for a partner or `B` for a branch user. A branch user is one whose agency type is the `sourcingAgency`. |
| GET | `applications/{appNo}` | | `Application`, or 404. Drafts included. |
| PUT | `applications/{appNo}/upload` | `UploadState`, with header `If-Match: "{version}"` | `{ version }`, or `409`/`412` if the application changed since that version was read. Nothing is saved in that case. |
| PUT | `applications/{appNo}/details` | `ApplicationDetails`, with `If-Match` | `{ version }`, or `409`/`412` |
| PUT | `applications/{appNo}/pages/{page}` | `{ state }` | `204`, or `404` when the application is not the partner's. A wizard page's working state, kept in `pages` on the application as it stands: it moves no version and meets no conflict. Opaque to the backend. |
| PUT | `applications/{appNo}/payment` | `PaymentDetails`, with `If-Match` | `{ version }`, or `409`/`412` |
| PUT | `applications/{appNo}/deposit` | `DepositDetails`, with `If-Match` | `{ version }`, or `409`/`412` |
| POST | `applications/{appNo}/submit` | `{}`, with `If-Match` | `Application` as submitted, or `409`/`412`. The application is saved first, and on **Try later** that is all: no link is made until one is asked for on Application Submitted (or Short URL). On **Submit & send link** its payment link is then made: the page the investor pays on, built from `PaymentLink:Template`, and its short form from UrlShortener.Api (the long one alone when the shortener does not answer). The link is put on record in `t_Unotp_Payment_Link` for a purchase and in `t_Unotp_RePayment_Link` for a renewal, and goes to the investor by SMS and e-mail both. It stays open for `linkValidityHours.payment` (72 hours: 3 days); the application itself stands for `cancellationDays` (14 days). |
| POST | `applications/{appNo}/resend-link` | | `Submission`, or `404`/`409` when there is no link to resend |
| GET | `applications/drafts` | | `DraftSummary[]`: the partner's own applications, opened and not yet submitted, the one touched last first, with identifiers masked |
| GET | `applications` | | `ApplicationRecord[]`: the partner's applications, including older ones, each with its `scheme` name and `milestones` (`{ step, at }`, in order) for the View Application timeline |

- **`holder`:** `pan`, `dob`, `name`, `folio`, `panFiled`, `address`,
  `onRecord { pan, photo, poa }` or null, and `gender` (the folio's, or `""`).
- **`Application`:** `appNo`, `holder`, `version`, `upload` (an `UploadState`,
  or null until first saved), `prior` (attempts on record from before the
  upload step, newest first, as `LogEntry[]`), and `pages` (each wizard page's
  working state by page name, as the page last saved it).
- **`ApplicationDetails`:** Investor Information: `holders` (one per holder
  type: `holder` 01/02/03, `gender`, `nameType`, `parentName`, `annualIncome`,
  `occupation`, `subOccupation`, `maritalStatus`, `mobile`, `email`,
  `fatcaTaxResident`, `fatcaPermanentResident`, `pep`, `pepRelated`, and
  `communication` — the address post goes to, typed, while
  `Features:CommProofUpload` is off: `line1`-`3`, `city`, `pinCode`, and the
  `district` and `state` from `GET pincodes/{pin}`; null when post goes to the
  permanent address) and
  `nominee` (`name`, `dob`, `relation`, `guardianName`, `guardianLine1`-`3`,
  `guardianPinCode`, `guardianCity`) or null.
- **`PaymentDetails`:** `payment` and `repayment` as `{ ifsc, accountNumber }`,
  `repaymentSameAsPayment`, and `cheque` as `{ number, date, cmsLocation }` or
  null.
- **`DepositDetails`:** `amount`, `tenureMonths`, `payout`, `autoRenewal`,
  `renewInstruction`, `noTds`, `deliveryType`, as the reference lists code them.
- **`Submission`:** `at`, `status`, `linkSentTo` (the mobile, masked), `linkValidUntil`,
  `resendsLeft`, `linkEmailedTo` (the e-mail, masked; empty when there is none),
  `shortUrl` (the short payment link the application was submitted with, or empty; the page does not show it). `Application` carries it as `submitted`, with `details`,
  `payment` and `deposit`.
- **`UploadState`:** the upload step as a whole. That covers the choices made
  (`appType`, `poaType`, `payMode`, `sourcing`, `sourceCode`, `subBroker`,
  `category`, the `emp*` fields, `formNo`, `typedFormNo`, `ckyc`, `savedAt`,
  and `gender` as read off the proof of address for a holder the folio gives none for),
  plus `docs`, `attempts`, `reads` and `log`. See `Applications.cs` for every
  field. It never contains a file or an Aadhaar number.
- **Joint holders:** `joint` maps a holder type (`02` the second holder, `03`
  the third) to `{ holder, poaType }`, added on Investor Information. Their
  documents sit in `docs`, `attempts` and `reads` under keys that begin with it
  (`h02-pan`, `h03-poa`). A log entry carries `holder` (`02`, `03`) when it is a
  joint holder's, and `removed` once that holder is taken off. CKYC (`ckyc`)
  is fetched for the investor only, so it never takes a joint holder's
  photograph or proof of address off.

**Deposit categories** (the codes are the rate card's own): `PUBLIC`, `GENERAL-WOMEN`, `SR CITIZEN` and `SR CITIZEN-WOMEN`
(60 or over), plus `EMPLOYEE` and `EMPLOYEE-WOMEN`, which only a 1033
partner can book, under MFL-EX.

## Renewals

Renew FD, the old RenewalDashboard. The investor is searched by PAN and date of
birth or by folio, their deposits listed with where each stands, and a renewal
entered for one that is due. A renewal is an application like a new deposit's:
the backend opens it with `renewal` set and the deposit's joint holders
(`upload.joint`), repayment account (`payment.repayment`) and maturity amount
(`deposit`, read-only on the page) already on it; the steps, the submit and the
investor's acceptance are the same as for a new deposit. Upload Documents asks no
payment mode or instrument for a renewal, and Bank Details asks only the
repayment account.

| Method | Route | Body | Answer |
|---|---|---|---|
| GET | `deposits?pan={pan}&dob={dd-MM-yyyy}` | | `HeldDeposit[]`: the deposits held by that investor, soonest to mature first; 404 for none on record |
| GET | `folios/{folio}/deposits` | | `HeldDeposit[]`: the deposits on the folio; empty for a folio with none; 404 for no such folio |
| GET | `deposits/{number}` | | `HeldDeposit`, or 404 |
| POST | `renewals` | `{ depositNumber }` | `Application`: the renewal opened, with `renewal` set and what comes over from the deposit on it; 409 when the deposit is not due |

- **`HeldDeposit`:** `number`, `folio`, `investor`, `category` (a category code), `amount`, `rate`, `tenureMonths`, `payout` (a payout code), `maturesOn`, `maturityAmount`, `status`, `renewable`, `why` it is not, `jointHolders` (`{ pan, dob, name, folio }`, in order), `repayment` (`{ ifsc, accountNumber }` or null) and `autoRenewal`.
- **`status`:** `running` (too early), `due` (inside the window), `late` (too near maturity: Operations'), `matured`, `renewed`. Only `due` is `renewable`.
- **The window**, in `config`: from `renewFromDays` (61) to `renewUntilDays` (7) days before maturity, or `renewUntilDaysAutoRenewal` (10) for a deposit tagged for auto renewal. A renewal entered in advance takes the rate prevailing on the maturity date.
- **`Application.renewal`:** `depositNumber`, `amount` (the maturity amount the new deposit is opened for), `maturesOn`, `rate`, `tenureMonths`, `payout`; null for a new deposit.
- `reference` carries `renewalNotes`: the lines the search page shows under the search.

## Documents (DMS)

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `applications/{appNo}/documents` | multipart `file`, with the holder's `folio`, `holderType` and the `document` | The name the copy is kept under. Files the copy as a new file. |
| POST | `applications/{appNo}/documents/{holder}/{slot}/refused` | multipart `file` | `{ ref, keptUntil }`. Keeps a refused copy aside for analysis, off the application. |
| GET | `applications/{appNo}/documents/{fileName}` | | The copy, or 404 |

Every copy is a file of its own, in its application's folder under `Dms:Root`:

| Holder | File name |
|---|---|
| With no folio | `{ApplNo}_{HolderType}_{DocSubType}_{yyyyMMddHHmmssfff}.ext` |
| On a folio | `{Folio}_{ApplNo}_{HolderType}_{DocSubType}_{yyyyMMddHHmmssfff}.ext` |

- **`HolderType`:** `01` the investor, `02` and `03` the joint holders. The
  application's own documents (the form, the cheque, an employee proof, the Form
  121) go under `01` with the investor's folio, as their rows do.
- **`DocSubType`:** the document's sub-type code in the document master (`PAN`,
  `Photograph`, `PASSPORT_A`, `PAYMENT_CHEQUE`). Some codes hold a `_` themselves.
- **The time** is when the copy is filed, to the millisecond, so every upload is a
  new file.
- **Nothing is replaced or deleted.** A document uploaded again, or taken off the
  application, leaves its earlier copy in the folder, for audit. The application
  points at the latest; the earlier rows of `t_FD_BT_KYC_document`, kept with
  `f_Active = 0`, still point at their own copy.
- **The row** records the name in `f_Doc_FileName` and the full path in
  `f_Doc_Filepath`. The name the partner's file had is not kept.
- **A refused copy** is not filed on the application: it is kept aside under
  `refused/` for a week.

## Registers and lists

| Method | Route | Returns |
|---|---|---|
| GET | `sourcing/brokers/{code}` | `Party` (`code`, `name`): the broker a code names, or 404 |
| GET | `sourcing/staff/{code}?staff=` | `Party`: the employee a code names, among the staff the sourcing mode's rule takes (`branch`, `mfis` or `mflEx`; any employee in service where none is given), or 404. |
| GET | `sourcing/brokers?q={text}`, `sourcing/staff?q={text}` | `Party[]`: the parties whose code or name holds every word of the text, best first, at most 20 — the code fields on Upload Documents are searched this way as they are typed. Both registers are big, so nothing is searched until 3 characters are typed, and neither is ever listed whole. The staff are searched among those the sourcing mode chosen takes (`staff` on the mode: `branch`, `mfis` or `mflEx`, each a query in `MasterQueries.cs` over the employee view; none named, any employee in service) |
| GET | `payin-slips` | `SlipRecord[]`: every application paying by cheque, cancelled ones included |
| GET | `links` | `SentLinkRecord[]`: links sent to investors, each by SMS and e-mail, with the masked `mobile` and `email` it went to. The link itself is never returned. |
| GET | `links/pending` | `PendingRecord[]`: applications waiting on the investor (`appNo`, `investor`, `applied`, masked `mobile` and `email`, `due`) |
| GET | `console/schedule` | `{ windows: WindowRecord[], announcements: AnnouncementRecord[] }` |

Each application in these lists carries its `applied` date. The app counts the
cancellation window (`cancellationDays` in `config`) from that date. A
`SlipRecord` carries `acceptedOn` once the investor accepts a digital
application. Field lists are in `SourcingSlipsLinksAndConsole.cs`.

### Actions on the lists

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `payin-slips/{appNo}` | | `SlipRecord` with its new `slipNo`: the slip, or a fresh one for a reprint. 404 when the application is not found, is cancelled, or is digital and not yet accepted. |
| POST | `links` | `{ appNo, purpose }` | `SentLinkRecord`: `purpose` is `payment` or `acceptance`. It goes to the investor's mobile and e-mail both. Any link sent before stops working. The validity is `linkValidityHours` for the purpose. 404 when the application cannot carry that link. |
| POST | `console/windows` | `{ features, from, to, notice }` | `WindowRecord`. The backend mints the `id` and records `setBy` and `setOn`. An empty `notice` leaves partners untold. |
| POST | `console/announcements` | `{ kind, title, at, detail }` | `AnnouncementRecord`. `kind` is one of `noticeKinds` in `reference`. |
| POST | `console/windows/{id}/end` | | 204. Ends a window that is on, or cancels one still to come, with its notice. 404 when there is none. |
| DELETE | `console/announcements/{id}` | | 204. 404 when there is none. |

The page checks each action before sending it (a tile picked, `from` in the
future and before `to`, a heading), and the backend checks again.

## Dashboard

The dashboard asks for its lists all at once and counts the rows itself; the
backend has no route that returns counts. A list is asked for only while the
user's menu opens its feature. A list that fails is left out and the page still
loads. The code is in `Pages/Dashboard/Index.cshtml.cs`.

| Section | Route | Feature | What the dashboard shows |
|---|---|---|---|
| Waiting on you: Close to auto-cancel | `GET applications`, `GET config` | `view-app` | Applications not `booked` or `cancelled` with 0 to 3 days left, where days left is `cancellationDays` minus the days since `applied`. The soonest is named. |
| Waiting on you: Waiting on the investor | `GET links/pending` | `short-url` | Every row, split by `due` into "to pay" and "to accept". |
| Waiting on you: Pay-in slips to generate | `GET payin-slips` | `pis` | Rows whose `state` is `pending`, on paper or accepted by the investor (`digital` false, or `accepted` true). |
| Continue an incomplete application | `GET applications/drafts` | `new-fd` | The latest 3 drafts, each opening on Upload Documents; the rest are on Investor Identification. |
| Tiles and the bell | `GET console/schedule` | | Tiles switched off for a window, and the notices in the bell. |
| Top bar | `GET me` | | The partner's name. |

Example answers (identifiers masked by the backend):

`GET applications`

```json
[{ "appNo": "FBBMFL26F18317", "folio": "MF0044146", "investor": "Ra•••• Sh•••••",
   "pan": "RASH••••P••••", "amount": 250000, "cumulative": true, "months": 36,
   "payout": "On maturity", "holders": 1, "applied": "2026-09-14T00:00:00",
   "digital": true, "instrument": "Cheque", "branch": "Pune Camp",
   "state": "awaiting", "fdr": null, "step": "Payment", "scheme": "Samruddhi",
   "milestones": [{ "step": "Application raised", "at": "2026-09-14T00:00:00" },
                  { "step": "Payment received", "at": null }] }]
```

`GET config`

```json
{ "sourcingAgency": "1033", "minAge": 18, "seniorAge": 60, "maxJointHolders": 2,
  "maxAttempts": 3, "minAmount": 5000, "maxAmount": 20000000, "amountStep": 1000,
  "cancellationDays": 14, "draftDays": 14,
  "linkValidityHours": { "payment": 48, "acceptance": 72 } }
```

`GET links/pending`

```json
[{ "appNo": "FBBMFL26F20488", "investor": "An•••• Jo•••••",
   "applied": "2026-09-25T00:00:00", "mobile": "90••••1000",
   "due": "payment", "email": "a••••@gmail.com" }]
```

`GET payin-slips`

```json
[{ "appNo": "FBBMFL26F16421", "investor": "Su•••• Pa•••••", "amount": 150000,
   "instrument": "Cheque", "instrumentNo": "104139", "drawnOn": "HDFC Bank",
   "applied": "2026-09-23T00:00:00", "branch": "Nashik Road", "digital": true,
   "accepted": true, "state": "pending", "slipNo": null,
   "acceptedOn": "2026-09-24T00:00:00" }]
```

`GET applications/drafts`

```json
[{ "appNo": "FBBMFL26F10421", "name": "P•••••• K•••••", "pan": "ABCPK••••F",
   "dob": "••/••/1984", "amount": 100000 }]
```

## Outside services

The app asks each check separately, in this order: identification, OCR, then
whoever answers for what was read. Only after that does it file the copy with
DMS. The name and date of birth OCR reads off a proof of address are
matched with the holder's (a name with an initial or a word left out partly
matches). Once both a holder's PAN copy
and their proof of address are filed, the faces on them are compared; for now both answers are only shown,
and the proof stays filed whatever they say. A holder's PAN copy is taken before their proof of
address, and the PAN–Aadhaar link card appears only once an Aadhaar is filed as a
proof.

- **An Aadhaar is taken on its name and date of birth.** The name and date of
  birth OCR reads off an Aadhaar (as proof of address or communication address,
  for the investor or a joint holder) must match the PAN's: the name as NSDL or
  the folio holds it, allowing initials or a name left out, and the date of birth
  exactly. If either differs or can't be read, the copy is refused, counts as an
  attempt, and is kept aside like any other refusal. A masked Aadhaar is
  accepted.
- **An Aadhaar is taken only when its number is read.** OCR must read the whole
  12-digit number or the last 4 digits a masked one still shows. With neither,
  the copy is refused, counts as an attempt, and is kept aside like any other
  refusal.
- **The first 8 digits are typed against the last 4 read.** When an Aadhaar is
  filed for a holder with no folio and OCR reads only its last 4 digits, the
  partner types the first 8 for the PAN–Aadhaar link. The last 4 are shown
  read-only and are taken from the copy's record, never from the page; if they
  were read wrong, the Aadhaar is uploaded again. The whole number must not
  start with 0 or 1 and must pass the Verhoeff check digit. Like a number read,
  it is kept in the server session only and never sent to the backend, except
  to the link check. With OCR switched off nothing is read, and the whole
  12-digit number is typed.
- **Nothing is taken from a copy until it can be filed.** The proof's issuer is
  asked and an Aadhaar is masked before the proof's type, address or number is
  put on the application. If either cannot be done, nothing is filed and the
  proof filed before stays as it was.

Each check is answered by one service, with a settings section of its own:

| Check | Interface | Service | Calls |
|---|---|---|---|
| PAN check | `IPanVerificationService` | PAN verification API (`PanApi`) | `POST VerifyPath` with `App_Code`, `Appl_No`, `Holder_Type`, `PAN_No`, `PAN_Holder_Name`, `PAN_Holder_DOB` (as `dd/MM/yyyy`) and who is asking → `PAN_No_Match_Status`, `PAN_Name_Match_Status`, `PAN_DOB_Match_Status` (`"1"` a match). The PAN and date of birth both matching is `pairOk`; the name matching too is `nameOk`. An answer with no match status at all is the API not having checked: an outage, said with its `ErrorMessage`, not a mismatch. `ErrorCode` and `Status` are read but decide nothing yet. |
| Aadhaar masking | `IMaskingService` | UID masking API (`UidMasking`) | `POST MaskPath` with `MaskLength` (as text), `Trans_Ref_No` (the application number and holder type, `123456_01`), `Source` (`UidMasking:Source`), `CreatedIP`, `FileType` (`IMAGE`) and `FileData` (the copy as Base64), under exactly those names → `Status` `Success` with `Result.FileData` (the masked copy). Any other status, or no masked copy, is masking failing, said with `Error` or `IntStatusDesc`. Called by the upload step for an Aadhaar in this order: identify, OCR, match the name and date of birth with the PAN's, mask, file (or keep aside if refused). |
| CKYC search | `ICkycService` | CKYC search API (`Ckyc`) | `POST SearchPath` with `IncludeImages` and one `SearchInCkycSearchParamDetail` (`InputIdType` `C`, `InputIdNo` the PAN, `DOB`, `ApplicationFormNo`, a ten-digit `TransactionId`, `RecordIdentifier`) → `ckycResponse.searchInCkycResponseDetail[0]`: `ckycAvailable` (`Y` or `Yes` is a record held), `masked_CKYCID`, `ckycName`, `ckycReferenceID`. Asked when the partner chooses Fetch from CKYC on Upload Documents. |
| Identification | `IDocumentIdentifier` | Idfy.Api (`Idfy`) | `documents/validate`: with `docType` for a PAN; with no `docType` for a proof of address, whose `detected_doc_type` says which proof it is (Aadhaar, passport, driving licence or voter ID). A document IDfy has no type for is taken as what it was handed in as. |
| OCR | `IOcrService` | Idfy.Api (`Idfy`) | `pan/extract`, `aadhaar/extract` (QR code read first), `driving-license/extract` (expiry from `date_of_validity` and `validity`, leaving out any date that is an `issue_dates` date or not after the latest one), `passport/extract`, `voter-id/extract`, and `cheque/extract` (IDfy's `ind_cheque`: `account_no`, `ifsc_code`, `micr_code`, `micr_cheque_number`, `date_of_issue`, `bank_name`, `account_name`). No other document is read. |
| Verification | `IVerificationService` | Idfy.Api (`Idfy`) | `driving-license/verify/sync` and `passport/verify/sync` (both with the holder's date of birth), `voter-id/verify/sync`. `id_found` counts as confirmed. For a licence, the later of `nt_validity_to` and `t_validity_to` is returned as `expiry`, and `dl_status` as `standing`. An Aadhaar and a bank account have no check: the answer says it was not asked, and an Aadhaar's address is taken as OCR read it. A cheque's account, read but not confirmed, is still carried to Bank Details & Payment, marked as read and not confirmed. |
| PAN–Aadhaar link | `IPanAadhaarLinkService` | Idfy.Api (`Idfy`) | `pan-aadhaar-link/verify/sync` |
| PAN–POA face match | `IFaceMatchService` | Idfy.Api (`Idfy`) | `face/compare` with `document` (the PAN copy) and `document2` (the proof of address) as Base64, each 150–4,096 px a side; reads `is_a_match`, `match_score`, `review_recommended` and `image_1`/`image_2.face_detected` and `face_quality`. No face found, or a review recommended, is shown as "Not sure". |
| Name screening | `INameScreeningService` | Name screening API (`NameScreening`) | `POST ScreenPath` with the holder's name, date of birth and mobile number → `status` and `nameScreeingStatus`; allowed only on `SUCCESS` and `ALLOWED`. |
| Name match | `INameMatchService` | Name match API (`NameMatch`) | `POST MatchPath { SourceName, TargetName }` → `{ status, error_code, error_message }`. Called as the Idfy.Api client calls: the body whole with its length, and no header but `X-Client-Id`; the gateway refuses it (403) otherwise. |

### What the app expects from every outside service

- **Failures:** a service that is down, slow, answers with an error, or
  answers with something that can't be read is reported to the partner as
  "could not answer". Nothing is filed and no refusal is counted. This holds
  for the Idfy.Api client and the `external/{name}/` clients alike.
- **A proof of address must give the address and its PIN code.** Whichever proof
  it is (Aadhaar, passport, driving licence, voter ID), a copy OCR reads no address
  or no PIN code off is refused: the copy has to show the address. IDfy gives the
  PIN code in a field of its own, which is read and put at the end of the address.
  The address read is the one the application takes, whether its issuer confirms
  the proof or not, and is saved to the holder's permanent address row: its lines
  in `f_Add1` to `f_Add3`, its PIN code in `f_AddPin`. With OCR switched off
  nothing is read, so nothing is asked of the copy.
- **NSDL failing does not cost the PAN copy.** Once a PAN copy is identified and
  read, it is filed whatever NSDL then says. If NSDL holds the PAN against another
  name, the name printed on the card is typed and NSDL is asked again, without the
  copy being identified or read a second time. If NSDL can't answer, or holds no
  such PAN and date of birth, there is no retry for now: the PAN copy is uploaded
  again, and NSDL is asked with it. Uploading another copy is possible until NSDL
  verifies one. Proceed waits until NSDL has verified the PAN.
- **"Not asked" is not a refusal.** Verification returns `notAsked` with a
  reason when it had nothing to ask the issuer with: a passport file number
  (it is on the last page), an unreadable licence number, or a voter ID EPIC
  number that IDfy returned partly masked. The copy is filed and Operations
  settle the address.
- **The link is asked only for a holder with no folio.** A holder on a folio
  never has the PAN–Aadhaar link asked, whatever they file.
- **The link check never costs a copy.** If the PAN–Aadhaar link can't be
  asked, the PAN or Aadhaar that prompted it is still filed. The link is
  marked "not checked" and asked again when an Aadhaar is next filed.
- **Aadhaar number:** only a whole 12-digit number is used for the link
  check: the one OCR reads, or the first 8 digits the partner types joined to
  the last 4 OCR read off a masked copy.

### Rules the app keeps when calling Idfy.Api

- **Images:** sent as Base64 in JSON rather than to the `/upload` routes,
  because those routes accept only `image/*` and a proof can be a PDF. Anything
  over the 3,000,000-character Base64 cap is refused before it is sent.
- **Results:** a task is accepted only when its `status` is `completed`.
- **Retries:** nothing is retried, since a repeated call can be charged twice.
- **Errors:** problem+json errors are logged with their `traceId`, which is
  also shown in the upload history. A failed check files nothing and doesn't
  count against the three attempts.
- **Aadhaar consent:** Aadhaar calls are sent only with the holder's consent.
  The upload step doesn't ask for it, so behind a real IDfy an Aadhaar is
  turned back with a message saying why.
- **The link check needs both numbers:** it runs when the second of the PAN and
  the Aadhaar number arrives. The Aadhaar number, read by OCR or typed, is kept only in
  the server session for that application. It is never saved to the backend.
