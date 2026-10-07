// The classic Pay In Slip Generation screen: the search over the last fourteen
// days of applications, and the slip made from a row. One slip carries one
// application, so there is nothing to batch. The slip itself is not produced in
// this mock - generating one only moves the row.
(function () {
  var form = document.getElementById('payinSlipForm');
  if (!form) return;

  var PER_PAGE = 8;
  var rows = Array.prototype.slice.call(document.querySelectorAll('#payinSlipRows tr'));
  var appNo = document.getElementById('payinSlipAppNo');
  var from = document.getElementById('payinSlipFrom');
  var to = document.getElementById('payinSlipTo');
  var error = document.getElementById('payinSlipError');
  var scope = document.getElementById('payinSlipScope');
  var empty = document.getElementById('payinSlipEmpty');
  var count = document.getElementById('payinSlipCount');
  var pages = document.getElementById('payinSlipPages');
  var prev = document.getElementById('payinSlipPrev');
  var next = document.getElementById('payinSlipNext');
  var modes = Array.prototype.slice.call(form.querySelectorAll('[name="payinSlipSearchBy"]'));

  // The window the page opens on, read off the dates the server put in the boxes
  // so the two never drift apart.
  var WINDOW = { from: readDateBoxes(from), to: readDateBoxes(to) };
  var filter = { mode: 'dates', from: WINDOW.from, to: WINDOW.to, appNo: '' };
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

  // The date boxes roll on from one to the next through date-input.js.

  // ---- Which fields the chosen search uses ---------------------------------
  function showFieldsForSearchMode() {
    var mode = modes.filter(function (m) { return m.checked; })[0].value;
    form.querySelectorAll('[data-for]').forEach(function (f) {
      f.hidden = f.dataset.for !== mode;
    });
    error.hidden = true;
    from.classList.remove('is-invalid');
    to.classList.remove('is-invalid');
    appNo.classList.remove('is-invalid');
  }

  modes.forEach(function (m) { m.addEventListener('change', showFieldsForSearchMode); });

  // ---- Running a search ----------------------------------------------------
  function showSearchError(message, field) {
    error.textContent = message;
    error.hidden = false;
    if (field) field.classList.add('is-invalid');
    return false;
  }

  // Checks the search fields and, when they are right, filters the list to what they reach.
  function runSearch() {
    error.hidden = true;
    [from, to, appNo].forEach(function (f) { f.classList.remove('is-invalid'); });

    var mode = modes.filter(function (m) { return m.checked; })[0].value;

    if (mode === 'appno') {
      var q = appNo.value.trim();
      if (q.length < 4) return showSearchError(message('Lists.AppNoTooShort'), appNo);
      filter = { mode: 'appno', appNo: q.toLowerCase() };
      scope.textContent = 'Showing every application matching “' + q + '”, inside the window or cancelled.';
    } else {
      var f = readDateBoxes(from), t = readDateBoxes(to);
      if (!f) return showSearchError(message('Lists.FromDateIncomplete'), from);
      if (!t) return showSearchError(message('Lists.ToDateIncomplete'), to);
      if (f > t) return showSearchError(message('Lists.FromAfterTo'), from);
      if (t > WINDOW.to) return showSearchError(message('Lists.ToInFuture'), to);
      if (f < WINDOW.from) {
        return showSearchError(message('Lists.TooOldForSlip', { earliest: isoToDisplayDate(WINDOW.from) }), from);
      }
      filter = { mode: 'dates', from: f, to: t };
      scope.textContent = f === WINDOW.from && t === WINDOW.to
        ? 'Showing the last ' + daysBetweenInclusive(WINDOW.from, WINDOW.to) + ' days — ' + isoToDisplayDate(f) + ' to ' + isoToDisplayDate(t) + '.'
        : 'Showing applications raised ' + isoToDisplayDate(f) + ' to ' + isoToDisplayDate(t) + '.';
    }

    page = 1;
    renderTablePage();
    return true;
  }

  // Days from one date to another, counting both ends.
  function daysBetweenInclusive(a, b) {
    return Math.round((new Date(b) - new Date(a)) / 86400000) + 1;
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    runSearch();
  });

  document.getElementById('payinSlipClear').addEventListener('click', function () {
    modes[0].checked = true;
    showFieldsForSearchMode();
    writeDateBoxes(from, WINDOW.from);
    writeDateBoxes(to, WINDOW.to);
    appNo.value = '';
    filter = { mode: 'dates', from: WINDOW.from, to: WINDOW.to };
    scope.textContent = 'Showing the last ' + daysBetweenInclusive(WINDOW.from, WINDOW.to) + ' days — ' + isoToDisplayDate(WINDOW.from) + ' to ' + isoToDisplayDate(WINDOW.to) + '.';
    page = 1;
    renderTablePage();
  });

  // ---- The list ------------------------------------------------------------
  function matchesSearch(row) {
    if (filter.mode === 'appno') return row.dataset.appno.indexOf(filter.appNo) !== -1;
    // A date search only ever reaches applications still inside the window.
    return row.dataset.window === 'in'
      && row.dataset.date >= filter.from
      && row.dataset.date <= filter.to;
  }

  // Shows the rows of the current page, and the pager under them.
  function renderTablePage() {
    var kept = rows.filter(matchesSearch);
    var total = Math.max(1, Math.ceil(kept.length / PER_PAGE));
    if (page > total) page = total;
    var start = (page - 1) * PER_PAGE;
    var shown = kept.slice(start, start + PER_PAGE);

    rows.forEach(function (r) { r.hidden = true; });
    shown.forEach(function (r) { r.hidden = false; });

    empty.hidden = kept.length > 0;
    if (!kept.length) {
      empty.textContent = filter.mode === 'appno'
        ? message('Lists.NoApplicationByNumberTryDate')
        : message('Lists.NoApplicationBetweenDates');
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

  // Generating a slip and sending the acceptance link are posts: the backend
  // issues the slip number and sends the link, and the page comes back with them.

  showFieldsForSearchMode();
  renderTablePage();
})();
