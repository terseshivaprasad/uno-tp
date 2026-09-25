// The phone menu behind the hamburger: the welcome line and the links the bar
// carries itself on a wider screen. One panel, closed by anything that means
// "not this" - a click elsewhere, Escape, or a window wide enough to hold the
// links again.
(function () {
  var btn = document.getElementById('appMenuBtn');
  var panel = document.getElementById('appMenuPanel');
  if (!btn || !panel) return;

  function open(yes) {
    panel.hidden = !yes;
    btn.setAttribute('aria-expanded', yes ? 'true' : 'false');
    btn.classList.toggle('is-open', yes);
  }

  btn.addEventListener('click', function (e) {
    e.stopPropagation();
    open(panel.hidden);
  });

  document.addEventListener('click', function (e) {
    if (!panel.hidden && !panel.contains(e.target)) open(false);
  });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !panel.hidden) {
      open(false);
      btn.focus();
    }
  });

  // Past the breakpoint the bar shows the links itself, and a panel left open
  // would hang under a header that no longer has a button to close it.
  window.addEventListener('resize', function () {
    if (!panel.hidden && window.innerWidth > 760) open(false);
  });
})();
