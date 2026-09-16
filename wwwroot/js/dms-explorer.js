(function () {
  var toastEl = document.getElementById('toast');
  var toastTimer = null;
  function showToast(message) {
    if (!toastEl) return;
    toastEl.textContent = message;
    toastEl.classList.add('toast--visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove('toast--visible'); }, 2600);
  }

  // The lookup box describes the number the chosen key expects.
  var byEl = document.getElementById('dmsBy');
  var noEl = document.getElementById('dmsNo');
  if (byEl && noEl) {
    byEl.addEventListener('change', function () {
      var option = byEl.options[byEl.selectedIndex];
      noEl.placeholder = option.getAttribute('data-placeholder');
      noEl.setAttribute('aria-label', option.textContent);
      noEl.focus();
    });
  }

  // The other tabs and Download all belong to boards that are not built yet.
  document.querySelectorAll('.js-dms-stub').forEach(function (btn) {
    btn.addEventListener('click', function () { showToast(btn.getAttribute('data-stub')); });
  });

  var viewer = document.getElementById('dmsViewer');
  var dataEl = document.getElementById('dmsVersions');
  if (!viewer || !dataEl) return;

  // Every version of every document, keyed d{document}v{version}.
  var versions = JSON.parse(dataEl.textContent);

  var panels = {
    current: document.getElementById('dmsCurrent'),
    history: document.getElementById('dmsHistory'),
  };
  var viewButtons = document.querySelectorAll('#dmsViews [data-view]');
  var pills = document.querySelectorAll('#dmsClasses .dms-pill');
  var scopeEl = document.getElementById('dmsScope');
  var scopeNameEl = document.getElementById('dmsScopeName');
  var fileEl = document.getElementById('dmsFile');
  var supersededEl = document.getElementById('dmsSuperseded');
  var posEl = document.getElementById('dmsPos');
  var extEl = document.getElementById('dmsExt');
  var pageEl = document.getElementById('dmsPage');
  var zoomEl = document.getElementById('dmsZoom');
  var zoomOut = document.getElementById('dmsZoomOut');
  var zoomIn = document.getElementById('dmsZoomIn');
  var prevBtn = document.getElementById('dmsPrev');
  var nextBtn = document.getElementById('dmsNext');
  var openCurrentBtn = document.getElementById('dmsOpenCurrent');
  var stacked = window.matchMedia('(max-width: 1200px)');

  var view = panels.history.hidden ? 'current' : 'history';
  var cls = 'all';
  var scope = null; // a document key when History is narrowed to one document
  var openKey = viewer.getAttribute('data-open');
  var zoom = 100;
  var rotation = 0;

  function rowsOf(panel) {
    return Array.prototype.slice.call(panel.querySelectorAll('.dms-row:not(.dms-row--head)'));
  }

  // The rows the viewer can step through: those in the showing panel that open a file.
  function openable() {
    return rowsOf(panels[view]).filter(function (row) { return !row.hidden && row.classList.contains('dms-row--file'); });
  }

  function currentKeyOf(docKey) {
    var key = null;
    Object.keys(versions).forEach(function (k) {
      if (versions[k].docKey === docKey && versions[k].current) key = k;
    });
    return key;
  }

  function drawPage() {
    zoomEl.textContent = zoom + '%';
    zoomOut.disabled = zoom <= 50;
    zoomIn.disabled = zoom >= 200;
    pageEl.style.transform = 'rotate(' + rotation + 'deg) scale(' + zoom / 100 + ')';
  }

  function drawPosition() {
    var rows = openable();
    var i = rows.findIndex(function (row) { return row.getAttribute('data-key') === openKey; });
    posEl.textContent = i === -1 ? '' : (i + 1) + ' of ' + rows.length;
    prevBtn.disabled = i <= 0;
    nextBtn.disabled = i === -1 || i >= rows.length - 1;
  }

  // Marks the open version in both panels: the current table and its upload in History.
  function drawOpenRows() {
    [panels.current, panels.history].forEach(function (panel) {
      rowsOf(panel).forEach(function (row) {
        var action = row.querySelector('.dms-action');
        var on = row.classList.contains('dms-row--file') && row.getAttribute('data-key') === openKey;
        row.classList.toggle('dms-row--open', on);
        if (!action) return;
        action.textContent = on ? 'Open •' : 'View';
        if (on) action.setAttribute('aria-current', 'true');
        else action.removeAttribute('aria-current');
      });
    });
  }

  function open(key) {
    var v = versions[key];
    if (!v) return;
    openKey = key;

    fileEl.textContent = v.file;
    extEl.textContent = v.file.split('.').pop().toUpperCase();
    supersededEl.hidden = v.current;
    openCurrentBtn.hidden = v.current;
    var factsEl = document.getElementById('dmsFacts');
    factsEl.innerHTML = '';
    v.facts.forEach(function (f) {
      var dt = document.createElement('dt');
      var dd = document.createElement('dd');
      dt.textContent = f.label;
      dd.textContent = f.value;
      factsEl.appendChild(dt);
      factsEl.appendChild(dd);
    });
    viewer.querySelectorAll('[data-field]').forEach(function (el) {
      el.textContent = v[el.getAttribute('data-field')];
    });

    zoom = 100;
    rotation = 0;
    drawPage();
    drawOpenRows();
    drawPosition();
  }

  function openAndShow(key) {
    open(key);
    // Once the viewer sits under the table it is out of sight, so bring it up.
    if (stacked.matches) viewer.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // Applies the class pill (both panels) and the one-document scope (History only),
  // drops day or holder groups left empty, and keeps the viewer on a listed row.
  function applyFilters() {
    ['current', 'history'].forEach(function (name) {
      var panel = panels[name];
      var shown = 0;
      rowsOf(panel).forEach(function (row) {
        row.hidden = (cls !== 'all' && row.getAttribute('data-class') !== cls)
          || (name === 'history' && scope !== null && row.getAttribute('data-doc') !== scope);
        if (!row.hidden) shown++;
      });
      panel.querySelectorAll('.dms-group').forEach(function (group) {
        var n = group.querySelectorAll('.dms-row:not([hidden])').length;
        var countEl = group.querySelector('[data-count]');
        group.hidden = n === 0;
        countEl.textContent = n + ' ' + countEl.getAttribute('data-unit') + (n === 1 ? '' : 's');
      });
      panel.querySelector('.dms-table__empty').hidden = shown !== 0;
    });

    scopeEl.hidden = scope === null;
    if (scope !== null) {
      var row = panels.current.querySelector('.dms-row[data-doc="' + scope + '"]');
      scopeNameEl.textContent = row.closest('.dms-group').querySelector('[role="rowheader"]').textContent
        + ' · ' + row.querySelector('.dms-doc').textContent;
    }

    var rows = openable();
    var listed = rows.some(function (row) { return row.getAttribute('data-key') === openKey; });
    if (!listed && view === 'current') {
      // A superseded version has no row here; fall back to the version in force.
      var current = currentKeyOf(versions[openKey].docKey);
      listed = rows.some(function (row) { return row.getAttribute('data-key') === current; });
      if (listed) { open(current); return; }
    }
    if (!listed && rows.length) { open(rows[0].getAttribute('data-key')); return; }
    drawPosition();
  }

  function showView(name) {
    view = name;
    panels.current.hidden = name !== 'current';
    panels.history.hidden = name !== 'history';
    viewButtons.forEach(function (btn) {
      var on = btn.getAttribute('data-view') === name;
      btn.classList.toggle('seg-toggle__opt--active', on);
      btn.setAttribute('aria-pressed', on ? 'true' : 'false');
    });
    pills.forEach(function (pill) {
      var n = pill.querySelector('[data-' + name + ']');
      n.textContent = n.getAttribute('data-' + name);
    });

    // Keep the view in the address so a link to a number's history opens on it.
    try {
      var url = new URL(window.location.href);
      if (name === 'history') url.searchParams.set('view', 'history');
      else url.searchParams.delete('view');
      window.history.replaceState(null, '', url);
    } catch (e) { /* the address just stays as it was */ }

    applyFilters();
  }

  viewButtons.forEach(function (btn) {
    btn.addEventListener('click', function () {
      if (btn.getAttribute('data-view') === view) return;
      scope = null;
      showView(btn.getAttribute('data-view'));
    });
  });

  [panels.current, panels.history].forEach(function (panel) {
    panel.addEventListener('click', function (e) {
      var row = e.target.closest('.dms-row--file');
      if (!row) return;
      var key = row.getAttribute('data-key');
      if (key !== openKey) openAndShow(key);
    });
  });

  pills.forEach(function (pill) {
    pill.addEventListener('click', function () {
      cls = pill.getAttribute('data-class');
      pills.forEach(function (p) {
        var on = p === pill;
        p.classList.toggle('dms-pill--active', on);
        p.setAttribute('aria-pressed', on ? 'true' : 'false');
      });
      applyFilters();
    });
  });

  // "History" beside Versions narrows History to the open document.
  document.getElementById('dmsDocHistory').addEventListener('click', function () {
    scope = versions[openKey].docKey;
    showView('history');
    panels.history.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });
  document.getElementById('dmsScopeClear').addEventListener('click', function () {
    scope = null;
    applyFilters();
  });

  openCurrentBtn.addEventListener('click', function () {
    open(currentKeyOf(versions[openKey].docKey));
  });

  prevBtn.addEventListener('click', function () { step(-1); });
  nextBtn.addEventListener('click', function () { step(1); });
  function step(by) {
    var rows = openable();
    var i = rows.findIndex(function (row) { return row.getAttribute('data-key') === openKey; });
    var target = rows[i + by];
    if (i !== -1 && target) open(target.getAttribute('data-key'));
  }

  zoomOut.addEventListener('click', function () { zoom = Math.max(50, zoom - 25); drawPage(); });
  zoomIn.addEventListener('click', function () { zoom = Math.min(200, zoom + 25); drawPage(); });
  document.getElementById('dmsRotate').addEventListener('click', function () { rotation = (rotation + 90) % 360; drawPage(); });

  viewer.querySelectorAll('.js-dms-file').forEach(function (btn) {
    btn.addEventListener('click', function () {
      showToast(btn.getAttribute('data-verb') + ' for ' + versions[openKey].file + ' is not built yet.');
    });
  });

  drawPage();
  drawPosition();
})();
