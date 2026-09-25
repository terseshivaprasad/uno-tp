# Uno TP backend API

This document lists every call the Uno TP web app makes for its data. The app
never connects to a database. It reads and writes everything through the
backend API described here, plus a few outside services for document checks.
The client code is in `src/UnoTP.Backend`. `BackendClient.cs` holds the
backend routes, and there is one file per outside service in `External/` and
`Idfy/`.

These routes are the ones the app proposes. If the backend's routes differ,
change `BackendClient.cs`: the pages depend only on the interfaces.

## Configuration

| Setting | Meaning |
|---|---|
| `Backend:BaseUrl` | The backend API. If blank, the app runs on the in-memory mock (`src/UnoTP.Backend.Mock`). |
| `Backend:TimeoutSeconds` | Default 30. |
| `Backend:External:{Nsdl,Identify,Masking,Ocr,Verification,PanAadhaarLink,FaceMatch,Decrypt}` | Optional address for each outside service. The default is `{Backend:BaseUrl}/external/{name}/`, which means the backend proxies it. |
| `Idfy:BaseUrl` | Idfy.Api. When set, IDfy handles the checks it has an endpoint for (see below). |
| `Idfy:TimeoutSeconds` | Default 75. The Idfy.Api guide asks for at least 70. |
| `Entry:DemoUserId`, `Entry:DemoSysCode` | The user the demo comes in as when the app is opened without the portal's values. Used only while `Features:DemoData` is on; leave empty in production. |
| `Backend:ReferenceCacheMinutes` | Minutes the lists and rules (`GET reference`, `GET config`) are kept for. Default 10; 0 asks every time. |

In the environment, use a double underscore, for example `Backend__BaseUrl`.

## Common to every backend call

- **Partner:** every request carries `X-Partner-Id`, the user the portal sent
  in, and `X-Session-Id`, the session the backend started for them. The backend
  must return only that user's applications, and answer `404` for anyone
  else's. A `401` on any call means the session has ended: the app shows
  Session Expired.
- **JSON:** camelCase in both directions.
- **Not found:** `404` means not found wherever a route below says "or 404".
  Any other failure status is treated as an error.

## Entry

The portal opens the app at `/Home?UserId=...&Syscode=...` (or at `/` with the
same query), both values encrypted. The app decrypts each with the portal's
decryption service, starts a session, reads the user's menu, keeps all three in
the server session, and redirects to the Dashboard, so neither value stays in
the address. Every other page needs that session; without one, or once it has
ended, the partner sees Session Expired. A refused entry shows Unauthorized.

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `external/decrypt/decrypt` (or `Backend:External:Decrypt`) | `{ value }` | `{ value }` in plain text, or `400` when it cannot be decrypted |
| POST | `sessions` | `{ userId, sysCode }` | `UserSession`: `sessionId`, `userId`, `expiresAt`. `401`, `403` or `404` when the user or system code is refused. |
| GET | `menu` | | `MenuItem[]` (`key`, `name`) for the session's user. `key` is a console feature: `new-fd`, `pis`, `view-app`, `short-url`, `app-status`, `renew` or `admin`. A feature the menu leaves out is closed, and its address shows Unauthorized. An empty menu refuses entry. |

## Lists, rules and the partner

The pages hold no data of their own: every list they offer, every limit they
check, and who is signed in come from these routes. The lists and the rules are
kept for `Backend:ReferenceCacheMinutes` (default 10) before they are asked for
again; a failed answer is not kept.

| Method | Route | Body | Returns |
|---|---|---|---|
| GET | `reference` | | `ReferenceData`: every list the pages offer (see below) |
| GET | `config` | | `AppConfig`: the limits and rules the pages check |
| GET | `me` | | `PartnerProfile`: `name`, `code`, `agencyType`, `brokerCode` of the signed-in partner. The `sourcingAgency` in `config` chooses the sourcing mode, broker code and deposit category; any other type sources as a broker under `brokerCode`, with the category set from the holder's date of birth and gender. While `Features:DemoData` is on, `?agency=2001&broker=BR10874` shows the app as another kind of partner for the session. |
| POST | `deposits/quote` | `{ amount, tenureMonths, payout, category, startsOn? }` | `DepositQuote`: `rate`, `interestEach`, `maturityAmount`, `maturesOn`, `rateAsOn` |
| GET | `ifsc/{code}` | | `BankBranch`: `ifsc`, `bank`, `branch`, `micr`, or 404 |
| GET | `demo/cases` | | `DemoCases`: `dob`, `cases` (`{ pan, dob, folios, shows }`; an empty `dob` is none on record, no `folios` a new investor) and `notes`, for the Test data card on Investor Identification while `Features:DemoData` is on. A live backend answers 404 and the card is not shown. |

