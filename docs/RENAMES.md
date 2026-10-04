# Renames: old name to new

Every name changed when the short prefixes were replaced with plain words (October 2026). Search this page for an old name found in notes, screenshots or an older branch.

## Files (under `src/UnoTP/wwwroot/`)

| Old | New |
|---|---|
| `css/shared/theme.css` | `css/shared/bootstrap-theme.css` |
| `css/shared/base.css` | `css/shared/layout-and-controls.css` |
| `css/shared/wizard.css` | `css/shared/journey-steps.css` |
| `css/shared/registers.css` | `css/shared/register-pages.css` |
| `js/shared/checks.js` | `js/shared/field-checks.js` |
| `js/shared/pin.js` | `js/shared/sticky-app-strip.js` |
| `js/shared/date-parts.js` | `js/shared/date-input.js` |
| `js/shared/notices.js` | `js/shared/notices-bell.js` |

## C# files (October 2026)

File names changed to say what the file holds. Only the files were renamed: the classes inside keep their names.

| Old | New |
|---|---|
| `UnoTP.Data/Db.cs` | `UnoTP.Data/DatabaseConnection.cs` |
| `UnoTP.Data/FileDocuments.cs` | `UnoTP.Data/DocumentFileStore.cs` |
| `UnoTP.Data/Masks.cs` | `UnoTP.Data/MaskedNameAndPan.cs` |
| `UnoTP.Data/Models/Documents.cs` | `UnoTP.Data/Models/DocumentStore.cs` |
| `UnoTP.Data/Models/Entry.cs` | `UnoTP.Data/Models/SignInSession.cs` |
| `UnoTP.Data/Models/Masking.cs` | `UnoTP.Data/Models/AadhaarMasking.cs` |
| `UnoTP.Data/Models/Places.cs` | `UnoTP.Data/Models/PinCodes.cs` |
| `UnoTP.Data/Models/Reference.cs` | `UnoTP.Data/Models/ListsSettingsAndRates.cs` |
| `UnoTP.Data/Models/Registers.cs` | `UnoTP.Data/Models/SourcingSlipsLinksAndConsole.cs` |
| `UnoTP.Data/Models/Verification.cs` | `UnoTP.Data/Models/DocumentVerification.cs` |
| `UnoTP.Data/Rows.cs` | `UnoTP.Data/ApplicationTableRows.cs` |
| `UnoTP.Data/Sections.Deposit.cs` | `UnoTP.Data/SaveStep.FdConfiguration.cs` |
| `UnoTP.Data/Sections.Details.cs` | `UnoTP.Data/SaveStep.InvestorInformation.cs` |
| `UnoTP.Data/Sections.Documents.cs` | `UnoTP.Data/SaveStep.DocumentCheckFlags.cs` |
| `UnoTP.Data/Sections.Payment.cs` | `UnoTP.Data/SaveStep.BankAndPayment.cs` |
| `UnoTP.Data/Sections.cs` | `UnoTP.Data/SaveStep.UploadDocuments.cs` |
| `UnoTP.Data/SqlConsole.cs` | `UnoTP.Data/SqlConsoleWindowsAndNotices.cs` |
| `UnoTP.Data/SqlDataServiceCollectionExtensions.cs` | `UnoTP.Data/RegisterDataServices.cs` |
| `UnoTP.Data/SqlReference.Documents.cs` | `UnoTP.Data/SqlReference.DocumentCodes.cs` |
| `UnoTP.Data/SqlReference.Masters.cs` | `UnoTP.Data/SqlReference.CodeLists.cs` |
| `UnoTP.Data/SqlRegisters.cs` | `UnoTP.Data/SqlPayInSlipsAndLinks.cs` |
| `UnoTP/Infrastructure/AppUrls.cs` | `UnoTP/Infrastructure/OtherAppAddresses.cs` |
| `UnoTP/Infrastructure/ApplicationUrls.cs` | `UnoTP/Infrastructure/ApplicationNumberInUrls.cs` |
| `UnoTP/Infrastructure/Lookups.cs` | `UnoTP/Infrastructure/CachedListsAndSettings.cs` |
| `UnoTP/Infrastructure/PartialFollow.cs` | `UnoTP/Infrastructure/PartialPostFollowsRedirect.cs` |
| `UnoTP/Infrastructure/SessionStarts.cs` | `UnoTP/Infrastructure/SessionStartRequestDetails.cs` |
| `UnoTP/Models/Flash.cs` | `UnoTP/Models/MessageAfterPost.cs` |
| `UnoTP/Services/ApiClient.cs` | `UnoTP/Services/BackendHttpClientBase.cs` |
| `UnoTP/Services/ExternalClient.cs` | `UnoTP/Services/OutsideServiceHttpClientBase.cs` |
| `UnoTP/Services/OutsideSwitches.cs` | `UnoTP/Services/OutsideServiceSwitches.cs` |
| `UnoTP/Services/SwitchedOff.cs` | `UnoTP/Services/SwitchedOffChecks.cs` |
| `UnoTP/ViewModels/Dates.cs` | `UnoTP/ViewModels/DateText.cs` |
| `UnoTP/ViewModels/DocSlotBlock.cs` | `UnoTP/ViewModels/DocumentSlotBlock.cs` |
| `UnoTP/ViewModels/DocumentsViewModel.Outstanding.cs` | `UnoTP/ViewModels/DocumentsViewModel.SourcingNamesAndOutstanding.cs` |
| `UnoTP/ViewModels/DocumentsViewModel.Posts.cs` | `UnoTP/ViewModels/DocumentsViewModel.PostedChanges.cs` |
| `UnoTP/ViewModels/DocumentsViewModel.Reference.cs` | `UnoTP/ViewModels/DocumentsViewModel.ListsCategoryAndSourcing.cs` |
| `UnoTP/ViewModels/DocumentsViewModel.Slots.cs` | `UnoTP/ViewModels/DocumentsViewModel.DocumentSlots.cs` |
| `UnoTP/ViewModels/Glossary.cs` | `UnoTP/ViewModels/TradeTermGlossary.cs` |
| `UnoTP/ViewModels/Money.cs` | `UnoTP/ViewModels/MoneyAndSentToText.cs` |
| `UnoTP/ViewModels/ReadItem.cs` | `UnoTP/ViewModels/ReadCardBlocks.cs` |
| `UnoTP/ViewModels/StepRail.cs` | `UnoTP/ViewModels/JourneyStepRail.cs` |
| `UnoTP/ViewModels/Tones.cs` | `UnoTP/ViewModels/StatusBadgeTones.cs` |
| `UnoTP/ViewModels/UploadForm.cs` | `UnoTP/ViewModels/UploadDocumentsForm.cs` |

## CSS classes and element ids (593)

