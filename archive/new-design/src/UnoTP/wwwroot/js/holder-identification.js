// Client-side step switching for the consolidated Holder Identification page
// (mockup screens 02, 02A, 02B, 03, 04, 05, 06). Mirrors the mockup's own
// doc-page.js/support.js pattern of toggling visibility of state blocks
// rather than navigating to a new URL for every intermediate screen.
(function () {
  var sections = document.querySelectorAll('.hid-section');
  var footers = document.querySelectorAll('.hid-footer');
  var pills = document.querySelectorAll('.hid-step-pill');
  var stepField = document.getElementById('hidStepField');

  function showStep(step) {
    var phoneTitle = document.getElementById('wizardPhoneTitle');
    sections.forEach(function (s) {
      s.hidden = s.getAttribute('data-step') !== step;
      // The phone back bar names the sub-step, as Boards 02-06 - Mobile do.
      if (!s.hidden && phoneTitle && s.getAttribute('data-phone-title')) {
        phoneTitle.textContent = s.getAttribute('data-phone-title');
      }
    });
    footers.forEach(function (f) {
      f.hidden = f.getAttribute('data-step') !== step;
    });
    pills.forEach(function (p) {
      p.classList.toggle('hid-step-pill--active', p.getAttribute('data-goto') === step);
    });
    if (stepField) stepField.value = step;
    window.scrollTo({ top: 0, behavior: 'instant' in window ? 'instant' : 'auto' });
  }

  document.querySelectorAll('[data-goto]').forEach(function (el) {
    el.addEventListener('click', function () {
      showStep(el.getAttribute('data-goto'));
    });
  });

  var initial = (stepField && stepField.value) || 'search';
  showStep(initial);

  // "Search By" toggle on the search step: swap PAN+DOB fields for a single Folio field.
  var searchByOpts = document.querySelectorAll('.hid-searchby-opt');
  var searchByFields = document.querySelectorAll('.hid-searchby-fields');
  var searchByHints = document.querySelectorAll('.hid-searchby-hint');
  searchByOpts.forEach(function (opt) {
    opt.addEventListener('click', function () {
      var mode = opt.getAttribute('data-searchby');
      searchByOpts.forEach(function (o) {
        var active = o === opt;
        o.classList.toggle('hid-searchby-opt--active', active);
        var dot = o.querySelector('.radio-toggle__dot');
        if (dot) dot.classList.toggle('radio-toggle__dot--checked', active);
      });
      searchByFields.forEach(function (f) {
        f.hidden = f.getAttribute('data-searchby') !== mode;
      });
      searchByHints.forEach(function (h) {
        h.hidden = h.getAttribute('data-searchby') !== mode;
      });
    });
  });

  // Offline/Digital consent toggle on the consent step: both comparison panels stay
  // visible (matching the prototype), the toggle just changes which is "selected".
  document.querySelectorAll('.seg-toggle[id]').forEach(function (toggle) {
    // The offline summary depends on how many consents are switched on, so the
    // server renders both strings onto the toggle rather than the script guessing.
    var consentDetail = {
      offline: toggle.getAttribute('data-offline-detail') || '',
      digital: toggle.getAttribute('data-digital-detail') || ''
    };
    var opts = toggle.querySelectorAll('.seg-toggle__opt');
    var scope = toggle.closest('.hid-section') || document;
    var panels = scope.querySelectorAll('.hid-consent-panel');
    var modeLabel = scope.querySelector('.hid-consent-mode');
    var detailLabel = scope.querySelector('.hid-consent-detail');
    opts.forEach(function (opt) {
      opt.addEventListener('click', function () {
        var mode = opt.getAttribute('data-consent');
        opts.forEach(function (o) {
          o.classList.toggle('seg-toggle__opt--active', o === opt);
        });
        panels.forEach(function (panel) {
          var selected = panel.getAttribute('data-consent') === mode;
          panel.classList.toggle('hid-consent-panel--selected', selected);
          var status = panel.querySelector('.hid-consent-panel__status');
          if (status) status.textContent = selected ? '· selected for this holder' : '· not selected';
        });
        if (modeLabel) modeLabel.textContent = mode === 'offline' ? 'Offline' : 'Digital';
        if (detailLabel) detailLabel.textContent = consentDetail[mode];
      });
    });
  });
})();