- **`ReferenceData`:** `applicationTypes` and `renewInstructions` and
  `deliveryTypes` as `{ code, name }`; `categories` as `{ code, name, employee,
  women, senior }`; `paymentModes` as `{ name, document }` (`document` is the
  instrument a copy is filed for, or null); `sourcingModes` as `{ code, name,
  codeLabel, nameLabel, house, search, register, sub, categories }`;
  `proofsOfAddress` as `{ type, issuer, hasPhoto }`; `payouts` as `{ code, name,
  perYear, each }` (`perYear` 0 is cumulative); `tenures` in months;
  `requiredDocuments` as `{ title, items, notes }`; and plain lists for
  `employeeHolders`, `employeeRelations`, `employeeProofs`, `incomeBands`,
  `occupations`, `subOccupations`, `maritalStatuses`, `genders`, `nameTypes`,
  `nomineeRelations`, `cmsLocations`, `identificationNotes`, `dashboardNotes`,
  `noticeKinds` (for Console Admin) and `declarations` (signed on Review Summary before submitting).
  Codes are what the app posts and saves.
- **`AppConfig`:** `sourcingAgency` (the agency type that chooses how an
  application is sourced), `minAge`, `seniorAge`, `maxJointHolders`,
  `maxAttempts`, `minAmount`, `maxAmount`, `amountStep` (rupees),
  `cancellationDays`, `draftDays`, and `linkValidityHours` by purpose
  (`payment`, `acceptance`). The backend checks the same rules again on save.
- **Quote:** the rate is the card rate for the tenure and category, locked
  when the application is submitted. The mock compounds a cumulative deposit
  half-yearly and pays simple interest per period otherwise.

## Investors

| Method | Route | Body | Returns |
|---|---|---|---|
| GET | `investors/folios?pan={pan}` | | `FolioRecord[]`. Every folio held against the PAN; more than one is a record Operations has to merge. |
| GET | `investors/folios/{folio}` | | `FolioRecord`, or 404 |

`FolioRecord`: `pan`, `dob` (`dd-MM-yyyy`, or `""` when none is held),
`folio`, `name`, `gender`, `address` (`""` when none is held),
`docs { pan, photo, poa }` (booleans), `note`.

## Applications

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `applications` | `{ holder }` | `Application`. The backend mints the application number. |
| GET | `applications/{appNo}` | | `Application`, or 404. Drafts included. |
| PUT | `applications/{appNo}/upload` | `UploadState`, with header `If-Match: "{version}"` | `{ version }`, or `409`/`412` if the application changed since that version was read. Nothing is saved in that case. |
| PUT | `applications/{appNo}/details` | `ApplicationDetails`, with `If-Match` | `{ version }`, or `409`/`412` |
| PUT | `applications/{appNo}/payment` | `PaymentDetails`, with `If-Match` | `{ version }`, or `409`/`412` |
| PUT | `applications/{appNo}/deposit` | `DepositDetails`, with `If-Match` | `{ version }`, or `409`/`412` |
| POST | `applications/{appNo}/submit` | with `If-Match` | `Application` as submitted, or `409`/`412`. Sends the investor the payment link by SMS and e-mail both. |
| POST | `applications/{appNo}/resend-link` | | `Submission`, or `404`/`409` when there is no link to resend |
| GET | `applications/drafts` | | `DraftSummary[]`: saved applications that are still the partner's to finish, with identifiers masked |
| GET | `applications` | | `ApplicationRecord[]`: the partner's applications, including older ones, each with its `scheme` name and `milestones` (`{ step, at }`, in order) for the View Application timeline |

