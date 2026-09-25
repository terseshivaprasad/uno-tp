// The classic console's admin screen: taking a dashboard tile off the air for a
// window, and writing what partners are told in the bell. Nothing is saved in
// this mock - a window set here lasts until the page is reloaded - but
// everything it changes on the page, the tile rows, the notice list and the bell
// in the top bar, changes the way it would if it were.
(function () {
  var rows = document.getElementById('admRows');
  if (!rows) return;

  var featureRows = Array.prototype.slice.call(rows.querySelectorAll('tr'));
  var picks = document.getElementById('admPicks');
  var windowForm = document.getElementById('admWindowForm');
  var windowError = document.getElementById('admWindowError');
  var title = document.getElementById('admTitle');
  var summary = document.getElementById('admSummary');

  var noticeRows = document.getElementById('admNotices');
  var noticeForm = document.getElementById('admNoticeForm');
  var noticeError = document.getElementById('admNoticeError');
  var noticeHead = document.getElementById('admNoticeHead');
  var noticeDetail = document.getElementById('admNoticeDetail');
  var noticesEmpty = document.getElementById('admNoticesEmpty');

  var bellList = document.querySelector('.notices__list');
  var bellCount = document.querySelector('.notices__count');
  var bellBtn = document.getElementById('noticesBell');

  var ADMIN = 'Shivaprasad Terse';
  var made = 0;

  // The date and time boxes roll on from one to the next through date-parts.js.

  function two(n) { return ('0' + n).slice(-2); }

  // The five boxes under one label read as one moment, or as null while any part
  // is unfilled or the parts together are not a real one.
  function readWhen(id) {
    var date = document.getElementById(id + 'Date');
    var time = document.getElementById(id + 'Time');
    var dd = date.querySelector('[data-dd]').value.trim();
    var mm = date.querySelector('[data-mm]').value.trim();
    var yyyy = date.querySelector('[data-yyyy]').value.trim();
    var hh = time.querySelector('[data-hh]').value.trim();
    var mi = time.querySelector('[data-mi]').value.trim();
    if (!/^\d{1,2}$/.test(dd) || !/^\d{1,2}$/.test(mm) || !/^\d{4}$/.test(yyyy)) return null;
    if (!/^\d{1,2}$/.test(hh) || !/^\d{1,2}$/.test(mi)) return null;
    if (+hh > 23 || +mi > 59) return null;
    var d = new Date(+yyyy, +mm - 1, +dd, +hh, +mi);
    if (d.getFullYear() !== +yyyy || d.getMonth() !== +mm - 1 || d.getDate() !== +dd) return null;
    return d;
  }

  function markWhen(id, bad) {
    document.getElementById(id + 'Date').classList.toggle('is-invalid', !!bad);
    document.getElementById(id + 'Time').classList.toggle('is-invalid', !!bad);
  }

  // ---- Saying when ---------------------------------------------------------
  var DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

  function clock(d) {
    var h = d.getHours() % 12;
    return (h === 0 ? 12 : h) + ':' + two(d.getMinutes()) + ' ' + (d.getHours() < 12 ? 'AM' : 'PM');
  }

  function stamp(d) {
    return DAYS[d.getDay()] + ' ' + d.getDate() + ' ' + MONTHS[d.getMonth()] + ', ' + clock(d);
  }

  function sameDay(a, b) {
    return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
  }

  function spell(from, to) {
    return sameDay(from, to) ? stamp(from) + ' – ' + clock(to) : stamp(from) + ' – ' + stamp(to);
  }

  // Whole days as a calendar counts them, so a window late tomorrow night is
  // tomorrow and not two days off.
  function away(at) {
    var midnight = new Date();
    midnight.setHours(0, 0, 0, 0);
    var day = new Date(at.getFullYear(), at.getMonth(), at.getDate());
    var days = Math.round((day - midnight) / 86400000);
    if (days === 1) return 'tomorrow';
    if (days > 1) return 'in ' + days + ' days';
    var off = at - new Date();
    if (off <= 0) return 'on now';
    if (off < 3600000) return 'in ' + Math.max(1, Math.round(off / 60000)) + ' minutes';
    return 'in ' + Math.floor(off / 3600000) + ' hours';
  }

  function length(from, to) {
    var mins = Math.round((to - from) / 60000);
    if (mins < 60) return mins + ' minutes';
    if (mins < 1440) {
      var hours = mins / 60;
      return hours === 1 ? 'an hour' : (Math.round(hours * 10) / 10) + ' hours';
    }
    var days = Math.round(mins / 1440 * 10) / 10;
    return days === 1 ? 'a day' : days + ' days';
  }

  // The key both lists are kept in order by. It is written the way the server
  // writes it, in local time, so a row added here sorts against the rows that
  // came with the page rather than hours off them.
  function key(d) {
    return d.getFullYear() + '-' + two(d.getMonth() + 1) + '-' + two(d.getDate())
      + 'T' + two(d.getHours()) + ':' + two(d.getMinutes()) + ':' + two(d.getSeconds());
  }

  function tone(kind) {
    return kind === 'Downtime' ? 'text-danger' : kind === 'Maintenance' ? 'text-amber' : 'text-success';
  }

  function name(key) {
    var row = rows.querySelector('[data-feature="' + key + '"]');
    return row ? row.dataset.name : key;
  }

  // The tiles a window names, written out as a sentence lists them.
  function names(keys) {
    var all = keys.map(name);
    return all.length === 1 ? all[0] : all.slice(0, -1).join(', ') + ' and ' + all[all.length - 1];
  }

  // ---- The bell in the top bar ---------------------------------------------
  function countBell() {
    if (!bellList || !bellCount) return;
    var n = bellList.children.length;
    bellCount.textContent = n;
    if (bellBtn) bellBtn.setAttribute('aria-label', n + ' scheduled notices');
  }

  function bellAdd(notice) {
    if (!bellList) return;
    var li = document.createElement('li');
    li.className = 'notice';
    li.dataset.notice = notice.id;
    li.dataset.at = key(notice.at);
    li.innerHTML =
      '<div class="notice__top"><span class="notice__kind ' + tone(notice.kind) + '"></span>'
      + '<span class="notice__away"></span></div>'
      + '<div class="notice__title"></div><div class="notice__when"></div><p class="notice__detail"></p>';
    li.querySelector('.notice__kind').textContent = notice.kind;
    li.querySelector('.notice__away').textContent = notice.away;
    li.querySelector('.notice__title').textContent = notice.title;
    li.querySelector('.notice__when').textContent = notice.when;
    li.querySelector('.notice__detail').textContent = notice.detail;

    // The bell reads soonest first, so a new notice takes its place by time
    // rather than going on the end.
    var at = li.dataset.at;
    var before = Array.prototype.slice.call(bellList.children).filter(function (el) {
      return (el.dataset.at || '') > at;
    })[0];
    bellList.insertBefore(li, before || null);
    countBell();
  }

  function bellRemove(id) {
    if (!bellList) return;
    var li = bellList.querySelector('[data-notice="' + id + '"]');
    if (li) li.remove();
    countBell();
  }

  // ---- The notice list on this screen --------------------------------------
  function noticeRow(notice, source) {
    var tr = document.createElement('tr');
    tr.dataset.notice = notice.id;
    tr.dataset.source = source;
    tr.dataset.at = key(notice.at);
    tr.innerHTML =
      '<td data-label="Kind"><span class="adm-kindtag ' + tone(notice.kind) + '"></span></td>'
      + '<td data-label="Notice"><strong></strong><div class="su-table__sub"></div></td>'
      + '<td data-label="When"><span></span><div class="su-table__sub"></div></td>'
      + '<td data-label="Raised by"><span></span><div class="su-table__sub"></div></td>'
      + '<td class="su-table__act"></td>';
    tr.querySelector('.adm-kindtag').textContent = notice.kind;
    tr.querySelector('strong').textContent = notice.title;
    tr.querySelectorAll('.su-table__sub')[0].textContent = notice.detail;
    tr.querySelectorAll('td')[2].querySelector('span').textContent = notice.when;
    tr.querySelectorAll('.su-table__sub')[1].textContent = notice.away;
    tr.querySelectorAll('td')[3].querySelector('span').textContent = notice.by;
    tr.querySelectorAll('.su-table__sub')[2].textContent =
      source === 'window' ? 'with window ' + notice.id : 'just now';

    var act = tr.querySelector('.su-table__act');
    if (source === 'window') {
      act.innerHTML = '<span class="su-nolink">—</span>';
    } else {
      var remove = document.createElement('button');
      remove.type = 'button';
      remove.className = 'csi-link';
      remove.dataset.remove = '';
      remove.textContent = 'Remove';
      act.appendChild(remove);
      wireRemove(remove);
    }

    var at = tr.dataset.at;
    var before = Array.prototype.slice.call(noticeRows.children).filter(function (el) {
      return (el.dataset.at || '') > at;
    })[0];
    noticeRows.insertBefore(tr, before || null);
    syncNotices();
  }

  function syncNotices() {
    noticesEmpty.hidden = noticeRows.children.length > 0;
  }

  function wireRemove(button) {
    button.addEventListener('click', function () {
      var tr = button.closest('tr');
      var id = tr.dataset.notice;
      tr.remove();
      bellRemove(id);
      syncNotices();
      window.showToast('Notice removed. Partners no longer see it in the bell.');
    });
  }

  Array.prototype.slice.call(noticeRows.querySelectorAll('[data-remove]')).forEach(wireRemove);

  // ---- A tile's row --------------------------------------------------------
  function set(row, selector, text) {
    var el = row.querySelector(selector);
    if (el) el.textContent = text;
  }

  // What the row's action offers follows its state, and nothing else does.
  function actions(row) {
    var act = row.querySelector('[data-act]');
    act.textContent = '';
    var state = row.dataset.state;
    if (state === 'off') {
      act.innerHTML = '<span class="su-nolink">—</span>';
      return;
    }
    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'csi-link';
    if (state === 'disabled') {
      button.dataset.end = '';
      button.textContent = 'End now';
    } else if (state === 'scheduled') {
      button.dataset.cancel = '';
      button.textContent = 'Cancel';
    } else {
      button.dataset.schedule = '';
      button.textContent = 'Disable…';
    }
    act.appendChild(button);
    wireRow(row);
  }

  function clearRow(row) {
    row.dataset.state = 'available';
    row.dataset.window = '';
    var status = row.querySelector('[data-status]');
    status.textContent = 'Available';
    status.className = 'text-success';
    var statusSub = row.querySelector('[data-status-sub]');
    statusSub.textContent = 'no window set';
    statusSub.className = 'su-table__sub';
    set(row, '[data-window-when]', '—');
    set(row, '[data-window-sub]', '');
    actions(row);
  }

  function fillRow(row, w) {
    var live = w.from <= new Date();
    row.dataset.state = live ? 'disabled' : 'scheduled';
    row.dataset.window = w.id;

    var status = row.querySelector('[data-status]');
    var statusSub = row.querySelector('[data-status-sub]');
    status.textContent = live ? 'Off now' : 'Goes off ' + away(w.from);
    status.className = live ? 'text-danger' : 'text-amber';
    statusSub.textContent = live ? 'back at ' + clock(w.to) : 'on until then';
    statusSub.className = 'su-table__sub' + (live ? ' su-table__sub--urgent' : '');

    var others = w.features.filter(function (k) { return k !== row.dataset.feature; });
    set(row, '[data-window-when]', spell(w.from, w.to));
    set(row, '[data-window-sub]', length(w.from, w.to)
      + (others.length ? ' · with ' + names(others) : '')
      + ' · ' + (w.notice ? 'in the bell' : 'not announced'));
    actions(row);
  }

  function rowsOfWindow(id) {
    return featureRows.filter(function (r) { return r.dataset.window === id; });
  }

  // A window is dropped from every tile it named, and takes its notice with it.
  function drop(id, message) {
    rowsOfWindow(id).forEach(clearRow);
    var tr = noticeRows.querySelector('[data-notice="' + id + '"]');
    if (tr) tr.remove();
    bellRemove(id);
    syncNotices();
    syncPicks();
    syncSummary();
    window.showToast(message);
  }

  function wireRow(row) {
    var schedule = row.querySelector('[data-schedule]');
    if (schedule) {
      schedule.addEventListener('click', function () {
        // The row hands the tile to the form rather than opening one of its
        // own: a window often covers more than one tile.
        var pick = picks.querySelector('[data-pick="' + row.dataset.feature + '"] input');
        if (pick) pick.checked = true;
        document.getElementById('admScheduleTitle').scrollIntoView({ behavior: 'smooth', block: 'center' });
        title.focus({ preventScroll: true });
      });
    }
    var cancel = row.querySelector('[data-cancel]');
    if (cancel) {
      cancel.addEventListener('click', function () {
        drop(row.dataset.window, 'Window cancelled. Nothing goes off, and the notice is out of the bell.');
      });
    }
    var end = row.querySelector('[data-end]');
    if (end) {
      end.addEventListener('click', function () {
        drop(row.dataset.window, name(row.dataset.feature) + ' is back on for every partner.');
      });
    }
  }

  featureRows.forEach(wireRow);

  // A tile already in a window cannot be picked for another until that one is
  // ended or cancelled.
  function syncPicks() {
    Array.prototype.slice.call(picks.querySelectorAll('[data-pick]')).forEach(function (label) {
      var row = rows.querySelector('[data-feature="' + label.dataset.pick + '"]');
      var busy = row && row.dataset.state !== 'available';
      var input = label.querySelector('input');
      input.disabled = !!busy;
      if (busy) input.checked = false;
      label.classList.toggle('adm-pick--off', !!busy);
      label.title = busy ? 'Already in a window' : '';
    });
  }

  function syncSummary() {
    var off = featureRows.filter(function (r) { return r.dataset.state === 'disabled'; }).length;
    var queued = featureRows.filter(function (r) { return r.dataset.state === 'scheduled'; }).length;
    summary.textContent = (off === 0 ? 'All on' : off + ' off now') + ' · ' + queued + ' scheduled to go off.';
  }

  // ---- Setting a window ----------------------------------------------------
  function fail(box, message, field) {
    box.textContent = message;
    box.hidden = false;
    if (field) field.focus();
    return false;
  }

  windowForm.addEventListener('submit', function (e) {
    e.preventDefault();
    windowError.hidden = true;
    markWhen('admFrom', false);
    markWhen('admTo', false);

    var chosen = Array.prototype.slice.call(picks.querySelectorAll('input:checked')).map(function (i) { return i.value; });
    if (!chosen.length) return fail(windowError, 'Pick at least one tile to disable.');

    var from = readWhen('admFrom');
    if (!from) { markWhen('admFrom', true); return fail(windowError, 'Enter a complete From as DD / MM / YYYY and HH : MM.'); }
    var to = readWhen('admTo');
    if (!to) { markWhen('admTo', true); return fail(windowError, 'Enter a complete To as DD / MM / YYYY and HH : MM.'); }
    if (to <= from) { markWhen('admTo', true); return fail(windowError, 'The window has to end after it starts.'); }
    if (from < new Date()) { markWhen('admFrom', true); return fail(windowError, 'A window cannot start in the past. To take a tile off right now, set From to the next minute.'); }

    made++;
    var w = {
      id: 'W-' + two(from.getDate()) + two(from.getMonth() + 1) + '-' + two(made + 10),
      features: chosen,
      from: from,
      to: to,
      notice: title.value.trim(),
      by: ADMIN,
    };

    chosen.forEach(function (k) {
      fillRow(rows.querySelector('[data-feature="' + k + '"]'), w);
    });

    if (w.notice) {
      var notice = {
        id: w.id,
        kind: 'Downtime',
        title: w.notice,
        detail: 'Affects ' + names(chosen) + '.',
        when: spell(from, to),
        away: away(from),
        at: from,
        by: w.by,
      };
      noticeRow(notice, 'window');
      bellAdd(notice);
    }

    syncPicks();
    syncSummary();
    title.value = '';
    window.showToast(w.notice
      ? 'Window set, and partners can see it in the bell.'
      : 'Window set. Partners are not told, so only the tile will say it is off.');
  });

  document.getElementById('admWindowReset').addEventListener('click', function () {
    windowError.hidden = true;
    markWhen('admFrom', false);
    markWhen('admTo', false);
    Array.prototype.slice.call(picks.querySelectorAll('input')).forEach(function (i) { i.checked = false; });
    title.value = '';
  });

  // ---- Adding a notification ----------------------------------------------
  noticeForm.addEventListener('submit', function (e) {
    e.preventDefault();
    noticeError.hidden = true;
    markWhen('admAt', false);
    noticeHead.classList.remove('is-invalid');

    var at = readWhen('admAt');
    if (!at) { markWhen('admAt', true); return fail(noticeError, 'Enter a complete date and time as DD / MM / YYYY and HH : MM.'); }
    if (at < new Date()) { markWhen('admAt', true); return fail(noticeError, 'A notice about something already past tells nobody anything.'); }
    if (!noticeHead.value.trim()) {
      noticeHead.classList.add('is-invalid');
      return fail(noticeError, 'Write the notice partners will see.', noticeHead);
    }

    made++;
    var notice = {
      id: 'N-' + two(at.getDate()) + two(at.getMonth() + 1) + '-' + two(made + 10),
      kind: noticeForm.querySelector('[name="admNoticeKind"]:checked').value,
      title: noticeHead.value.trim(),
      detail: noticeDetail.value.trim(),
      when: stamp(at),
      away: away(at),
      at: at,
      by: ADMIN,
    };

    noticeRow(notice, 'standalone');
    bellAdd(notice);
    noticeHead.value = '';
    noticeDetail.value = '';
    window.showToast('Published. Every partner sees it in the bell now.');
  });

  document.getElementById('admNoticeReset').addEventListener('click', function () {
    noticeError.hidden = true;
    markWhen('admAt', false);
    noticeHead.value = '';
    noticeDetail.value = '';
  });

  syncPicks();
  syncNotices();
})();
