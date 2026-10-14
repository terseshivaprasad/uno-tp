// The classic View Application screen: the search that decides which
// applications are on the list, the status filter over whatever it found, and
// the read-only sheet a row opens. Nothing here changes an application - the
// screen only looks at one.
// The search is run by the server: this script checks the fields and sends
// them, and the page comes back holding what the search reached. The status
// pills, the narrowing box and the pages work on those rows alone.
(function () {
  var form = document.getElementById('viewAppForm');
  if (!form) return;

  var PER_PAGE = +document.getElementById('viewAppRows').dataset.perPage;
  var rows = Array.prototype.slice.call(document.querySelectorAll('#viewAppRows tr'));
  var pills = Array.prototype.slice.call(document.querySelectorAll('.register-pill'));
  var appNo = document.getElementById('viewAppAppNo');
  var folio = document.getElementById('viewAppFolio');
  var from = document.getElementById('viewAppFrom');
  var to = document.getElementById('viewAppTo');
  var error = document.getElementById('viewAppError');
  var narrow = document.getElementById('viewAppFilter');
  var empty = document.getElementById('viewAppEmpty');
  var count = document.getElementById('viewAppCount');
  var pages = document.getElementById('viewAppPages');
  var prev = document.getElementById('viewAppPrev');
  var next = document.getElementById('viewAppNext');
  var modes = Array.prototype.slice.call(form.querySelectorAll('[name="by"]'));
  var askedText = document.getElementById('viewAppAskedText');
  var askedFrom = document.getElementById('viewAppAskedFrom');
  var askedTo = document.getElementById('viewAppAskedTo');

  // The window a date search can reach, as the server counts it.
  var WINDOW = { from: form.dataset.earliest, to: form.dataset.today };
  // What the rows on the page are the answer to: 'dates', 'appno' or 'folio'.
  var searchedBy = form.dataset.by;
  var state = 'all';
  var page = 1;

  // ---- The date boxes ------------------------------------------------------
  // Three parts read as one yyyy-mm-dd, or as null while any part is unfilled or
  // the three together are not a real day.
  function readDateBoxes(box) {
    var dd = box.querySelector('[data-dd]').value.trim();
    var mm = box.querySelector('[data-mm]').value.trim();
    var yyyy = box.querySelector('[data-yyyy]').value.trim();
    if (!/^\d{1,2}$/.test(dd) || !/^\d{1,2}$/.test(mm) || !/^\d{4}$/.test(yyyy)) return null;
    var d = new Date(+yyyy, +mm - 1, +dd);
    if (d.getFullYear() !== +yyyy || d.getMonth() !== +mm - 1 || d.getDate() !== +dd) return null;
    return yyyy + '-' + padToTwoDigits(mm) + '-' + padToTwoDigits(dd);
  }

  // Pads a number to two digits: 7 -> "07".
  function padToTwoDigits(n) { return ('0' + n).slice(-2); }

  // yyyy-mm-dd -> dd/mm/yyyy, as the page shows dates.
  function isoToDisplayDate(iso) {
    var p = iso.split('-');
    return p[2] + '/' + p[1] + '/' + p[0];
  }

  // Writes a yyyy-mm-dd date into a DD / MM / YYYY box set.
  function writeDateBoxes(box, iso) {
    var p = iso.split('-');
    box.querySelector('[data-dd]').value = p[2];
    box.querySelector('[data-mm]').value = p[1];
    box.querySelector('[data-yyyy]').value = p[0];
  }

  // Days from one date to another, counting both ends.
  function daysBetweenInclusive(a, b) {
    return Math.round((new Date(b) - new Date(a)) / 86400000) + 1;
  }

  // The date boxes roll on from one to the next through date-input.js.

  // ---- Which fields the chosen search uses ---------------------------------
  function showFieldsForSearchMode() {
    var mode = modes.filter(function (m) { return m.checked; })[0].value;
    form.querySelectorAll('[data-for]').forEach(function (f) {
      f.hidden = f.dataset.for !== mode;
    });
    error.hidden = true;
    [from, to, appNo, folio].forEach(function (f) { f.classList.remove('is-invalid'); });
  }

  modes.forEach(function (m) { m.addEventListener('change', showFieldsForSearchMode); });

  // ---- Running a search ----------------------------------------------------
  function showSearchError(message, field) {
    error.textContent = message;
    error.hidden = false;
    if (field) field.classList.add('is-invalid');
    return false;
  }

  // Checks the search fields and, when they are right, puts what is searched for
  // in the form's hidden boxes. True when the search can be sent.
  function readySearch() {
    error.hidden = true;
    [from, to, appNo, folio].forEach(function (f) { f.classList.remove('is-invalid'); });
    askedText.value = '';
    askedFrom.value = '';
    askedTo.value = '';

    var mode = modes.filter(function (m) { return m.checked; })[0].value;

    if (mode === 'appno') {
      var q = appNo.value.trim();
      if (q.length < 4) return showSearchError(message('Lists.AppNoTooShort'), appNo);
      askedText.value = q;
    } else if (mode === 'folio') {
      var f = folio.value.trim();
      if (f.length < 4) return showSearchError(message('Lists.FolioTooShort'), folio);
      askedText.value = f;
    } else {
      var a = readDateBoxes(from), b = readDateBoxes(to);
      if (!a) return showSearchError(message('Lists.FromDateIncomplete'), from);
      if (!b) return showSearchError(message('Lists.ToDateIncomplete'), to);
      if (a > b) return showSearchError(message('Lists.FromAfterTo'), from);
      if (b > WINDOW.to) return showSearchError(message('Lists.ToInFuture'), to);
      if (a < WINDOW.from) {
        return showSearchError(message('Lists.DateSearchReach', { days: daysBetweenInclusive(WINDOW.from, WINDOW.to), earliest: isoToDisplayDate(WINDOW.from) }), from);
      }
      askedFrom.value = a;
      askedTo.value = b;
    }
    return true;
  }

  // A search that is not right stays on the page with its error; one that is
  // right goes to the server, which answers with what it reaches.
  form.addEventListener('submit', function (e) {
    if (!readySearch()) e.preventDefault();
  });

  // Clear goes back to the list the page opens on. When that is the list already
  // on the page, only the filters over it are taken off: nothing is asked again.
  document.getElementById('viewAppClear').addEventListener('click', function () {
    if (form.dataset.opening !== 'yes') {
      if (window.showLoader) window.showLoader('Loading\u2026');
      window.location.assign(window.location.pathname);
      return;
    }
    modes[0].checked = true;
    showFieldsForSearchMode();
    writeDateBoxes(from, WINDOW.from);
    writeDateBoxes(to, WINDOW.to);
    appNo.value = '';
    folio.value = '';
    narrow.value = '';
    setStatusFilter('all');
    page = 1;
    renderTablePage();
  });

  // ---- The list ------------------------------------------------------------
  // True when a row has the chosen status and contains the narrowing text. Every
  // row on the page is one the search reached.
  function matchesStatusAndFilter(row) {
    var q = narrow.value.trim().toLowerCase();
    return (state === 'all' || row.dataset.state === state)
      && (!q || row.dataset.search.indexOf(q) !== -1);
  }

  // Selects a status pill and remembers its status as the filter.
  function setStatusFilter(key) {
    state = key;
    pills.forEach(function (p) { p.classList.toggle('register-pill--on', p.dataset.state === key); });
  }

  pills.forEach(function (pill) {
    pill.addEventListener('click', function () {
      setStatusFilter(pill.dataset.state);
      page = 1;
      renderTablePage();
    });
  });

  narrow.addEventListener('input', function () { page = 1; renderTablePage(); });

  // Writes on each status pill how many of the rows have that status. The pills
  // count every row the search reached, so they say what is there to be filtered.
  function updateStatusCounts() {
    pills.forEach(function (pill) {
      var key = pill.dataset.state;
      var n = key === 'all' ? rows.length : rows.filter(function (r) { return r.dataset.state === key; }).length;
      pill.querySelector('.register-pill__number').textContent = n;
    });
  }

  // What the empty list says, by what was searched for.
  function nothingFoundMessage() {
    if (searchedBy === 'appno') return message('Lists.NoApplicationByNumberTryFolio');
    if (searchedBy === 'folio') return message('Lists.NoApplicationUnderFolio');
    return message('Lists.NoApplicationBetweenDates');
  }

  // Shows the rows of the current page, and the pager and counts under them.
  function renderTablePage() {
    updateStatusCounts();

    var kept = rows.filter(matchesStatusAndFilter);
    var total = Math.max(1, Math.ceil(kept.length / PER_PAGE));
    if (page > total) page = total;
    var start = (page - 1) * PER_PAGE;
    var shown = kept.slice(start, start + PER_PAGE);

    rows.forEach(function (r) { r.hidden = true; });
    shown.forEach(function (r) { r.hidden = false; });

    empty.hidden = kept.length > 0;
    if (!kept.length) {
      empty.textContent = nothingFoundMessage();
    }

    count.textContent = kept.length
      ? 'Showing ' + (start + 1) + '–' + (start + shown.length) + ' of ' + kept.length + (kept.length === 1 ? ' application' : ' applications')
      : '';

    pages.textContent = 'Page ' + page + ' of ' + total;
    prev.disabled = page === 1;
    next.disabled = page === total;
    document.querySelector('.register-pager').hidden = kept.length === 0;
  }

  prev.addEventListener('click', function () { if (page > 1) { page--; renderTablePage(); } });
  next.addEventListener('click', function () { page++; renderTablePage(); });

  // ---- The application itself ----------------------------------------------
  // The row hands over its own sheet, built by the server; the dialog only
  // carries it. Nothing on it can be edited.
  var modal = document.getElementById('viewAppModal');
  var modalTitle = document.getElementById('viewAppModalTitle');
  var modalSub = document.getElementById('viewAppModalSub');
  var modalBody = document.getElementById('viewAppModalBody');
  var closeBtn = document.getElementById('viewAppModalClose');
  var opener = null;

  // Opens the details dialog for one application.
  function openApplicationSheet(appNumber) {
    var sheet = document.querySelector('[data-sheet="' + appNumber + '"]');
    if (!sheet) return;
    modalTitle.textContent = sheet.dataset.title;
    modalSub.textContent = sheet.dataset.sub;
    modalBody.textContent = '';
    modalBody.appendChild(sheet.cloneNode(true));
    loadApplicationDetails(appNumber);
    modal.hidden = false;
    document.body.style.overflow = 'hidden';
    modalBody.scrollTop = 0;
    closeBtn.focus();
  }

  // The application in full, under its status: Review Summary's sections,
  // read-only, fetched for the one row opened. A slow answer for a sheet since
  // closed or changed is dropped.
  var asked = 0;
  // Fetches the application's full details into the open dialog.
  function loadApplicationDetails(appNumber) {
    var box = document.createElement('div');
    box.className = 'view-app-details';
    box.setAttribute('aria-busy', 'true');
    box.innerHTML = '<p class="view-app-details__wait">Loading the application&hellip;</p>';
    modalBody.appendChild(box);
    var mine = ++asked;
    var url = window.location.pathname.replace(/\/$/, '') + '/' + encodeURIComponent(appNumber) + '/details';
    fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch' } })
      .then(function (res) { return res.ok ? res.text() : Promise.reject(res.status); })
      .then(function (html) {
        if (mine !== asked) return;
        box.innerHTML = html;
        box.removeAttribute('aria-busy');
      })
      .catch(function () {
        if (mine !== asked) return;
        box.removeAttribute('aria-busy');
        box.innerHTML = '<p class="view-app-details__wait">The full details of this application could not be loaded. Close this and open it again.</p>';
      });
  }

  // Closes the details dialog and returns focus to the row that opened it.
  function closeApplicationSheet() {
    asked++;
    modal.hidden = true;
    document.body.style.overflow = '';
    if (opener) opener.focus();
  }

  rows.forEach(function (row) {
    var view = row.querySelector('[data-view]');
    if (!view) return;
    view.addEventListener('click', function () {
      opener = view;
      openApplicationSheet(view.dataset.view);
    });
  });

  // On a phone the whole row opens the application, not only its View.
  document.getElementById('viewAppRows').addEventListener('click', function (e) {
    if (!window.matchMedia('(max-width: 560px)').matches) return;
    if (e.target.closest('button, a')) return;
    var row = e.target.closest('tr');
    var view = row && row.querySelector('[data-view]');
    if (view) view.click();
  });

  closeBtn.addEventListener('click', closeApplicationSheet);
  modal.addEventListener('click', function (e) { if (e.target === modal) closeApplicationSheet(); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !modal.hidden) closeApplicationSheet();
  });

  showFieldsForSearchMode();
  renderTablePage();
})();