| Old | New |
|---|---|
| `adm-control--wide` | `admin-control--wide` |
| `adm-form` | `admin-form` |
| `adm-form__actions` | `admin-form__actions` |
| `adm-form__row` | `admin-form__row` |
| `adm-form__title` | `admin-form__title` |
| `adm-kind` | `admin-kind` |
| `adm-kindtag` | `admin-kind-tag` |
| `adm-pick` | `admin-pick` |
| `adm-pick--off` | `admin-pick--off` |
| `adm-picks` | `admin-picks` |
| `adm-stack` | `admin-stack` |
| `adm-table` | `admin-table` |
| `adm-table--notices` | `admin-table--notices` |
| `adm-textarea` | `admin-textarea` |
| `adm-time` | `admin-time` |
| `adm-when` | `admin-when` |
| `cii-` | `investor-` |
| `cii-add` | `investor-add` |
| `cii-alert` | `page-alert` |
| `cii-alert__close` | `page-alert__close` |
| `cii-alert__icon` | `page-alert__icon` |
| `cii-alert__text` | `page-alert__text` |
| `cii-app` | `page-app-number` |
| `cii-ask` | `investor-question` |
| `cii-ask__q` | `investor-question__question` |
| `cii-card` | `section-card` |
| `cii-clear` | `investor-clear` |
| `cii-docs-head` | `investor-docs-head` |
| `cii-docs-head__title` | `investor-docs-head__title` |
| `cii-fold` | `investor-folded-holder` |
| `cii-fold__chev` | `investor-folded-holder__chevron` |
| `cii-fold__line` | `investor-folded-holder__line` |
| `cii-fold__tools` | `investor-folded-holder__tools` |
| `cii-guardian` | `investor-guardian` |
| `cii-guardian__minor` | `investor-guardian__minor` |
| `cii-guardian__when` | `investor-guardian__when` |
| `cii-head` | `page-head` |
| `cii-identify` | `investor-identify` |
| `cii-note` | `page-note` |
| `cii-pep` | `investor-pep` |
| `cii-pep__answer` | `investor-pep__answer` |
| `cii-pep__answers` | `investor-pep__answers` |
| `cii-pep__error` | `investor-pep__error` |
| `cii-pep__q` | `investor-pep__question` |
| `cii-pep__row` | `investor-pep__row` |
| `cii-place` | `investor-district-state` |
| `cii-record` | `investor-record` |
| `cii-section` | `section-title` |
| `cii-switches` | `investor-switches` |
| `cii-title` | `page-title` |
| `cii-title--section` | `page-title--section` |
| `cii-unfinished` | `investor-unfinished` |
| `classic` | `dashboard-panel` |
| `classic-actions` | `dashboard-actions` |
| `classic-actions__rule` | `dashboard-actions__rule` |
| `classic-actions__title` | `dashboard-actions__title` |
| `classic-closed` | `dashboard-closed` |
| `classic-docs` | `dashboard-docs` |
| `classic-note` | `dashboard-note` |
| `classic-note__title` | `dashboard-note__title` |
| `classic-step` | `dashboard-step` |
| `classic-steps` | `dashboard-steps` |
| `classic-steps__list` | `dashboard-steps__list` |
| `classic-steps__title` | `dashboard-steps__title` |
| `classic-tile` | `dashboard-tile` |
| `classic-tile--off` | `dashboard-tile--off` |
| `classic-tile__icon` | `dashboard-tile__icon` |
| `classic-tile__off` | `dashboard-tile__off` |
| `classic-tile__title` | `dashboard-tile__title` |
| `classic-tiles` | `dashboard-tiles` |
| `csi` | `page-layout` |
| `csi--wide` | `page-layout--wide` |
| `csi-bar` | `page-action-bar` |
| `csi-bar__hint` | `page-action-bar__hint` |
| `csi-bar__inner` | `page-action-bar__inner` |
| `csi-bar__right` | `page-action-bar__right` |
| `csi-bar__saved` | `page-action-bar__saved` |
| `csi-card` | `page-card` |
| `csi-card__title` | `page-card__title` |
| `csi-card__title--plain` | `page-card__title--plain` |
| `csi-check` | `identify-check-button` |
| `csi-control` | `field-input` |
| `csi-control--caps` | `field-input--caps` |
| `csi-control--masked` | `field-input--masked` |
| `csi-date` | `date-input` |
| `csi-date__part` | `date-input__part` |
| `csi-date__part--year` | `date-input__part--year` |
| `csi-date__sep` | `date-input__sep` |
| `csi-demo` | `demo-data` |
| `csi-demo__head` | `demo-data__head` |
| `csi-demo__no` | `demo-data__no` |
| `csi-demo__none` | `demo-data__none` |
| `csi-demo__notes` | `demo-data__notes` |
| `csi-demo__sub` | `demo-data__sub` |
| `csi-demo__table` | `demo-data__table` |
| `csi-demo__title` | `demo-data__title` |
| `csi-drafts` | `page-drafts` |
| `csi-drafts__act` | `page-drafts__actions` |
| `csi-drafts__detail` | `page-drafts__detail` |
| `csi-drafts__head` | `page-drafts__head` |
| `csi-drafts__sub` | `page-drafts__sub` |
| `csi-drafts__table` | `page-drafts__table` |
| `csi-drafts__title` | `page-drafts__title` |
| `csi-error` | `field-error` |
| `csi-field` | `form-field` |
| `csi-field--check` | `form-field--check` |
| `csi-fields` | `field-grid` |
| `csi-gap` | `page-missing-items` |
| `csi-gap__body` | `page-missing-items__body` |
| `csi-gap__icon` | `page-missing-items__icon` |
| `csi-gap__title` | `page-missing-items__title` |
| `csi-hint` | `field-hint` |
| `csi-holder` | `identify-holder` |
| `csi-holder__head` | `identify-holder__head` |
| `csi-holder__name` | `identify-holder__name` |
| `csi-holder__no` | `identify-holder__no` |
| `csi-label` | `field-label` |
| `csi-left` | `page-remaining` |
| `csi-left__list` | `page-remaining__list` |
| `csi-left__more` | `page-remaining__more` |
| `csi-link` | `link-button` |
| `csi-link--muted` | `link-button--muted` |
| `csi-main` | `page-main` |
| `csi-note` | `page-warning-note` |
| `csi-note--banner` | `page-warning-note--banner` |
| `csi-note__body` | `page-warning-note__body` |
| `csi-note__icon` | `page-warning-note__icon` |
| `csi-note__title` | `page-warning-note__title` |
| `csi-onrecord` | `page-on-record` |
| `csi-onrecord--complete` | `page-on-record--complete` |
| `csi-onrecord__foot` | `page-on-record__foot` |
| `csi-onrecord__item` | `page-on-record__item` |
| `csi-onrecord__item--missing` | `page-on-record__item--missing` |
| `csi-onrecord__label` | `page-on-record__label` |
| `csi-onrecord__list` | `page-on-record__list` |
| `csi-ops` | `identify-actions` |
| `csi-radio` | `radio-option` |
| `csi-rail` | `page-rail` |
| `csi-rail--steps` | `page-rail--steps` |
| `csi-rail__bar` | `page-rail__bar` |
| `csi-rail__check` | `page-rail__check` |
| `csi-rail__count` | `page-rail__count` |
| `csi-rail__dot` | `page-rail__dot` |
| `csi-rail__label` | `page-rail__label` |
| `csi-rail__note` | `page-rail__note` |
| `csi-rail__note-title` | `page-rail__note-title` |
| `csi-rail__now` | `page-rail__now` |
| `csi-rail__num` | `page-rail__num` |
| `csi-rail__rule` | `page-rail__rule` |
| `csi-rail__step` | `page-rail__step` |
| `csi-rail__step--current` | `page-rail__step--current` |
| `csi-rail__step--done` | `page-rail__step--done` |
| `csi-rail__step--end` | `page-rail__step--end` |
| `csi-rail__title` | `page-rail__title` |
| `csi-rail__where` | `page-rail__where` |
| `csi-result` | `identify-result` |
| `csi-result__again` | `identify-result__again` |
| `csi-result__head` | `identify-result__head` |
| `csi-result__name` | `identify-result__name` |
| `csi-result__said` | `identify-result__said` |
| `csi-result__tag` | `identify-result__tag` |
| `csi-result__tag--new` | `identify-result__tag--new` |
| `csi-search` | `page-search` |
| `csi-searchby` | `search-by` |
| `csi-searchby--first` | `search-by--first` |
| `csi-status` | `identify-status` |
| `csi-status--ok` | `identify-status--ok` |
| `cud-` | `doc-` |
| `cud-apptype` | `doc-app-type` |
| `cud-apptype__group` | `doc-app-type__group` |
| `cud-apptype__note` | `doc-app-type__note` |
| `cud-banner` | `doc-banner` |
| `cud-check` | `doc-check` |
| `cud-check__dot` | `doc-check__dot` |
| `cud-check__from` | `doc-check__from` |
| `cud-check__kind` | `doc-check__kind` |
| `cud-check__lines` | `doc-check__lines` |
| `cud-check__state` | `doc-check__state` |
| `cud-checks` | `doc-checks` |
| `cud-choice` | `doc-choice` |
| `cud-ckyc` | `doc-ckyc` |
| `cud-ckyc-ask__note` | `doc-ckyc-ask__note` |
| `cud-ckyc-btn` | `doc-ckyc-btn` |
| `cud-ckyc-on` | `doc-ckyc-on` |
| `cud-ckyc-pop` | `doc-ckyc-pop` |
| `cud-ckyc__body` | `doc-ckyc__body` |
| `cud-ckyc__foot` | `doc-ckyc__foot` |
| `cud-ckyc__head` | `doc-ckyc__head` |
| `cud-ckyc__mark` | `doc-ckyc__mark` |
| `cud-ckyc__sheet` | `doc-ckyc__sheet` |
| `cud-ckyc__title` | `doc-ckyc__title` |
| `cud-col` | `doc-column` |
| `cud-col__head` | `doc-column__head` |
| `cud-col__spacer` | `doc-column__spacer` |
| `cud-copy` | `doc-copy` |
| `cud-drop` | `doc-drop` |
| `cud-drop__busy` | `doc-drop__busy` |
| `cud-drop__check` | `doc-drop__check` |
| `cud-drop__checking` | `doc-drop__checking` |
| `cud-drop__done` | `doc-drop__done` |
| `cud-drop__file` | `doc-drop__file` |
| `cud-drop__hint` | `doc-drop__hint` |
| `cud-drop__icon` | `doc-drop__icon` |
| `cud-drop__idle` | `doc-drop__idle` |
| `cud-drop__input` | `doc-drop__input` |
| `cud-drop__lock` | `doc-drop__lock` |
| `cud-drop__locked` | `doc-drop__locked` |
| `cud-drop__must` | `doc-drop__must` |
| `cud-drop__size` | `doc-drop__size` |
| `cud-drop__spinner` | `doc-drop__spinner` |
| `cud-drop__step` | `doc-drop__step` |
| `cud-drop__title` | `doc-drop__title` |
| `cud-drop__with` | `doc-drop__with` |
| `cud-extra` | `doc-section` |
| `cud-extra__title` | `doc-section__title` |
| `cud-grid` | `doc-grid` |
| `cud-grid--details` | `doc-grid--details` |
| `cud-grid--docs` | `doc-grid--docs` |
| `cud-head` | `doc-head` |
| `cud-head__ids` | `doc-head__ids` |
| `cud-head__name` | `doc-head__name` |
| `cud-head__pending` | `doc-head__pending` |
| `cud-head__ref` | `doc-head__ref` |
| `cud-head__refs` | `doc-head__refs` |
| `cud-head__right` | `doc-head__right` |
| `cud-head__step` | `doc-head__step` |
| `cud-head__who` | `doc-head__who` |
| `cud-held__expired` | `doc-on-file__expired` |
| `cud-held__from` | `doc-on-file__from` |
| `cud-held__id` | `doc-on-file__id` |
| `cud-held__lines` | `doc-on-file__lines` |
| `cud-held__lines--one` | `doc-on-file__lines--one` |
| `cud-held__pin` | `doc-on-file__pin` |
| `cud-held__tag` | `doc-on-file__tag` |
| `cud-log` | `doc-history` |
| `cud-log__at` | `doc-history__at` |
| `cud-log__body` | `doc-history__body` |
| `cud-log__doc` | `doc-history__doc` |
| `cud-log__dot` | `doc-history__dot` |
| `cud-log__file` | `doc-history__file` |
| `cud-log__item` | `doc-history__item` |
| `cud-log__list` | `doc-history__list` |
| `cud-log__mark` | `doc-history__mark` |
| `cud-log__none` | `doc-history__none` |
| `cud-log__row` | `doc-history__row` |
| `cud-log__stage` | `doc-history__stage` |
| `cud-log__stages` | `doc-history__stages` |
| `cud-log__sub` | `doc-history__sub` |
| `cud-log__top` | `doc-history__top` |
| `cud-log__try` | `doc-history__try` |
| `cud-more` | `doc-more-info` |
| `cud-note` | `doc-note` |
| `cud-optional` | `doc-optional` |
| `cud-page` | `doc-page` |
| `cud-pay` | `doc-payment` |
| `cud-pay__card` | `doc-payment__card` |
| `cud-pay__grid` | `doc-payment__grid` |
| `cud-pay__mode` | `doc-payment__mode` |
| `cud-pay__slot` | `doc-payment__slot` |
| `cud-pin` | `app-sticky-strip` |
| `cud-pin__app` | `app-sticky-strip__app` |
| `cud-pin__inner` | `app-sticky-strip__inner` |
| `cud-pin__label` | `app-sticky-strip__label` |
| `cud-pin__pan` | `app-sticky-strip__pan` |
| `cud-pin__who` | `app-sticky-strip__who` |
| `cud-preview` | `doc-preview` |
| `cud-preview-pop` | `doc-preview-pop` |
| `cud-preview__body` | `doc-preview__body` |
| `cud-preview__close` | `doc-preview__close` |
| `cud-preview__foot` | `doc-preview__foot` |
| `cud-preview__head` | `doc-preview__head` |
| `cud-preview__kind` | `doc-preview__kind` |
| `cud-preview__meta` | `doc-preview__meta` |
| `cud-preview__none` | `doc-preview__none` |
| `cud-preview__sheet` | `doc-preview__sheet` |
| `cud-preview__title` | `doc-preview__title` |
| `cud-read` | `doc-read-result` |
| `cud-read__card` | `doc-read-result__card` |
| `cud-read__cards` | `doc-read-result__cards` |
| `cud-read__from` | `doc-read-result__from` |
| `cud-read__head` | `doc-read-result__head` |
| `cud-read__kind` | `doc-read-result__kind` |
| `cud-read__lines` | `doc-read-result__lines` |
| `cud-read__state` | `doc-read-result__state` |
| `cud-read__sub` | `doc-read-result__sub` |
| `cud-read__title` | `doc-read-result__title` |
| `cud-read__was` | `doc-read-result__was` |
| `cud-refresh` | `doc-refresh` |
| `cud-resolved` | `doc-resolved` |
| `cud-resolved__none` | `doc-resolved__none` |
| `cud-retry` | `doc-retry` |
| `cud-retry-row` | `doc-retry-row` |
| `cud-retry-row--end` | `doc-retry-row--end` |
| `cud-retry-row--pan-second` | `doc-retry-row--pan-second` |
| `cud-retry__ask` | `doc-retry__ask` |
| `cud-retry__error` | `doc-retry__error` |
| `cud-retry__say` | `doc-retry__say` |
| `cud-retry__text` | `doc-retry__text` |
| `cud-retry__title` | `doc-retry__title` |
| `cud-route` | `doc-kyc-route` |
| `cud-route__act` | `doc-kyc-route__actions` |
| `cud-route__p` | `doc-kyc-route__p` |
| `cud-route__text` | `doc-kyc-route__text` |
| `cud-route__title` | `doc-kyc-route__title` |
| `cud-select` | `doc-select` |
| `cud-sheet` | `doc-sheet` |
| `cud-slot` | `doc-slot` |
| `cud-slot__bar` | `doc-slot__bar` |
| `cud-slot__check` | `doc-slot__check` |
| `cud-slot__frame` | `doc-slot__frame` |
| `cud-slot__head` | `doc-slot__head` |
| `cud-slot__na` | `doc-slot__na` |
| `cud-slot__na-mark` | `doc-slot__na-mark` |
| `cud-slot__na-text` | `doc-slot__na-text` |
| `cud-slot__na-title` | `doc-slot__na-title` |
| `cud-slot__na-why` | `doc-slot__na-why` |
| `cud-slot__notes` | `doc-slot__notes` |
| `cud-slot__tools` | `doc-slot__tools` |
| `cud-slot__tries` | `doc-slot__tries` |
| `cud-spin` | `doc-spin` |
| `cud-tool` | `doc-tool` |
| `cud-tool--final` | `doc-tool--final` |
| `cud-type` | `doc-type` |
| `cud-upload` | `doc-upload` |
| `cud-why` | `doc-refusal-link` |
| `fd-amount` | `deposit-amount` |
| `fd-amount--error` | `deposit-amount--error` |
| `fd-amount-hint` | `deposit-amount-hint` |
| `fd-amountError` | `deposit-amountError` |
| `fd-amount__clear` | `deposit-amount__clear` |
| `fd-amount__currency` | `deposit-amount__currency` |
| `fd-amount__input` | `deposit-amount__input` |
| `fd-aside` | `deposit-aside` |
| `fd-delivery` | `deposit-delivery` |
| `fd-grid-2` | `deposit-grid-2` |
| `fd-group` | `deposit-group` |
| `fd-hint` | `deposit-hint` |
| `fd-hint--error` | `deposit-hint--error` |
| `fd-kv__k` | `deposit-key-value__key` |
| `fd-kv__v` | `deposit-key-value__value` |
| `fd-kv__v--rate` | `deposit-key-value__value--rate` |
| `fd-label` | `deposit-label` |
| `fd-layout` | `deposit-layout` |
| `fd-main` | `deposit-main` |
| `fd-no-tds` | `deposit-no-tds` |
| `fd-note` | `deposit-note` |
| `fd-note__action` | `deposit-note__action` |
| `fd-note__text` | `deposit-note__text` |
| `fd-option` | `deposit-option` |
| `fd-option__face` | `deposit-option__face` |
| `fd-options` | `deposit-options` |
| `fd-options--payout` | `deposit-options--payout` |
| `fd-options--tenure` | `deposit-options--tenure` |
| `fd-payout` | `deposit-payout` |
| `fd-payout-` | `deposit-payout-` |
| `fd-payout-label` | `deposit-payout-label` |
| `fd-quote` | `deposit-quote` |
| `fd-radios` | `deposit-radios` |
| `fd-renew` | `deposit-renew` |
| `fd-renew-for` | `deposit-renew-for` |
| `fd-renew-on` | `deposit-renew-on` |
| `fd-section` | `deposit-section` |
| `fd-section--tight` | `deposit-section--tight` |
| `fd-summary` | `deposit-summary` |
| `fd-summary__amount` | `deposit-summary__amount` |
| `fd-summary__block` | `deposit-summary__block` |
| `fd-summary__grid` | `deposit-summary__grid` |
| `fd-summary__label` | `deposit-summary__label` |
| `fd-summary__note` | `deposit-summary__note` |
| `fd-tds-form` | `deposit-tds-form` |
| `fd-tenure` | `deposit-tenure` |
| `fd-tenure-` | `deposit-tenure-` |
| `fd-tenure-label` | `deposit-tenure-label` |
| `fd-toggle` | `deposit-toggle` |
| `fd-toggle__desc` | `deposit-toggle__desc` |
| `fd-toggle__text` | `deposit-toggle__text` |
| `fd-toggle__title` | `deposit-toggle__title` |
| `inv-ckyc` | `investor-ckyc` |
| `inv-ckyc__photo` | `investor-ckyc__photo` |
| `inv-ckyc__photo--img` | `investor-ckyc__photo--img` |
| `inv-control` | `investor-control` |
| `inv-control--error` | `investor-control--error` |
| `inv-control--strong` | `investor-control--strong` |
| `inv-field` | `investor-field` |
| `inv-field--optional` | `investor-field--optional` |
| `inv-field--sm` | `investor-field--sm` |
| `inv-field__hint` | `investor-field__hint` |
| `inv-field__hint--error` | `investor-field__hint--error` |
| `inv-grid` | `investor-grid` |
| `inv-kv` | `investor-key-value` |
| `inv-kv--wide` | `investor-key-value--wide` |
| `inv-kv-grid` | `investor-key-value-grid` |
| `inv-kv-grid--divided` | `investor-key-value-grid--divided` |
| `inv-kv__k` | `investor-key-value__key` |
| `inv-kv__note` | `investor-key-value__note` |
| `inv-kv__v` | `investor-key-value__value` |
| `inv-kv__v--unread` | `investor-key-value__value--unread` |
| `inv-manual` | `investor-manual` |
| `inv-manual__badge` | `investor-manual__badge` |
| `inv-manual__text` | `investor-manual__text` |
| `inv-manual__title` | `investor-manual__title` |
| `inv-panel` | `investor-panel` |
| `inv-panel--stack` | `investor-panel--stack` |
| `inv-panel__action` | `investor-panel__action` |
| `inv-panel__label` | `investor-panel__label` |
| `inv-panel__note` | `investor-panel__note` |
| `inv-panel__part` | `investor-panel__part` |
| `inv-remove` | `investor-remove` |
| `inv-section-head` | `investor-section-head` |
| `inv-switch` | `investor-switch` |
| `inv-switch-row` | `investor-switch-row` |
| `inv-title` | `investor-title` |
| `kv` | `key-value` |
| `kv-grid` | `key-value-grid` |
| `kv-grid--2` | `key-value-grid--2` |
| `kv-grid--4` | `key-value-grid--4` |
| `pis-btn--search` | `payin-slip-btn--search` |
| `pis-field--actions` | `payin-slip-field--actions` |
| `pis-field--appno` | `payin-slip-field--appno` |
| `pis-flag` | `payin-slip-flag` |
| `pis-form` | `payin-slip-form` |
| `pis-form__row` | `payin-slip-form__row` |
| `pis-form__row--search` | `payin-slip-form__row--search` |
| `pis-table` | `payin-slip-table` |
| `pis-table__amt` | `payin-slip-table__amount` |
| `pis-table__slip` | `payin-slip-table__slip` |
| `pl-actions` | `pay-link-actions` |
| `pl-actions__later` | `pay-link-actions__later` |
| `pl-actions__send` | `pay-link-actions__send` |
| `pl-body` | `pay-link-body` |
| `pl-dialog` | `pay-link-dialog` |
| `pl-foot` | `pay-link-foot` |
| `pl-head` | `pay-link-head` |
| `pl-head__icon` | `pay-link-head__icon` |
| `pl-head__title` | `pay-link-head__title` |
| `pl-intro` | `pay-link-intro` |
| `pl-intro--long` | `pay-link-intro--long` |
| `pl-intro--short` | `pay-link-intro--short` |
| `pl-lapse` | `pay-link-lapse` |
| `pl-lapse--long` | `pay-link-lapse--long` |
| `pl-lapse--short` | `pay-link-lapse--short` |
| `pl-overlay` | `pay-link-overlay` |
| `pl-terms` | `pay-link-terms` |
| `pl-terms__email` | `pay-link-terms__email` |
| `pl-terms__note` | `pay-link-terms__note` |
| `pub-nav` | `public-nav` |
| `pub-nav__link` | `public-nav__link` |
| `pub-nav__link--on` | `public-nav__link--on` |
| `pub-nav__login` | `public-nav__login` |
| `rdocs` | `required-docs` |
| `rdocs__body` | `required-docs__body` |
| `rdocs__callout` | `required-docs__callout` |
| `rdocs__list` | `required-docs__list` |
| `rdocs__panel` | `required-docs__panel` |
| `rdocs__panel-title` | `required-docs__panel-title` |
| `rdocs__type` | `required-docs__type` |
| `rdocs__type-count` | `required-docs__type-count` |
| `rdocs__type-name` | `required-docs__type-name` |
| `rdocs__types` | `required-docs__types` |
| `rv-bank` | `review-bank` |
| `rv-check` | `review-check` |
| `rv-check__body` | `review-check__body` |
| `rv-check__detail` | `review-check__detail` |
| `rv-check__mark` | `review-check__mark` |
| `rv-check__mark--na` | `review-check__mark--na` |
| `rv-check__title` | `review-check__title` |
| `rv-checks` | `review-checks` |
| `rv-declaration` | `review-declaration` |
| `rv-declaration--unsigned` | `review-declaration--unsigned` |
| `rv-declaration__box` | `review-declaration__box` |
| `rv-declaration__text` | `review-declaration__text` |
| `rv-declarations` | `review-declarations` |
| `rv-deposit` | `review-deposit` |
| `rv-documents` | `review-documents` |
| `rv-edit` | `review-edit` |
| `rv-footer-hint` | `review-footer-hint` |
| `rv-holder` | `review-holder` |
| `rv-holder__bar` | `review-holder__bar` |
| `rv-holder__col` | `review-holder__col` |
| `rv-holder__col--flag` | `review-holder__col--flag` |
| `rv-holder__col--poa` | `review-holder__col--poa` |
| `rv-holder__col-head` | `review-holder__col-head` |
| `rv-holder__col-label` | `review-holder__col-label` |
| `rv-holder__consent` | `review-holder__consent` |
| `rv-holder__count` | `review-holder__count` |
| `rv-holder__count-label` | `review-holder__count-label` |
| `rv-holder__id` | `review-holder__id` |
| `rv-holder__id-body` | `review-holder__id-body` |
| `rv-holder__meta` | `review-holder__meta` |
| `rv-holder__name` | `review-holder__name` |
| `rv-holder__name-row` | `review-holder__name-row` |
| `rv-holder__photo` | `review-holder__photo` |
| `rv-holder__poa-note` | `review-holder__poa-note` |
| `rv-holders` | `review-holders` |
| `rv-nominee` | `review-nominee` |
| `rv-other` | `review-other` |
| `seg-toggle` | `choice-toggle` |
| `seg-toggle--sm` | `choice-toggle--sm` |
| `seg-toggle__opt` | `choice-toggle__option` |
| `seg-toggle__opt--active` | `choice-toggle__option--active` |
| `su-empty` | `register-empty` |
| `su-filters` | `register-filters` |
| `su-nolink` | `register-no-link` |
| `su-notice` | `register-notice` |
| `su-notice__icon` | `register-notice__icon` |
| `su-notice__title` | `register-notice__title` |
| `su-page` | `register-page` |
| `su-page--on` | `register-page--on` |
| `su-page--step` | `register-page--step` |
| `su-pager` | `register-pager` |
| `su-pager__count` | `register-pager__count` |
| `su-pager__nav` | `register-pager__nav` |
| `su-pager__pages` | `register-pager__pages` |
| `su-pill` | `register-pill` |
| `su-pill--on` | `register-pill--on` |
| `su-pill__n` | `register-pill__number` |
| `su-pills` | `register-pills` |
| `su-search` | `register-search` |
| `su-table` | `register-table` |
| `su-table__act` | `register-table__actions` |
| `su-table__sub` | `register-table__sub` |
| `su-table__sub--urgent` | `register-table__sub--urgent` |
| `sub-actions` | `submitted-actions` |
| `sub-actions__link` | `submitted-actions__link` |
| `sub-actions__link--plain` | `submitted-actions__link--plain` |
| `sub-actions__primary` | `submitted-actions__primary` |
| `sub-box` | `submitted-box` |
| `sub-box__label` | `submitted-box__label` |
| `sub-card` | `submitted-card` |
| `sub-card--padded` | `submitted-card--padded` |
| `sub-card__action` | `submitted-card__action` |
| `sub-card__head` | `submitted-card__head` |
| `sub-card__title` | `submitted-card__title` |
| `sub-holders` | `submitted-holders` |
| `sub-holders__label` | `submitted-holders__label` |
| `sub-holders__names` | `submitted-holders__names` |
| `sub-holders__role` | `submitted-holders__role` |
| `sub-holders__text` | `submitted-holders__text` |
| `sub-kv-grid` | `submitted-key-value-grid` |
| `sub-kv__k` | `submitted-key-value__key` |
| `sub-kv__note` | `submitted-key-value__note` |
| `sub-kv__v` | `submitted-key-value__value` |
| `sub-kv__v--green` | `submitted-key-value__value--green` |
| `sub-kv__v--lg` | `submitted-key-value__value--lg` |
| `sub-main` | `submitted-main` |
| `sub-meta` | `submitted-meta` |
| `sub-note` | `submitted-note` |
| `sub-page` | `submitted-page` |
| `sub-pending` | `submitted-pending` |
| `sub-pending__head` | `submitted-pending__head` |
| `sub-pending__tag` | `submitted-pending__tag` |
| `sub-pending__text` | `submitted-pending__text` |
| `sub-pending__title` | `submitted-pending__title` |
| `sub-side` | `submitted-side` |
| `sub-step` | `submitted-step` |
| `sub-step--current` | `submitted-step--current` |
| `sub-step__num` | `submitted-step__num` |
| `sub-step__text` | `submitted-step__text` |
| `sub-steps` | `submitted-steps` |
| `sub-tick` | `submitted-tick` |
| `sub-tile` | `submitted-tile` |
| `sub-tile__text` | `submitted-tile__text` |
| `sub-tile__title` | `submitted-tile__title` |
| `sub-tiles` | `submitted-tiles` |
| `sub-title` | `submitted-title` |
| `sub-title-row` | `submitted-title-row` |
| `tip` | `hover-hint` |
| `tip--info` | `hover-hint--info` |
| `tip--mark` | `hover-hint--mark` |
| `va-details` | `view-app-details` |
| `va-details__wait` | `view-app-details__wait` |
| `va-flag` | `view-app-scope-note` |
| `va-kv` | `view-app-key-value` |
| `va-kv__k` | `view-app-key-value__key` |
| `va-kv__note` | `view-app-key-value__note` |
| `va-kv__v` | `view-app-key-value__value` |
| `va-kv__v--lg` | `view-app-key-value__value--lg` |
| `va-part` | `view-app-section` |
| `va-sheet` | `view-app-sheet` |
| `va-sheet__body` | `view-app-sheet__body` |
| `va-sheet__grid` | `view-app-sheet__grid` |
| `va-sheet__head` | `view-app-sheet__head` |
| `va-step` | `view-app-step` |
| `va-step--done` | `view-app-step--done` |
| `va-step--failed` | `view-app-step--failed` |
| `va-step--na` | `view-app-step--na` |
| `va-step--pending` | `view-app-step--pending` |
| `va-step__label` | `view-app-step__label` |
| `va-step__note` | `view-app-step__note` |
| `va-step__text` | `view-app-step__text` |
| `va-step__when` | `view-app-step__when` |
| `va-steps` | `view-app-steps` |
| `va-table` | `view-app-table` |