- **`holder`:** `pan`, `dob`, `name`, `folio`, `panFiled`, `address`,
  `onRecord { pan, photo, poa }` or null, and `gender` (the folio's, or `""`).
- **`Application`:** `appNo`, `holder`, `version`, `upload` (an `UploadState`,
  or null until first saved), and `prior` (attempts on record from before the
  upload step, newest first, as `LogEntry[]`).
- **`ApplicationDetails`:** Investor Information: `holders` (one per holder
  type: `holder` 01/02/03, `gender`, `nameType`, `parentName`, `annualIncome`,
  `occupation`, `subOccupation`, `maritalStatus`, `mobile`, `email`,
  `fatcaTaxResident`, `fatcaPermanentResident`, `pep`, `pepRelated`) and
  `nominee` (`name`, `dob`, `relation`, `guardianName`, `guardianLine1`-`3`,
  `guardianPinCode`, `guardianCity`) or null.
- **`PaymentDetails`:** `payment` and `repayment` as `{ ifsc, accountNumber }`,
  `repaymentSameAsPayment`, and `cheque` as `{ number, date, cmsLocation }` or
  null.
- **`DepositDetails`:** `amount`, `tenureMonths`, `payout`, `autoRenewal`,
  `renewInstruction`, `noTds`, `deliveryType`, as the reference lists code them.
- **`Submission`:** `at`, `status`, `linkSentTo` (the mobile, masked), `linkValidUntil`,
  `resendsLeft`, `linkEmailedTo` (the e-mail, masked; empty when there is none). `Application` carries it as `submitted`, with `details`,
  `payment` and `deposit`.
- **`UploadState`:** the upload step as a whole. That covers the choices made
  (`appType`, `poaType`, `payMode`, `sourcing`, `sourceCode`, `subBroker`,
  `category`, the `emp*` fields, `formNo`, `typedFormNo`, `ckyc`, `savedAt`,
  and `gender` as read off an Aadhaar for a holder the folio gives none for),
  plus `docs`, `attempts`, `reads` and `log`. See `Applications.cs` for every
  field. It never contains a file or an Aadhaar number.
- **Joint holders:** `joint` maps a holder type (`02` the second holder, `03`
  the third) to `{ holder, poaType }`, added on Investor Information. Their
  documents sit in `docs`, `attempts` and `reads` under keys that begin with it
  (`h02-pan`, `h03-poa`). A log entry carries `holder` (`02`, `03`) when it is a
  joint holder's, and `removed` once that holder is taken off. CKYC (`ckyc`)
  is fetched for the investor only, so it never takes a joint holder's
  photograph or proof of address off.

**Deposit categories:** `PUBLIC/GENERAL`, `WOMEN`, `SR CITIZEN` and `SR CITIZEN
WOMEN` (60 or over), plus `EMPLOYEE` and `EMPLOYEE WOMEN`, which only a 1033
partner can book, under MFL-EX.

## Documents (DMS)

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `applications/{appNo}/documents/{holder}/{slot}` | multipart `file` | `2xx`. Files the copy against the holder's slot. |
| DELETE | `applications/{appNo}/documents/{holder}/{slot}` | | `2xx`, or 404 if there is no copy (the app treats that as done) |
| POST | `applications/{appNo}/documents/{holder}/{slot}/refused` | multipart `file` | `{ ref, keptUntil }`. Keeps a refused copy aside for analysis, off the application. |
| GET | `applications/{appNo}/documents/{holder}/{slot}` | | The copy, with `Content-Type` and `Content-Disposition`, or 404 |

`{holder}` is the holder type DMS files under:

| Code | Holder | Slots |
|---|---|---|
| `00` | Not holder-specific | `form`, `payment`, `empproof` |
| `01` | Investor | `pan`, `photo`, `poa` |
| `02` | Second holder | `pan`, `photo`, `poa` |
| `03` | Third holder | `pan`, `photo`, `poa` |

A holder never changes type: the second holder can only be removed once there
is no third, so a third never becomes the second.

A slot holds one copy:

- **Re-upload:** the app deletes the old copy first, then files the new one.
  This happens only once the new copy has passed its checks; a refused copy
  leaves the old one in place.
- **Dropped document:** when the partner's choices take a document off the
  application (for example, switching to a payment mode that has no
  instrument), the app deletes its copy once the save goes through.

## Registers and lists

| Method | Route | Returns |
|---|---|---|
| GET | `sourcing/brokers` | `Party[]` (`code`, `name`) |
| GET | `sourcing/staff` | `Party[]`. Includes the partner at the keyboard. |
| GET | `payin-slips` | `SlipRecord[]`: every application paying by cheque or DD, cancelled ones included |
| GET | `links` | `SentLinkRecord[]`: links sent to investors, each by SMS and e-mail, with the masked `mobile` and `email` it went to. The link itself is never returned. |
| GET | `links/pending` | `PendingRecord[]`: applications waiting on the investor (`appNo`, `investor`, `applied`, masked `mobile` and `email`, `due`) |
| GET | `console/schedule` | `{ windows: WindowRecord[], announcements: AnnouncementRecord[] }` |

Each application in these lists carries its `applied` date. The app counts the
cancellation window (`cancellationDays` in `config`) from that date. A
`SlipRecord` carries `acceptedOn` once the investor accepts a digital
application. Field lists are in `Registers.cs`.

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

## Outside services

The app asks each check separately, in this order: identification, OCR, then
whoever answers for what was read. Only after that does it file the copy with
DMS. The name and date of birth OCR reads off a proof of address are
matched with the holder's (a name with an initial or a word left out partly
matches; a utility bill prints no date of birth). Once both a holder's PAN copy
and their proof of address are filed, the faces on them are compared; for now both answers are only shown,
and the proof stays filed whatever they say. A holder's PAN copy is taken before their proof of
address, and the PAN–Aadhaar link card appears only once an Aadhaar is filed as a
proof.

