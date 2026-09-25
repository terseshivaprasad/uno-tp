// The classic Pay In Slip Generation screen: the search over the last fourteen
// days of applications, and the slip made from a row. One slip carries one
// application, so there is nothing to batch. The slip itself is not produced in
// this mock - generating one only moves the row.
(function () {
  var form = document.getElementById('pisForm');
  if (!form) return;

  var PER_PAGE = 8;
  var rows = Array.prototype.slice.call(document.querySelectorAll('#pisRows tr'));
  var appNo = document.getElementById('pisAppNo');
  var from = document.getElementById('pisFrom');
  var to = document.getElementById('pisTo');
  var error = document.getElementById('pisError');
  var scope = document.getElementById('pisScope');
  var empty = document.getElementById('pisEmpty');
  var count = document.getElementById('pisCount');
  var pages = document.getElementById('pisPages');
  var prev = document.getElementById('pisPrev');
  var next = document.getElementById('pisNext');
  var modes = Array.prototype.slice.call(form.querySelectorAll('[name="pisSearchBy"]'));

  // The window the page opens on, read off the dates the server put in the boxes
  // so the two never drift apart.
  var WINDOW = { from: read(from), to: read(to) };
  var filter = { mode: 'dates', from: WINDOW.from, to: WINDOW.to, appNo: '' };
  var page = 1;

  // ---- The date boxes ------------------------------------------------------
  // Three parts read as one yyyy-mm-dd, or as null while any part is unfilled or
  // the three together are not a real day.
  function read(box) {
    var dd = box.querySelector('[data-dd]').value.trim();
    var mm = box.querySelector('[data-mm]').value.trim();
    var yyyy = box.querySelector('[data-yyyy]').value.trim();
    if (!/^\d{1,2}$/.test(dd) || !/^\d{1,2}$/.test(mm) || !/^\d{4}$/.test(yyyy)) return null;
    var d = new Date(+yyyy, +mm - 1, +dd);
    if (d.getFullYear() !== +yyyy || d.getMonth() !== +mm - 1 || d.getDate() !== +dd) return null;
    return yyyy + '-' + pad(mm) + '-' + pad(dd);
  }

  function pad(n) { return ('0' + n).slice(-2); }

  function show(iso) {
    var p = iso.split('-');
    return p[2] + '/' + p[1] + '/' + p[0];
  }

  function write(box, iso) {
    var p = iso.split('-');
    box.querySelector('[data-dd]').value = p[2];
    box.querySelector('[data-mm]').value = p[1];
    box.querySelector('[data-yyyy]').value = p[0];
  }

  // The date boxes roll on from one to the next through date-parts.js.

  // ---- Which fields the chosen search uses ---------------------------------
  function syncMode() {
    var mode = modes.filter(function (m) { return m.checked; })[0].value;
    form.querySelectorAll('[data-for]').forEach(function (f) {
      f.hidden = f.dataset.for !== mode;
    });
    error.hidden = true;
    from.classList.remove('is-invalid');
    to.classList.remove('is-invalid');
    appNo.classList.remove('is-invalid');
  }

  modes.forEach(function (m) { m.addEventListener('change', syncMode); });

  // ---- Running a search ----------------------------------------------------
  function fail(message, field) {
    error.textContent = message;
    error.hidden = false;
    if (field) field.classList.add('is-invalid');
    return false;
  }

  function search() {
    error.hidden = true;
    [from, to, appNo].forEach(function (f) { f.classList.remove('is-invalid'); });

    var mode = modes.filter(function (m) { return m.checked; })[0].value;

    if (mode === 'appno') {
      var q = appNo.value.trim();
      if (q.length < 4) return fail('Enter at least the last four characters of the application number.', appNo);
      filter = { mode: 'appno', appNo: q.toLowerCase() };
      scope.textContent = 'Showing every application matching “' + q + '”, inside the window or cancelled.';
    } else {
      var f = read(from), t = read(to);
      if (!f) return fail('Enter a complete From Date as DD / MM / YYYY.', from);
      if (!t) return fail('Enter a complete To Date as DD / MM / YYYY.', to);
      if (f > t) return fail('The From Date cannot be after the To Date.', from);
      if (t > WINDOW.to) return fail('The To Date cannot be in the future.', to);
      if (f < WINDOW.from) {
        return fail('An application older than that has cancelled itself, so no slip can be made for it. The earliest date you can search from is ' + show(WINDOW.from) + '.', from);
      }
      filter = { mode: 'dates', from: f, to: t };
      scope.textContent = f === WINDOW.from && t === WINDOW.to
        ? 'Showing the last ' + days(WINDOW.from, WINDOW.to) + ' days — ' + show(f) + ' to ' + show(t) + '.'
        : 'Showing applications raised ' + show(f) + ' to ' + show(t) + '.';
    }

    page = 1;
    render();
    return true;
  }

  function days(a, b) {
    return Math.round((new Date(b) - new Date(a)) / 86400000) + 1;
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    search();
  });

  document.getElementById('pisClear').addEventListener('click', function () {
    modes[0].checked = true;
    syncMode();
    write(from, WINDOW.from);
    write(to, WINDOW.to);
    appNo.value = '';
    filter = { mode: 'dates', from: WINDOW.from, to: WINDOW.to };
    scope.textContent = 'Showing the last ' + days(WINDOW.from, WINDOW.to) + ' days — ' + show(WINDOW.from) + ' to ' + show(WINDOW.to) + '.';
    page = 1;
    render();
  });

  // ---- The list ------------------------------------------------------------
  function matches(row) {
    if (filter.mode === 'appno') return row.dataset.appno.indexOf(filter.appNo) !== -1;
    // A date search only ever reaches applications still inside the window.
    return row.dataset.window === 'in'
      && row.dataset.date >= filter.from
      && row.dataset.date <= filter.to;
  }

  function render() {
    var kept = rows.filter(matches);
    var total = Math.max(1, Math.ceil(kept.length / PER_PAGE));
    if (page > total) page = total;
    var start = (page - 1) * PER_PAGE;
    var shown = kept.slice(start, start + PER_PAGE);

    rows.forEach(function (r) { r.hidden = true; });
    shown.forEach(function (r) { r.hidden = false; });

    empty.hidden = kept.length > 0;
    if (!kept.length) {
      empty.textContent = filter.mode === 'appno'
        ? 'No application matches that number. Check it, or search by date instead.'
        : 'No application was raised between those two dates.';
    }

    count.textContent = kept.length
      ? 'Showing ' + (start + 1) + '–' + (start + shown.length) + ' of ' + kept.length + (kept.length === 1 ? ' application' : ' applications')
      : '';

    pages.textContent = '';
    for (var i = 1; i <= total; i++) {
      (function (n) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'su-page' + (n === page ? ' su-page--on' : '');
        b.textContent = n;
        if (n === page) b.setAttribute('aria-current', 'page');
        b.addEventListener('click', function () { page = n; render(); });
        pages.appendChild(b);
      })(i);
    }
    prev.disabled = page === 1;
    next.disabled = page === total;
    document.querySelector('.su-pager').hidden = kept.length === 0;
  }

  prev.addEventListener('click', function () { if (page > 1) { page--; render(); } });
  next.addEventListener('click', function () { page++; render(); });

  // Generating a slip and sending the acceptance link are posts: the backend
  // issues the slip number and sends the link, and the page comes back with them.

  syncMode();
  render();
})();
