// The top bar's bell: opens the list of scheduled rate changes, downtime and
// maintenance, and closes on Escape or a click anywhere else.
(function () {
  var bell = document.getElementById('noticesBell');
  var panel = document.getElementById('noticesPanel');
  if (!bell || !panel) return;

  function open(yes) {
    panel.hidden = !yes;
    bell.setAttribute('aria-expanded', yes ? 'true' : 'false');
  }

  bell.addEventListener('click', function (e) {
    e.stopPropagation();
    open(panel.hidden);
  });
  document.getElementById('noticesClose').addEventListener('click', function () {
    open(false);
    bell.focus();
  });
  panel.addEventListener('click', function (e) { e.stopPropagation(); });
  document.addEventListener('click', function () { if (!panel.hidden) open(false); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !panel.hidden) {
      open(false);
      bell.focus();
    }
  });
})();
