(function () {
  var rowsEl = document.getElementById('ctRows');
  if (!rowsEl) return;

  var rows = Array.prototype.slice.call(rowsEl.querySelectorAll('.ct-row'));
  var searchEl = document.getElementById('ctSearch');
  var sourceEl = document.getElementById('ctSource');
  var statusEl = document.getElementById('ctStatus');
  var requestedEl = document.getElementById('ctRequested');
  var emptyEl = document.getElementById('ctEmpty');
  var summaryEl = document.getElementById('ctSummary');
  var pagerEl = document.getElementById('ctPager');
  var kpis = document.querySelectorAll('#ctKpis .ct-kpi');
  var pageSize = parseInt(pagerEl.getAttribute('data-page-size'), 10) || 6;
  var page = 1;

  var toastEl = document.getElementById('toast');
  var toastTimer = null;
  function showToast(message) {
    if (!toastEl) return;
    toastEl.textContent = message;
    toastEl.classList.add('toast--visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove('toast--visible'); }, 2600);
  }

  function matches(row) {
    var q = searchEl.value.trim().toLowerCase();
    var source = sourceEl.value;
    var status = statusEl.value;
    var requested = requestedEl.value;
    var bucket = row.getAttribute('data-bucket');

    if (q && row.getAttribute('data-search').indexOf(q) === -1) return false;
    if (source !== 'all' && row.getAttribute('data-source') !== source) return false;
    if (status === 'attention' && bucket === 'onfile') return false;
    if (status !== 'all' && status !== 'attention' && bucket !== status) return false;
    if (requested !== 'any' && parseInt(row.getAttribute('data-days'), 10) > parseInt(requested, 10)) return false;
    return true;
  }

  function matchingRows() {
    return rows.filter(matches);
  }

  function pageButton(label, target, opts) {
    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'ct-page' + (opts && opts.active ? ' ct-page--active' : '');
    btn.textContent = label;
    if (opts && opts.disabled) btn.disabled = true;
    if (opts && opts.active) btn.setAttribute('aria-current', 'page');
    btn.addEventListener('click', function () { page = target; render(); });
    return btn;
  }

  function render() {
    var matched = matchingRows();
    var pages = Math.max(1, Math.ceil(matched.length / pageSize));
    if (page > pages) page = pages;

    var start = (page - 1) * pageSize;
    var shown = matched.slice(start, start + pageSize);
    rows.forEach(function (row) { row.hidden = shown.indexOf(row) === -1; });

    emptyEl.hidden = matched.length !== 0;
    summaryEl.textContent = 'Showing ' + shown.length + ' of ' + matched.length + ' records';

    // Board 12 shows three page numbers at a time between Prev and Next.
    pagerEl.innerHTML = '';
    pagerEl.hidden = matched.length === 0;
    var first = Math.max(1, Math.min(page - 1, pages - 2));
    var last = Math.min(pages, first + 2);
    pagerEl.appendChild(pageButton('Prev', page - 1, { disabled: page === 1 }));
    for (var p = first; p <= last; p++) {
      pagerEl.appendChild(pageButton(String(p), p, { active: p === page }));
    }
    pagerEl.appendChild(pageButton('Next', page + 1, { disabled: page === pages }));

    kpis.forEach(function (kpi) {
      var on = kpi.getAttribute('data-bucket') === statusEl.value;
      kpi.classList.toggle('ct-kpi--active', on);
      kpi.setAttribute('aria-pressed', on ? 'true' : 'false');
    });
  }

  function refilter() { page = 1; render(); }

  searchEl.addEventListener('input', refilter);
  [sourceEl, statusEl, requestedEl].forEach(function (el) { el.addEventListener('change', refilter); });

  // A KPI tile filters to its bucket; pressing the active tile again clears it.
  kpis.forEach(function (kpi) {
    kpi.addEventListener('click', function () {
      var bucket = kpi.getAttribute('data-bucket');
      statusEl.value = statusEl.value === bucket ? 'all' : bucket;
      refilter();
    });
  });

  document.getElementById('ctClear').addEventListener('click', function () {
    searchEl.value = '';
    sourceEl.value = 'all';
    statusEl.value = 'all';
    requestedEl.value = 'any';
    refilter();
  });

  // New request, resend, upload and renew all open the Board 13 dialogs.
  document.querySelectorAll('.js-ct-dialog').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var what = btn.getAttribute('data-dialog');
      var app = btn.getAttribute('data-app');
      showToast('“' + what + '”' + (app ? ' for ' + app : '') + ' opens a Board 13 dialog — not built yet.');
    });
  });

  // Export the rows that match the current filters (all pages, not just this one).
  document.getElementById('ctExport').addEventListener('click', function () {
    var header = Array.prototype.map.call(
      document.querySelectorAll('.ct-row--head > div'),
      function (cell) { return cell.textContent.trim(); }
    ).slice(0, -1);
    var lines = [header];
    matchingRows().forEach(function (row) {
      var cells = Array.prototype.slice.call(row.children, 0, -1);
      lines.push(cells.map(function (cell) {
        return Array.prototype.map.call(cell.querySelectorAll('div, span'), function (el) {
          return el.children.length ? '' : el.textContent.trim();
        }).filter(Boolean).join(' · ');
      }));
    });
    var csv = lines.map(function (line) {
      return line.map(function (v) { return '"' + v.replace(/"/g, '""') + '"'; }).join(',');
    }).join('\r\n');

    var link = document.createElement('a');
    link.href = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' }));
    link.download = 'dpdp-consent-register.csv';
    document.body.appendChild(link);
    link.click();
    link.remove();
    showToast('Exported ' + (lines.length - 1) + ' records.');
  });

  render();
})();
