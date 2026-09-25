// The classic Short URL page: sending a link against a pending application, and
// the filter, search and pages over the list. The link itself is never shown -
// it goes to the investor's contacts on the application and nowhere else.
(function () {
  var table = document.getElementById('suRows');
  if (!table) return;

  // ---- The links sent: filter, search, pages -------------------------------
  var PER_PAGE = 8;
  var rows = Array.prototype.slice.call(document.querySelectorAll('#suRows tr'));
  var pills = Array.prototype.slice.call(document.querySelectorAll('.su-pill'));
  var search = document.getElementById('suSearch');
  var empty = document.getElementById('suEmpty');
  var count = document.getElementById('suCount');
  var pages = document.getElementById('suPages');
  var prev = document.getElementById('suPrev');
  var next = document.getElementById('suNext');
  var state = 'all';
  var page = 1;

  // Each pill carries how many links are in that state, counted once up front.
  pills.forEach(function (pill) {
    var key = pill.dataset.state;
    var n = key === 'all' ? rows.length : rows.filter(function (r) { return r.dataset.state === key; }).length;
    pill.querySelector('.su-pill__n').textContent = n;
    pill.addEventListener('click', function () {
      pills.forEach(function (p) { p.classList.toggle('su-pill--on', p === pill); });
      state = key;
      page = 1;
      render();
    });
  });

  search.addEventListener('input', function () { page = 1; render(); });

  // Regenerating a link in the list swaps its code, restarts its clock and puts
  // it back to not opened; the pill counts follow.
  function recount() {
    pills.forEach(function (pill) {
      var key = pill.dataset.state;
      var n = key === 'all' ? rows.length : rows.filter(function (r) { return r.dataset.state === key; }).length;
      pill.querySelector('.su-pill__n').textContent = n;
    });
  }

  rows.forEach(function (row) {
    var regen = row.querySelector('[data-regen]');
    if (!regen) return;
    regen.addEventListener('click', function () {
      row.querySelector('[data-label="Sent"]').textContent = 'just now';
      row.querySelector('[data-label="Expires"]').textContent = 'in ' + regen.dataset.validity;
      var state = row.querySelector('[data-label="State"]');
      state.textContent = 'Not opened';
      state.className = 'text-muted';
      row.dataset.state = 'open';
      recount();
      render();
      window.showToast('New link generated — the old one stops working.');
    });
  });

  function matches(row) {
    var q = search.value.trim().toLowerCase();
    return (state === 'all' || row.dataset.state === state)
      && (!q || row.dataset.search.indexOf(q) !== -1);
  }

  function render() {
    var kept = rows.filter(matches);
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

  render();
})();