- **An Aadhaar is filed unmasked.** If OCR can't read all 12 digits of its
  number (the copy is masked, or not clear enough), the copy is refused, counts
  as an attempt, and is kept aside like any other refusal. The masking service
  is not called by the upload step.

| Service | Interface | With Idfy.Api | Without IDfy (`external/{name}/`) |
|---|---|---|---|
| NSDL | `INsdlService` | Not covered by IDfy | `POST verify { pan, dob, name }` → `{ pairOk, nameOk }` |
| Identification | `IDocumentIdentifier` | `POST /api/documents/validate`: with `docType` for a PAN; with no `docType` for a proof of address, whose `detected_doc_type` says which proof it is (Aadhaar, passport, driving licence or voter ID) | `POST identify` (multipart `file`, `expected`, `type`) → `{ matches, hint, type }`. Used for a cheque, and for a proof of address IDfy does not know (a utility bill). For a proof of address `type` is sent empty and the answer's `type` says which proof it is: `Aadhaar`, `Passport`, `Driving Licence`, `Voter ID` or `Utility bill`. That becomes the proof's type on the application: it is never chosen. |
| Masking | `IMaskingService` | `POST /api/aadhaar/mask`. A copy with no number to mask (`id_number_found: false`) is already masked. | `POST check` (multipart `file`, `consent`) → `{ masked }`. Not called by the upload step, which relies on OCR reading the whole number instead. |
| OCR | `IOcrService` | `/api/pan/extract`, `/api/aadhaar/extract` (QR code read first), `/api/driving-license/extract` (expiry from `date_of_validity` and `validity`, leaving out any date that is an `issue_dates` date or not after the latest one), `/api/passport/extract`, `/api/voter-id/extract` | `POST read` (multipart `file`, `kind`, `type`, `pan`, `dob`, `name`, `consent`) → `OcrReading` (a PAN card's carries `pan` and `dob` as `dd-MM-yyyy`, which must match the holder's or the copy is refused; an Aadhaar's also carries `gender`). Used for a utility bill or a cheque. |
| Verification | `IVerificationService` | `/api/driving-license/verify/sync` and `/api/passport/verify/sync` (both with the holder's date of birth), `/api/voter-id/verify/sync`. `id_found` counts as confirmed. For a licence, the later of `nt_validity_to` and `t_validity_to` is returned as `expiry`, and `dl_status` as `standing`. | `POST proof { proofType, reading, holderDob }` and `POST account { account, bank }` → `{ confirmed, verifier, notAsked, expiry, standing }` (`expiry` as `dd-MM-yyyy`, or empty; once confirmed it replaces the expiry OCR read off the copy). Used for an Aadhaar (IDfy has no UIDAI source check) and for bank accounts. |
| PAN–Aadhaar link | `IPanAadhaarLinkService` | `/api/pan-aadhaar-link/verify/sync` | `POST check { pan, aadhaarNumber }` → `{ link }` |
| PAN–POA face match | `IFaceMatchService` | `POST /api/face/compare` with `document` (the PAN copy) and `document2` (the proof of address) as Base64, each 150–4,096 px a side; reads `is_a_match`, `match_score`, `review_recommended` and `image_1`/`image_2.face_detected` and `face_quality`. No face found, or a review recommended, is shown as "Not sure" | `POST compare` (multipart `pan`, `proof`) → `{ matched, score, unsure }` |

### What the app expects from every outside service

- **Failures:** a service that is down, slow, answers with an error, or
  answers with something that can't be read is reported to the partner as
  "could not answer". Nothing is filed and no refusal is counted. This holds
  for the Idfy.Api client and the `external/{name}/` clients alike.
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
  check. A masked number, or the partial one a secure QR code carries, is
  ignored, and the printed number is used instead.

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
  turned back with a message saying why. The mock doesn't ask for consent.
- **The link check needs both numbers:** it runs when the second of the PAN and
  the Aadhaar number arrives. The Aadhaar number, read by OCR, is kept only in
  the server session for that application. It is never saved to the backend.
