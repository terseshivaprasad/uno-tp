(function () {
  var toastEl = document.getElementById('toast');
  var toastTimer = null;
  function showToast(message) {
    if (!toastEl) return;
    toastEl.textContent = message;
    toastEl.classList.add('toast--visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove('toast--visible'); }, 2200);
  }

  // Any control explicitly marked as a stub in this mock build.
  document.querySelectorAll('.js-stub').forEach(function (el) {
    el.addEventListener('click', function () {
      showToast('Not wired up in this mock yet.');
    });
  });

  // Status pill filter + free-text search over the in-flight table.
  var tbody = document.getElementById('applicationsTbody');
  var emptyRow = document.getElementById('applicationsEmpty');
  var searchInput = document.getElementById('appSearch');
  var pills = document.querySelectorAll('#statusPills .status-pill[data-status]');
  var activeStatus = 'all';

  function applyFilters() {
    if (!tbody) return;
    var q = (searchInput && searchInput.value || '').trim().toLowerCase();
    var rows = tbody.querySelectorAll('[data-status]');
    var visible = 0;
    rows.forEach(function (row) {
      var status = row.getAttribute('data-status');
      var haystack = row.getAttribute('data-search') || '';
      var matchesStatus = activeStatus === 'all' || status === activeStatus;
      var matchesSearch = q === '' || haystack.indexOf(q) !== -1;
      var show = matchesStatus && matchesSearch;
      row.hidden = !show;
      if (show) visible += 1;
    });
    if (emptyRow) emptyRow.hidden = visible !== 0;
  }

  pills.forEach(function (pill) {
    if (pill.classList.contains('js-stub')) return; // Booked / Lapsed / All: no matching mock rows
    pill.addEventListener('click', function () {
      pills.forEach(function (p) { p.classList.remove('status-pill--active'); });
      pill.classList.add('status-pill--active');
      activeStatus = pill.getAttribute('data-status');
      applyFilters();
    });
  });

  if (searchInput) {
    searchInput.addEventListener('input', applyFilters);
  }

  // Pagination: only page 1 has mock data; page 2 shows the empty state honestly.
  var pageButtons = document.querySelectorAll('.js-page');
  var summaryEl = document.getElementById('tableSummary');
  pageButtons.forEach(function (btn) {
    btn.addEventListener('click', function () {
      pageButtons.forEach(function (b) { b.classList.remove('js-page--active'); });
      btn.classList.add('js-page--active');
      var page = btn.getAttribute('data-page');
      if (page === '2') {
        tbody.querySelectorAll('[data-status]').forEach(function (row) { row.hidden = true; });
        if (emptyRow) emptyRow.hidden = false;
        if (summaryEl) summaryEl.textContent = 'Showing 8–14 of 14 in flight · this mock only seeds the first page.';
      } else {
        if (summaryEl) summaryEl.textContent = 'Showing 1–7 of 14 in flight · 5 need you, 6 with Operations, 3 awaiting realisation';
        activeStatus = 'all';
        pills.forEach(function (p) { p.classList.remove('status-pill--active'); });
        var allPill = document.querySelector('#statusPills .status-pill[data-status="all"]');
        if (allPill) allPill.classList.add('status-pill--active');
        applyFilters();
      }
    });
  });

  // App-tile search (Board 00 keeps this separate from the in-flight search):
  // hides tiles that do not match, and any section left with nothing in it.
  var tileSearch = document.getElementById('appTileSearch');
  var tileSections = document.querySelectorAll('.tile-section');
  var tileEmpty = document.getElementById('appTileEmpty');
  if (tileSearch) {
    tileSearch.addEventListener('input', function () {
      var q = tileSearch.value.trim().toLowerCase();
      var total = 0;
      tileSections.forEach(function (section) {
        var shown = 0;
        section.querySelectorAll('.app-tile[data-search]').forEach(function (tile) {
          var match = q === '' || tile.getAttribute('data-search').indexOf(q) !== -1;
          tile.hidden = !match;
          if (match) shown += 1;
        });
        section.hidden = shown === 0;
        total += shown;
      });
      if (tileEmpty) tileEmpty.hidden = total !== 0;
    });
  }

  // Edit pinned toggle (cosmetic in this mock — no drag-and-drop persistence yet).
  var editPinnedBtn = document.getElementById('editPinnedBtn');
  if (editPinnedBtn) {
    var editing = false;
    editPinnedBtn.addEventListener('click', function () {
      editing = !editing;
      editPinnedBtn.textContent = editing ? 'Done' : 'Edit pinned';
      showToast(editing ? 'Drag to reorder is not wired up in this mock yet.' : 'Pinned apps saved.');
    });
  }

  // Rate card modal.
  var rateCardBtn = document.getElementById('rateCardBtn');
  var rateCardModal = document.getElementById('rateCardModal');
  var rateCardCloseBtn = document.getElementById('rateCardCloseBtn');
  if (rateCardBtn && rateCardModal) {
    rateCardBtn.addEventListener('click', function () { rateCardModal.hidden = false; });
  }
  if (rateCardCloseBtn && rateCardModal) {
    rateCardCloseBtn.addEventListener('click', function () { rateCardModal.hidden = true; });
  }
  if (rateCardModal) {
    rateCardModal.addEventListener('click', function (e) {
      if (e.target === rateCardModal) rateCardModal.hidden = true;
    });
  }
})();