## camelCase element ids and data attributes (130)

| Old | New |
|---|---|
| `admAt` | `adminAt` |
| `admAtLabel` | `adminAtLabel` |
| `admAtValue` | `adminAtValue` |
| `admFrom` | `adminFrom` |
| `admFromLabel` | `adminFromLabel` |
| `admFromValue` | `adminFromValue` |
| `admNoticeDetail` | `adminNoticeDetail` |
| `admNoticeError` | `adminNoticeError` |
| `admNoticeForm` | `adminNoticeForm` |
| `admNoticeHead` | `adminNoticeHead` |
| `admNoticeReset` | `adminNoticeReset` |
| `admNoticeTitle` | `adminNoticeTitle` |
| `admNotices` | `adminNotices` |
| `admNoticesEmpty` | `adminNoticesEmpty` |
| `admPicks` | `adminPicks` |
| `admRows` | `adminRows` |
| `admScheduleTitle` | `adminScheduleTitle` |
| `admSummary` | `adminSummary` |
| `admTitle` | `adminTitle` |
| `admTo` | `adminTo` |
| `admToLabel` | `adminToLabel` |
| `admToValue` | `adminToValue` |
| `admWindowError` | `adminWindowError` |
| `admWindowForm` | `adminWindowForm` |
| `admWindowReset` | `adminWindowReset` |
| `ciiAddHolder` | `investorAddHolder` |
| `ciiAddNominee` | `investorAddNominee` |
| `ciiClose` | `investorClose` |
| `ciiFatcaAlert` | `investorFatcaAlert` |
| `ciiGdn1` | `investorGuardian1` |
| `ciiGdn2` | `investorGuardian2` |
| `ciiGdn3` | `investorGuardian3` |
| `ciiGdnCity` | `investorGuardianCity` |
| `ciiGdnPin` | `investorGuardianPin` |
| `ciiGuardian` | `investorGuardian` |
| `ciiLog` | `investorLog` |
| `ciiNomDd` | `investorNomineeDd` |
| `ciiNomDdError` | `investorNomineeDdError` |
| `ciiNomDobLabel` | `investorNomineeDobLabel` |
| `ciiNomGuardian` | `investorNomineeGuardian` |
| `ciiNomName` | `investorNomineeName` |
| `ciiNomRelation` | `investorNomineeRelation` |
| `ciiNomineeAsk` | `investorNomineeAsk` |
| `ciiNomineeAskTitle` | `investorNomineeAskTitle` |
| `ciiNomineeSkipped` | `investorNomineeSkipped` |
| `cii{n}Income (and the other per-holder ids)` | `investor{n}Income` |
| `csiClearForm` | `pageClearForm` |
| `csiProceedForm` | `pageProceedForm` |
| `cudAppType` | `docsAppType` |
| `cudAppTypeLabel` | `docsAppTypeLabel` |
| `cudCategory` | `docsCategory` |
| `cudCkycAsk` | `docsCkycAsk` |
| `cudCkycTitle` | `docsCkycTitle` |
| `cudEmpCode` | `docsEmployeeCode` |
| `cudEmpCodeSuggest` | `docsEmployeeCodeSuggest` |
| `cudEmpCompany` | `docsEmployeeCompany` |
| `cudEmpHolder` | `docsEmployeeHolder` |
| `cudEmpProofType` | `docsEmployeeProofType` |
| `cudEmpRelation` | `docsEmployeeRelation` |
| `cudEmpRelationFixed` | `docsEmployeeRelationFixed` |
| `cudEmpRelationWhy` | `docsEmployeeRelationWhy` |
| `cudForm` | `docsForm` |
| `cudFormNo` | `docsFormNo` |
| `cudMailType` | `docsMailType` |
| `cudMailing` | `docsMailing` |
| `cudMailingLabel` | `docsMailingLabel` |
| `cudPayMode` | `docsPayMode` |
| `cudPayment` | `docsPayment` |
| `cudPin` | `docsPin` |
| `cudPoaType` | `docsPoaType` |
| `cudRefresh` | `docsRefresh` |
| `cudRoute` | `docsRoute` |
| `cudSourceCode` | `docsSourceCode` |
| `cudSourceCodeSuggest` | `docsSourceCodeSuggest` |
| `cudSourcing` | `docsSourcing` |
| `cudSubBroker` | `docsSubBroker` |
| `cudSubBrokerSuggest` | `docsSubBrokerSuggest` |
| `data-cii-close` | `data-investor-close` |
| `data-cii-guardian` | `data-investor-guardian` |
| `data-fd-amount` | `data-deposit-amount` |
| `fdConfigForm` | `depositConfigForm` |
| `fdRefresh` | `depositRefresh` |
| `pisAppNo` | `payinSlipAppNo` |
| `pisClear` | `payinSlipClear` |
| `pisCount` | `payinSlipCount` |
| `pisEmpty` | `payinSlipEmpty` |
| `pisError` | `payinSlipError` |
| `pisForm` | `payinSlipForm` |
| `pisFrom` | `payinSlipFrom` |
| `pisFromLabel` | `payinSlipFromLabel` |
| `pisNext` | `payinSlipNext` |
| `pisPages` | `payinSlipPages` |
| `pisPrev` | `payinSlipPrev` |
| `pisRows` | `payinSlipRows` |
| `pisScope` | `payinSlipScope` |
| `pisSearchBy` | `payinSlipSearchBy` |
| `pisTo` | `payinSlipTo` |
| `pisToLabel` | `payinSlipToLabel` |
| `rdocsType (radio name)` | `requiredDocsType` |
| `suCount` | `registerCount` |
| `suEmpty` | `registerEmpty` |
| `suNext` | `registerNext` |
| `suPages` | `registerPages` |
| `suPrev` | `registerPrev` |
| `suRows` | `registerRows` |
| `suSearch` | `registerSearch` |
| `vaAppNo` | `viewAppAppNo` |
| `vaClear` | `viewAppClear` |
| `vaCount` | `viewAppCount` |
| `vaEmpty` | `viewAppEmpty` |
| `vaError` | `viewAppError` |
| `vaFilter` | `viewAppFilter` |
| `vaFolio` | `viewAppFolio` |
| `vaForm` | `viewAppForm` |
| `vaFrom` | `viewAppFrom` |
| `vaFromLabel` | `viewAppFromLabel` |
| `vaModal` | `viewAppModal` |
| `vaModalBody` | `viewAppModalBody` |
| `vaModalClose` | `viewAppModalClose` |
| `vaModalSub` | `viewAppModalSub` |
| `vaModalTitle` | `viewAppModalTitle` |
| `vaNext` | `viewAppNext` |
| `vaPages` | `viewAppPages` |
| `vaPrev` | `viewAppPrev` |
| `vaRows` | `viewAppRows` |
| `vaScope` | `viewAppScope` |
| `vaSearchBy` | `viewAppSearchBy` |
| `vaSheets` | `viewAppSheets` |
| `vaTo` | `viewAppTo` |
| `vaToLabel` | `viewAppToLabel` |

