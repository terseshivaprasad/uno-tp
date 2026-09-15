// Client-side step switching for the consolidated Holder Identification page
// (mockup screens 02, 02A, 02B, 03, 04, 05, 06). Mirrors the mockup's own
// doc-page.js/support.js pattern of toggling visibility of state blocks
// rather than navigating to a new URL for every intermediate screen.
(function () {
  var sections = document.querySelectorAll('.hid-section');
  var pills = document.querySelectorAll('.hid-step-pill');
  var stepField = document.getElementById('hidStepField');

  function showStep(step) {
    sections.forEach(function (s) {
      s.hidden = s.getAttribute('data-step') !== step;
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
})();
