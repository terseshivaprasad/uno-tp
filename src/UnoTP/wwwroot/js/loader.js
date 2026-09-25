// The screen the app puts up while it is waiting on something, in one place so
// every step says it the same way. It is deliberately in the way: a partner who
// cannot see whether a press landed presses again, and on this application that
// means a second upload or a second application.
(function () {
  var box = document.getElementById('appLoader');
  if (!box) return;
  var what = document.getElementById('appLoaderWhat');
  var hint = document.getElementById('appLoaderHint');

  function showLoader(words, note) {
    what.textContent = words || 'Working…';
    hint.textContent = note || '';
    hint.hidden = !note;
    box.hidden = false;
    document.body.classList.add('is-waiting');
  }

  function hideLoader() {
    box.hidden = true;
    hint.hidden = true;
    document.body.classList.remove('is-waiting');
  }

  window.showLoader = showLoader;
  window.hideLoader = hideLoader;

  // Anything that leaves the page can say so by carrying the words itself: the
  // wait belongs to the press, not to the script of whatever page it is on. One
  // listener for the whole document, so what a form redraws in place says it too.
  // A form that posts in place (partial-forms.js) puts its own wait up.
  document.addEventListener('click', function (e) {
    var el = e.target.closest && e.target.closest('[data-loader]');
    if (!el || el.disabled) return;
    var form = el.form || (el.closest && el.closest('form'));
    if (form && form.hasAttribute('data-partial') && document.documentElement.classList.contains('has-js')) return;
    if (el.matches('a[data-partial]')) return;
    showLoader(el.dataset.loader, el.dataset.loaderHint || '');
  });

  // Coming back through the browser's own Back lands on the page as it was left,
  // loader and all, because the page was never torn down. Whatever it was
  // waiting for is long finished by then.
  window.addEventListener('pageshow', function (e) {
    if (e.persisted) hideLoader();
  });
})();