## JavaScript functions

| File | Old | New |
|---|---|---|
| `pages/admin.js` | `two` | `padToTwoDigits` |
| `pages/admin.js` | `local` | `toServerDateTime` |
| `pages/admin.js` | `readWhen` | `readDateTimeBoxes` |
| `pages/admin.js` | `markWhen` | `markDateTimeInvalid` |
| `pages/admin.js` | `syncNotices` | `showNoNoticesMessage` |
| `pages/admin.js` | `wireRow` | `wireTileRow` |
| `pages/admin.js` | `syncPicks` | `disableTilesAlreadyInWindow` |
| `pages/admin.js` | `fail` | `showFormError` |
| `pages/applications.js` | `read` | `readDateBoxes` |
| `pages/applications.js` | `pad` | `padToTwoDigits` |
| `pages/applications.js` | `show` | `isoToDisplayDate` |
| `pages/applications.js` | `write` | `writeDateBoxes` |
| `pages/applications.js` | `days` | `daysBetweenInclusive` |
| `pages/applications.js` | `syncMode` | `showFieldsForSearchMode` |
| `pages/applications.js` | `fail` | `showSearchError` |
| `pages/applications.js` | `run` | `runSearch` |
| `pages/applications.js` | `found` | `isReachedBySearch` |
| `pages/applications.js` | `matches` | `matchesStatusAndFilter` |
| `pages/applications.js` | `setState` | `setStatusFilter` |
| `pages/applications.js` | `recount` | `updateStatusCounts` |
| `pages/applications.js` | `render` | `renderTablePage` |
| `pages/applications.js` | `open` | `openApplicationSheet` |
| `pages/applications.js` | `details` | `loadApplicationDetails` |
| `pages/applications.js` | `close` | `closeApplicationSheet` |
| `pages/deposit.js` | `quote` | `refreshQuote` |
| `pages/deposit.js` | `amount` | `showAmountMessage` |
| `pages/deposit.js` | `renewFor` | `showRenewSameTenure` |
| `pages/documents.js` | `listFor` | `suggestionListFor` |
| `pages/documents.js` | `options` | `suggestionOptions` |
| `pages/documents.js` | `close` | `closeSuggestions` |
| `pages/documents.js` | `name` | `showPickedName` |
| `pages/documents.js` | `pick` | `pickSuggestion` |
| `pages/documents.js` | `show` | `showSuggestions` |
| `pages/documents.js` | `search` | `searchRegister` |
| `pages/documents.js` | `move` | `moveSuggestionHighlight` |
| `pages/investor.js` | `show` | `showDistrictAndState` |
| `pages/investor.js` | `place` | `lookUpPinCode` |
| `pages/investor.js` | `part` | `nomineeDobPart` |
| `pages/links.js` | `matches` | `matchesStatusAndSearch` |
| `pages/links.js` | `render` | `renderTablePage` |
| `pages/pay-in-slips.js` | `read` | `readDateBoxes` |
| `pages/pay-in-slips.js` | `pad` | `padToTwoDigits` |
| `pages/pay-in-slips.js` | `show` | `isoToDisplayDate` |
| `pages/pay-in-slips.js` | `write` | `writeDateBoxes` |
| `pages/pay-in-slips.js` | `syncMode` | `showFieldsForSearchMode` |
| `pages/pay-in-slips.js` | `fail` | `showSearchError` |
| `pages/pay-in-slips.js` | `search` | `runSearch` |
| `pages/pay-in-slips.js` | `days` | `daysBetweenInclusive` |
| `pages/pay-in-slips.js` | `matches` | `matchesSearch` |
| `pages/pay-in-slips.js` | `render` | `renderTablePage` |
| `pages/payment.js` | `listFor` | `suggestionListFor` |
| `pages/payment.js` | `options` | `suggestionOptions` |
| `pages/payment.js` | `close` | `closeSuggestions` |
| `pages/payment.js` | `find` | `findBankByIfsc` |
| `pages/payment.js` | `pick` | `pickBranch` |
| `pages/payment.js` | `show` | `showBranchSuggestions` |
| `pages/payment.js` | `search` | `searchBanks` |
| `pages/payment.js` | `move` | `moveSuggestionHighlight` |
| `pages/review.js` | `sync` | `enableSubmitWhenReady` |
| `pages/review.js` | `openDialog` | `openPaymentLinkDialog` |
| `pages/review.js` | `closeDialog` | `closePaymentLinkDialog` |
| `shared/checks.js` | `valueOf` | `readFieldValue` |
| `shared/checks.js` | `realDate` | `toRealDate` |
| `shared/checks.js` | `problem` | `findProblem` |
| `shared/checks.js` | `checkProblem` | `findShapeProblem` |
| `shared/checks.js` | `errorOf` | `errorMessageFor` |
| `shared/checks.js` | `mark` | `showFieldError` |
| `shared/checks.js` | `checked` | `hasChecks` |
| `shared/checks.js` | `inPlay` | `isVisibleAndEnabled` |
| `shared/checks.js` | `holder` | `checkedFieldOf` |
| `shared/date-parts.js` | `parts` | `datePartBoxes` |
| `shared/date-parts.js` | `boxOf` | `dateInputOf` |
| `shared/pin.js` | `pin` | `watchStickyStrip` |
| `shared/notices.js` | `open` | `setNoticesOpen` |
| `shared/topbar.js` | `open` | `setMenuOpen` |
| `shared/image-shrink.js` | `shrink` | `shrinkImage` |
| `shared/image-shrink.js` | `decode` | `decodeUpright` |
| `shared/image-shrink.js` | `viaImage` | `decodeWithImageElement` |
| `shared/partial-forms.js` | `holds` | `ruleHolds` |
| `shared/partial-forms.js` | `guarded` | `isInGuardedForm` |
| `shared/partial-forms.js` | `restore` | `restoreDefaultValue` |
| `shared/partial-forms.js` | `fallback` | `postTheOrdinaryWay` |
| `shared/partial-forms.js` | `openTarget` | `openLinkedHistoryEntry` |
| `shared/partial-forms.js` | `typedNow` | `currentTextValues` |
| `shared/partial-forms.js` | `toast` | `showToastMessage` |
| `shared/partial-forms.js` | `go` | `sendAndSwap` |
| `shared/partial-forms.js` | `filesIn` | `totalFileSize` |
| `shared/partial-forms.js` | `size` | `formatFileSize` |
| `shared/partial-forms.js` | `send` | `sendWithProgress` |
| `shared/partial-forms.js` | `withoutSpot` | `urlWithoutFragment` |
| `shared/partial-forms.js` | `fieldAfter` | `nextFocusableField` |
| `shared/partial-forms.js` | `anchorIn` | `elementToHoldInPlace` |
| `shared/partial-forms.js` | `swap` | `swapInMain` |

