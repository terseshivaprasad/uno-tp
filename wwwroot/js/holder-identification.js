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
    sections.forEach(function (s) {
      s.hidden = s.getAttribute('data-step') !== step;
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
})();
