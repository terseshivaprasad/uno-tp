// The classic View Application screen: the search that decides which
// applications are on the list, the status filter over whatever it found, and
// the read-only sheet a row opens. Nothing here changes an application - the
// screen only looks at one.
(function () {
  var form = document.getElementById('vaForm');
  if (!form) return;

  var PER_PAGE = 8;
  var rows = Array.prototype.slice.call(document.querySelectorAll('#vaRows tr'));
  var pills = Array.prototype.slice.call(document.querySelectorAll('.su-pill'));
  var appNo = document.getElementById('vaAppNo');
  var folio = document.getElementById('vaFolio');
  var from = document.getElementById('vaFrom');
  var to = document.getElementById('vaTo');
  var error = document.getElementById('vaError');
  var scope = document.getElementById('vaScope');
  var narrow = document.getElementById('vaFilter');
  var empty = document.getElementById('vaEmpty');
  var count = document.getElementById('vaCount');
  var pages = document.getElementById('vaPages');
  var prev = document.getElementById('vaPrev');
  var next = document.getElementById('vaNext');
  var modes = Array.prototype.slice.call(form.querySelectorAll('[name="vaSearchBy"]'));

  // The window the page opens on, read off the dates the server put in the boxes
  // so the two never drift apart.
  var WINDOW = { from: read(from), to: read(to) };
  var search = { mode: 'dates', from: WINDOW.from, to: WINDOW.to };
  var state = 'all';
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

  function days(a, b) {
    return Math.round((new Date(b) - new Date(a)) / 86400000) + 1;
  }

  // Typing rolls on to the next box the way the old jq-dte control did.
  [from, to].forEach(function (box) {
    var parts = Array.prototype.slice.call(box.querySelectorAll('.csi-date__part'));
    parts.forEach(function (part, i) {
      part.addEventListener('input', function () {
        part.value = part.value.replace(/\D/g, '');
        if (part.value.length === part.maxLength && parts[i + 1]) parts[i + 1].focus();
      });
    });
  });

  // ---- Which fields the chosen search uses ---------------------------------
  function syncMode() {
    var mode = modes.filter(function (m) { return m.checked; })[0].value;
    form.querySelectorAll('[data-for]').forEach(function (f) {
      f.hidden = f.dataset.for !== mode;
    });
    error.hidden = true;
    [from, to, appNo, folio].forEach(function (f) { f.classList.remove('is-invalid'); });
  }

  modes.forEach(function (m) { m.addEventListener('change', syncMode); });

  // ---- Running a search ----------------------------------------------------
  function fail(message, field) {
    error.textContent = message;
    error.hidden = false;
    if (field) field.classList.add('is-invalid');
    return false;
  }

  function run() {
    error.hidden = true;
    [from, to, appNo, folio].forEach(function (f) { f.classList.remove('is-invalid'); });

    var mode = modes.filter(function (m) { return m.checked; })[0].value;

    if (mode === 'appno') {
      var q = appNo.value.trim();
      if (q.length < 4) return fail('Enter at least the last four characters of the application number.', appNo);
      search = { mode: 'appno', appNo: q.toLowerCase() };
      scope.textContent = 'Showing every application matching “' + q + '”, inside the window or older.';
    } else if (mode === 'folio') {
      var f = folio.value.trim();
      if (f.length < 4) return fail('Enter at least the last four characters of the folio number.', folio);
      search = { mode: 'folio', folio: f.toLowerCase() };
      scope.textContent = 'Showing every application under folio “' + f + '”, however old.';
    } else {
      var a = read(from), b = read(to);
      if (!a) return fail('Enter a complete From Date as DD / MM / YYYY.', from);
      if (!b) return fail('Enter a complete To Date as DD / MM / YYYY.', to);
      if (a > b) return fail('The From Date cannot be after the To Date.', from);
      if (b > WINDOW.to) return fail('The To Date cannot be in the future.', to);
      if (a < WINDOW.from) {
        return fail('A date search reaches back ' + days(WINDOW.from, WINDOW.to) + ' days, to ' + show(WINDOW.from) + '. For anything older, search by application number or folio.', from);
      }
      search = { mode: 'dates', from: a, to: b };
      scope.textContent = a === WINDOW.from && b === WINDOW.to
        ? 'Showing the last ' + days(WINDOW.from, WINDOW.to) + ' days — ' + show(a) + ' to ' + show(b) + '.'
        : 'Showing applications raised ' + show(a) + ' to ' + show(b) + '.';
    }

    page = 1;
    render();
    return true;
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    run();
  });

  document.getElementById('vaClear').addEventListener('click', function () {
    modes[0].checked = true;
    syncMode();
    write(from, WINDOW.from);
    write(to, WINDOW.to);
    appNo.value = '';
    folio.value = '';
    narrow.value = '';
    setState('all');
    search = { mode: 'dates', from: WINDOW.from, to: WINDOW.to };
    scope.textContent = 'Showing the last ' + days(WINDOW.from, WINDOW.to) + ' days — ' + show(WINDOW.from) + ' to ' + show(WINDOW.to) + '.';
    page = 1;
    render();
  });

  // ---- The list ------------------------------------------------------------
  // What the search alone reaches, before the status filter narrows it. The
  // pills count against this, so they always say what is there to be filtered.
  function found(row) {
    if (search.mode === 'appno') return row.dataset.appno.indexOf(search.appNo) !== -1;
    // A folio search follows the investor rather than the window, so it reaches
    // their older applications too. A new customer has no folio to match.
    if (search.mode === 'folio') return !!row.dataset.folio && row.dataset.folio.indexOf(search.folio) !== -1;
    // A date search only ever reaches applications inside the window.
    return row.dataset.window === 'in'
      && row.dataset.date >= search.from
      && row.dataset.date <= search.to;
  }

  function matches(row) {
    var q = narrow.value.trim().toLowerCase();
    return found(row)
      && (state === 'all' || row.dataset.state === state)
      && (!q || row.dataset.search.indexOf(q) !== -1);
  }

  function setState(key) {
    state = key;
    pills.forEach(function (p) { p.classList.toggle('su-pill--on', p.dataset.state === key); });
  }

  pills.forEach(function (pill) {
    pill.addEventListener('click', function () {
      setState(pill.dataset.state);
      page = 1;
      render();
    });
  });

  narrow.addEventListener('input', function () { page = 1; render(); });

  function recount(reached) {
    pills.forEach(function (pill) {
      var key = pill.dataset.state;
      var n = key === 'all' ? reached.length : reached.filter(function (r) { return r.dataset.state === key; }).length;
      pill.querySelector('.su-pill__n').textContent = n;
    });
  }

  function render() {
    recount(rows.filter(found));

    var kept = rows.filter(matches);
    var total = Math.max(1, Math.ceil(kept.length / PER_PAGE));
    if (page > total) page = total;
    var start = (page - 1) * PER_PAGE;
    var shown = kept.slice(start, start + PER_PAGE);

    rows.forEach(function (r) { r.hidden = true; });
    shown.forEach(function (r) { r.hidden = false; });

    empty.hidden = kept.length > 0;
    if (!kept.length) {
      empty.textContent = search.mode === 'appno'
        ? 'No application matches that number. Check it, or search by folio instead.'
        : search.mode === 'folio'
          ? 'No application under that folio. A new customer has no folio until their first deposit books — search by application number instead.'
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

  // ---- The application itself ----------------------------------------------
  // The row hands over its own sheet, built by the server; the dialog only
  // carries it. Nothing on it can be edited.
  var modal = document.getElementById('vaModal');
  var modalTitle = document.getElementById('vaModalTitle');
  var modalSub = document.getElementById('vaModalSub');
  var modalBody = document.getElementById('vaModalBody');
  var closeBtn = document.getElementById('vaModalClose');
  var opener = null;

  function open(appNumber) {
    var sheet = document.querySelector('[data-sheet="' + appNumber + '"]');
    if (!sheet) return;
    modalTitle.textContent = sheet.dataset.title;
    modalSub.textContent = sheet.dataset.sub;
    modalBody.textContent = '';
    modalBody.appendChild(sheet.cloneNode(true));
    modal.hidden = false;
    document.body.style.overflow = 'hidden';
    modalBody.scrollTop = 0;
    closeBtn.focus();
  }

  function close() {
    modal.hidden = true;
    document.body.style.overflow = '';
    if (opener) opener.focus();
  }

  rows.forEach(function (row) {
    var view = row.querySelector('[data-view]');
    if (!view) return;
    view.addEventListener('click', function () {
      opener = view;
      open(view.dataset.view);
    });
  });

  closeBtn.addEventListener('click', close);
  modal.addEventListener('click', function (e) { if (e.target === modal) close(); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !modal.hidden) close();
  });

  syncMode();
  render();
})();