## C# methods

| Where | Old | New |
|---|---|---|
| ApiClient | `Seg` | `EscapePathSegment` |
| ApiClient | `Body` | `JsonBody` |
| SqlErrorLog | `Cut` | `Truncate` |
| SqlReference | `Bool / Int / Str / Strs` | `ReadBool / ReadInt / ReadString / ReadStrings` |
| SqlMasters | `Like` | `EscapeLikePattern` |
| LinksViewModel | `Ago / Left` | `HoursAgoText / HoursLeftText` |
| NewApplicationViewModel | `Pad` | `PadToTwoDigits` |
| ReviewViewModel, DocumentsViewModel | `Cap` | `Capitalize` |
| DocumentsViewModel | `Say` | `FlashMessages` |
| DocumentsViewModel | `Drop` | `TakeOffApplication` |
| DocumentsViewModel | `Mask` | `MaskPan` |
| AppUrls | `Base` | `BaseUrlOf` |
| Lookups | `Kept` | `CachedOrAsk` |
| AdminController | `When` | `ParseLocalDateTime` |
| Views/Admin/Index.cshtml | `When` | `DateTimeBoxes` |
| DashboardController | `Try / Skip` | `OrEmptyWhenUnavailable / Empty` |
| InvestorController | `Keep / Drop` | `KeepTypedFields / DropFieldsStartingWith` |
| MockApplications | `New` | `NewApplication` |
| Dates (UnoTP.Data) | `ToDb` | `ParseDdMmYyyy` |
| DmsPaths | `Safe` | `SafePathSegment` |
| IdfyClient | `Date` | `IsoDate` |
| IdfyServices | `Dob` | `ParseDob` |
