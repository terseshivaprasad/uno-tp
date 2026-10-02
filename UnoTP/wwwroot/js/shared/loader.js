// The screen the app puts up while it is waiting on something, in one place so
// every step says it the same way. It is deliberately in the way: a partner who
// cannot see whether a press landed presses again, and on this application that
// means a second upload or a second application.
(function () {
  var box = document.getElementById('appLoader');
  if (!box) return;
  var what = document.getElementById('appLoaderWhat');
  var hint = document.getElementById('appLoaderHint');

  // Puts up the full-screen wait with what is under way.
  function showLoader(words, note) {
    what.textContent = words || 'Working…';
    hint.textContent = note || '';
    hint.hidden = !note;
    box.hidden = false;
    document.body.classList.add('is-waiting');
  }

  // Takes the full-screen wait down.
  function hideLoader() {
    box.hidden = true;
    hint.hidden = true;
    document.body.classList.remove('is-waiting');
  }

  // What is under way changes while the wait lasts: an upload's progress, then
  // what the server is doing with the file.
  function setLoaderHint(note) {
    hint.textContent = note || '';
    hint.hidden = !note;
  }

  window.showLoader = showLoader;
  window.hideLoader = hideLoader;
  window.setLoaderHint = setLoaderHint;

  // ---- A wait that turns out to be slow -------------------------------------
  // On a quick connection nothing below is ever seen. On a slow one the partner is
  // told the app is still working, rather than left wondering whether a press landed.

  // How long a wait may last before it is said, in milliseconds.
  var SLOW_AFTER = 1000;

  // A wait that leaves the page usable - a change on its way, a quote, a search as
  // it is typed - says so in a line at the foot of the screen once it has lasted a
  // second, and the line goes when the wait ends. Call the function it returns
  // when the wait is over.
  var slowWaits = 0;
  function slowNoticeBox() {
    var notice = document.getElementById('slowNotice');
    if (!notice) {
      notice = document.createElement('div');
      notice.id = 'slowNotice';
      notice.className = 'app-toast';
      notice.setAttribute('role', 'status');
      notice.setAttribute('aria-live', 'polite');
      document.body.appendChild(notice);
    }
    return notice;
  }
  function whenSlow(words) {
    var shown = false;
    var over = false;
    var timer = setTimeout(function () {
      shown = true;
      slowWaits++;
      var notice = slowNoticeBox();
      notice.textContent = words || 'Still working \u2014 the connection is slow\u2026';
      notice.classList.add('app-toast--visible');
    }, SLOW_AFTER);
    return function () {
      if (over) return;
      over = true;
      clearTimeout(timer);
      if (!shown) return;
      slowWaits--;
      if (slowWaits === 0) slowNoticeBox().classList.remove('app-toast--visible');
    };
  }
  window.whenSlow = whenSlow;

  // A move to another page that carries no words of its own - a tile, a menu row,
  // a plain link or form - puts the wait up by itself, but only once the move has
  // lasted a second: the page is still here, so the connection is slow.
  var movingTimer = null;
  function sayIfMoveIsSlow(words) {
    clearTimeout(movingTimer);
    movingTimer = setTimeout(function () {
      if (!box.hidden) return;
      showLoader(words, 'The connection is slow.');
      // A move that never leaves the page - the server answered with nothing to
      // show - must not hold the screen for good.
      setTimeout(hideLoader, 60000);
    }, SLOW_AFTER);
  }

  document.addEventListener('click', function (e) {
    var link = e.target.closest && e.target.closest('a[href]');
    if (!link || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    if (link.hasAttribute('data-loader') || link.hasAttribute('data-partial') || link.hasAttribute('download')) return;
    if (link.target && link.target !== '_self') return;
    var href = link.getAttribute('href') || '';
    if (href === '' || href.charAt(0) === '#' || /^(javascript|mailto|tel):/i.test(href)) return;
    // Once every other listener has had its say: a click another script stopped
    // (the "unsaved changes" question answered No) goes nowhere.
    setTimeout(function () { if (!e.defaultPrevented) sayIfMoveIsSlow('Loading\u2026'); }, 0);
  });

  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (form.target && form.target !== '_self') return;
    // A form posted in place (partial-forms.js), or searched in the browser, stops
    // the submit itself: only one the browser really sends leaves the page.
    setTimeout(function () { if (!e.defaultPrevented) sayIfMoveIsSlow('Working\u2026'); }, 0);
  });

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
    if (e.persisted) { clearTimeout(movingTimer); hideLoader(); }
  });
  // The page is going: a move that was being timed has happened.
  window.addEventListener('pagehide', function () { clearTimeout(movingTimer); });
})();
