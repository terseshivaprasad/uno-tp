// The required-documents dialog (Pages/Shared/_RequiredDocs.cshtml), opened by
// whatever "Click here to see the list of required documents" control the page
// gives the id #openRequiredDocs.
(function () {
  var modal = document.getElementById('requiredDocsModal');
  var opener = document.getElementById('openRequiredDocs');
  var closeBtn = document.getElementById('closeRequiredDocs');
  if (!modal || !opener || !closeBtn) return;

  function openDialog() {
    modal.hidden = false;
    document.body.style.overflow = 'hidden';
    closeBtn.focus();
  }
  function closeDialog() {
    modal.hidden = true;
    document.body.style.overflow = '';
    opener.focus();
  }
  // Tabs: a click or the arrow keys pick an investor type.
  var tabs = Array.prototype.slice.call(modal.querySelectorAll('[role="tab"]'));
  function select(tab) {
    tabs.forEach(function (t) {
      var on = t === tab;
      t.setAttribute('aria-selected', on ? 'true' : 'false');
      t.tabIndex = on ? 0 : -1;
      document.getElementById(t.getAttribute('aria-controls')).hidden = !on;
    });
    tab.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }
  tabs.forEach(function (tab, i) {
    tab.addEventListener('click', function () { select(tab); });
    tab.addEventListener('keydown', function (e) {
      var next = { ArrowDown: i + 1, ArrowRight: i + 1, ArrowUp: i - 1, ArrowLeft: i - 1, Home: 0, End: tabs.length - 1 }[e.key];
      if (next === undefined) return;
      e.preventDefault();
      var t = tabs[(next + tabs.length) % tabs.length];
      select(t);
      t.focus();
    });
  });
  opener.addEventListener('click', openDialog);
  closeBtn.addEventListener('click', closeDialog);
  modal.addEventListener('click', function (e) { if (e.target === modal) closeDialog(); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !modal.hidden) closeDialog(); });
})();
