// The classic Short URL page: sending a link against a pending application, and
// the filter, search and pages over the list. The link itself is never shown -
// it goes to the investor's contacts on the application and nowhere else.
(function () {
  var table = document.getElementById('registerRows');
  if (!table) return;

  // ---- The links sent: filter, search, pages -------------------------------
  var PER_PAGE = 8;
  var rows = Array.prototype.slice.call(document.querySelectorAll('#registerRows tr'));
  var pills = Array.prototype.slice.call(document.querySelectorAll('.register-pill'));
  var search = document.getElementById('registerSearch');
  var empty = document.getElementById('registerEmpty');
  var count = document.getElementById('registerCount');
  var pages = document.getElementById('registerPages');
  var prev = document.getElementById('registerPrev');
  var next = document.getElementById('registerNext');
  var state = 'all';
  var page = 1;

  // Each pill carries how many links are in that state, counted once up front.
  pills.forEach(function (pill) {
    var key = pill.dataset.state;
    var n = key === 'all' ? rows.length : rows.filter(function (r) { return r.dataset.state === key; }).length;
    pill.querySelector('.register-pill__number').textContent = n;
    pill.addEventListener('click', function () {
      pills.forEach(function (p) { p.classList.toggle('register-pill--on', p === pill); });
      state = key;
      page = 1;
      renderTablePage();
    });
  });

  search.addEventListener('input', function () { page = 1; renderTablePage(); });

  // Sending and regenerating a link are posts: the backend sends it, and the
  // page comes back with the link as it now stands.

  // True when a row has the chosen status and contains the search text.
  function matchesStatusAndSearch(row) {
    var q = search.value.trim().toLowerCase();
    return (state === 'all' || row.dataset.state === state)
      && (!q || row.dataset.search.indexOf(q) !== -1);
  }

  // Shows the rows of the current page, and the pager under them.
  function renderTablePage() {
    var kept = rows.filter(matchesStatusAndSearch);
    var total = Math.max(1, Math.ceil(kept.length / PER_PAGE));
    if (page > total) page = total;
    var from = (page - 1) * PER_PAGE;

    rows.forEach(function (r) { r.hidden = true; });
    kept.slice(from, from + PER_PAGE).forEach(function (r) { r.hidden = false; });

    empty.hidden = kept.length > 0;
    count.textContent = kept.length
      ? 'Showing ' + (from + 1) + '–' + Math.min(from + PER_PAGE, kept.length) + ' of ' + kept.length
      : '';

    // One button per page, and the arrows stop at the ends.
    pages.textContent = '';
    for (var i = 1; i <= total; i++) {
      (function (n) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'register-page' + (n === page ? ' register-page--on' : '');
        b.textContent = n;
        if (n === page) b.setAttribute('aria-current', 'page');
        b.addEventListener('click', function () { page = n; renderTablePage(); });
        pages.appendChild(b);
      })(i);
    }
    prev.disabled = page === 1;
    next.disabled = page === total;
    document.querySelector('.register-pager').hidden = kept.length === 0;
  }

  prev.addEventListener('click', function () { if (page > 1) { page--; renderTablePage(); } });
  next.addEventListener('click', function () { page++; renderTablePage(); });

  renderTablePage();
})();
