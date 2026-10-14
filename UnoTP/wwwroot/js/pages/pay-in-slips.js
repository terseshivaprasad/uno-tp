// The classic Pay In Slip Generation screen: the search over the last fourteen
// days of applications, and the slip made from a row. One slip carries one
// application, so there is nothing to batch. A slip generated is saved as a PDF
// as soon as the page comes back from generating it.
// The search is run by the server: this script checks the fields and sends
// them, and the page comes back holding what the search reached. The pages
// work on those rows alone.
(function () {
  var form = document.getElementById('payinSlipForm');
  if (!form) return;

  // The slip generated just now: the page carries a link to its PDF, and following
  // it saves the file without leaving the page.
  var slipJustMade = document.getElementById('payinSlipDownload');
  if (slipJustMade) slipJustMade.click();

  var PER_PAGE = +document.getElementById('payinSlipRows').dataset.perPage;
  var rows = Array.prototype.slice.call(document.querySelectorAll('#payinSlipRows tr'));
  var appNo = document.getElementById('payinSlipAppNo');
  var from = document.getElementById('payinSlipFrom');
  var to = document.getElementById('payinSlipTo');
  var error = document.getElementById('payinSlipError');
  var empty = document.getElementById('payinSlipEmpty');
  var count = document.getElementById('payinSlipCount');
  var pages = document.getElementById('payinSlipPages');
  var prev = document.getElementById('payinSlipPrev');
  var next = document.getElementById('payinSlipNext');
  var modes = Array.prototype.slice.call(form.querySelectorAll('[name="by"]'));
  var askedText = document.getElementById('payinSlipAskedText');
  var askedFrom = document.getElementById('payinSlipAskedFrom');
  var askedTo = document.getElementById('payinSlipAskedTo');

  // The window a date search can reach, as the server counts it.
  var WINDOW = { from: form.dataset.earliest, to: form.dataset.today };
  // What the rows on the page are the answer to: 'dates' or 'appno'.
  var searchedBy = form.dataset.by;
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

  // Checks the search fields and, when they are right, puts what is searched for
  // in the form's hidden boxes. True when the search can be sent.
  function readySearch() {
    error.hidden = true;
    [from, to, appNo].forEach(function (f) { f.classList.remove('is-invalid'); });
    askedText.value = '';
    askedFrom.value = '';
    askedTo.value = '';

    var mode = modes.filter(function (m) { return m.checked; })[0].value;

    if (mode === 'appno') {
      var q = appNo.value.trim();
      if (q.length < 4) return showSearchError(message('Lists.AppNoTooShort'), appNo);
      askedText.value = q;
    } else {
      var f = readDateBoxes(from), t = readDateBoxes(to);
      if (!f) return showSearchError(message('Lists.FromDateIncomplete'), from);
      if (!t) return showSearchError(message('Lists.ToDateIncomplete'), to);
      if (f > t) return showSearchError(message('Lists.FromAfterTo'), from);
      if (t > WINDOW.to) return showSearchError(message('Lists.ToInFuture'), to);
      if (f < WINDOW.from) {
        return showSearchError(message('Lists.TooOldForSlip', { earliest: isoToDisplayDate(WINDOW.from) }), from);
      }
      askedFrom.value = f;
      askedTo.value = t;
    }
    return true;
  }

  // A search that is not right stays on the page with its error; one that is
  // right goes to the server, which answers with what it reaches.
  form.addEventListener('submit', function (e) {
    if (!readySearch()) e.preventDefault();
  });

  // Clear goes back to the list the page opens on. When that is the list already
  // on the page, only the fields are put back: nothing is asked again.
  document.getElementById('payinSlipClear').addEventListener('click', function () {
    if (form.dataset.opening !== 'yes') {
      if (window.showLoader) window.showLoader('Loading\u2026');
      window.location.assign(form.getAttribute('action'));
      return;
    }
    modes[0].checked = true;
    showFieldsForSearchMode();
    writeDateBoxes(from, WINDOW.from);
    writeDateBoxes(to, WINDOW.to);
    appNo.value = '';
    page = 1;
    renderTablePage();
  });

  // ---- The list ------------------------------------------------------------
  // Every row on the page is one the search reached.
  // Shows the rows of the current page, and the pager under them.
  function renderTablePage() {
    var kept = rows;
    var total = Math.max(1, Math.ceil(kept.length / PER_PAGE));
    if (page > total) page = total;
    var start = (page - 1) * PER_PAGE;
    var shown = kept.slice(start, start + PER_PAGE);

    rows.forEach(function (r) { r.hidden = true; });
    shown.forEach(function (r) { r.hidden = false; });

    empty.hidden = kept.length > 0;
    if (!kept.length) {
      empty.textContent = searchedBy === 'appno'
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
