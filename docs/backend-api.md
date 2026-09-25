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
| `Backend:External:{Nsdl,Identify,Masking,Ocr,Verification,PanAadhaarLink}` | Optional address for each outside service. The default is `{Backend:BaseUrl}/external/{name}/`, which means the backend proxies it. |
| `Idfy:BaseUrl` | Idfy.Api. When set, IDfy handles the checks it has an endpoint for (see below). |
| `Idfy:TimeoutSeconds` | Default 75. The Idfy.Api guide asks for at least 70. |

In the environment, use a double underscore, for example `Backend__BaseUrl`.

## Common to every backend call

- **Partner:** every request carries `X-Partner-Id`, the partner the call is
  made for. The backend must return only that partner's applications, and
  answer `404` for anyone else's. The header stands in for a signed-in
  partner's token until the app has a sign-in.
- **JSON:** camelCase in both directions.
- **Not found:** `404` means not found wherever a route below says "or 404".
  Any other failure status is treated as an error.

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
| GET | `applications/drafts` | | `DraftSummary[]`: saved applications that are still the partner's to finish, with identifiers masked |
| GET | `applications` | | `ApplicationRecord[]`: the partner's applications, including older ones |

- **`holder`:** `pan`, `dob`, `name`, `folio`, `panFiled`, `address`, and
  `onRecord { pan, photo, poa }` or null.
- **`Application`:** `appNo`, `holder`, `version`, `upload` (an `UploadState`,
  or null until first saved), and `prior` (attempts on record from before the
  upload step, newest first, as `LogEntry[]`).
- **`UploadState`:** the upload step as a whole. That covers the choices made
  (`appType`, `poaType`, `payMode`, `sourcing`, `sourceCode`, `subBroker`,
  `category`, the `emp*` fields, `formNo`, `typedFormNo`, `ckyc`, `savedAt`),
  plus `docs`, `attempts`, `reads` and `log`. See `Applications.cs` for every
  field. It never contains a file or an Aadhaar number.

## Documents (DMS)

| Method | Route | Body | Returns |
|---|---|---|---|
| POST | `applications/{appNo}/documents/{slot}` | multipart `file` | `2xx`. Files the copy against the slot. |
| DELETE | `applications/{appNo}/documents/{slot}` | | `2xx`, or 404 if there is no copy (the app treats that as done) |
| POST | `applications/{appNo}/documents/{slot}/refused` | multipart `file` | `{ ref, keptUntil }`. Keeps a refused copy aside for analysis, off the application. |
| GET | `applications/{appNo}/documents/{slot}` | | The copy, with `Content-Type` and `Content-Disposition`, or 404 |

Slots are `form`, `pan`, `photo`, `poa`, `payment` and `empproof`.

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
| GET | `links` | `SentLinkRecord[]`: links sent to investors. The link itself is never returned. |
| GET | `links/pending` | `PendingRecord[]`: applications waiting on the investor (`appNo`, `investor`, `applied`, masked `mobile`, `due`) |
| GET | `console/schedule` | `{ windows: WindowRecord[], announcements: AnnouncementRecord[] }` |

Each application in these lists carries its `applied` date. The app counts the
14-day cancellation window from that date. Field lists are in `Registers.cs`.

## Outside services

The app asks each check separately, in this order: identification, masking (for
an Aadhaar only), OCR, then whoever answers for what was read. Only after that
does it file the copy with DMS.

| Service | Interface | With Idfy.Api | Without IDfy (`external/{name}/`) |
|---|---|---|---|
| NSDL | `INsdlService` | Not covered by IDfy | `POST verify { pan, dob, name }` → `{ pairOk, nameOk }` |
| Identification | `IDocumentIdentifier` | `POST /api/documents/validate` with `docType`, for a PAN, Aadhaar, passport, driving licence or voter ID | `POST identify` (multipart `file`, `expected`, `type`) → `{ matches, hint }`. Used for a cheque or a utility bill. |
| Masking | `IMaskingService` | `POST /api/aadhaar/mask`. A copy with no number to mask (`id_number_found: false`) is already masked. | `POST check` (multipart `file`, `consent`) → `{ masked }` |
| OCR | `IOcrService` | `/api/pan/extract`, `/api/aadhaar/extract` (QR code read first), `/api/driving-license/extract`, `/api/passport/extract`, `/api/voter-id/extract` | `POST read` (multipart `file`, `kind`, `type`, `pan`, `dob`, `name`, `consent`) → `OcrReading`. Used for a utility bill or a cheque. |
| Verification | `IVerificationService` | `/api/driving-license/verify/sync` and `/api/passport/verify/sync` (both with the holder's date of birth), `/api/voter-id/verify/sync`. `id_found` counts as confirmed. | `POST proof { proofType, reading, holderDob }` and `POST account { account, bank }` → `{ confirmed, verifier, notAsked }`. Used for an Aadhaar (IDfy has no UIDAI source check) and for bank accounts. |
| PAN–Aadhaar link | `IPanAadhaarLinkService` | `/api/pan-aadhaar-link/verify/sync` | `POST check { pan, aadhaarNumber }` → `{ link }` |

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
