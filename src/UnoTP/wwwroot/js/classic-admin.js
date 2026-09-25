// The classic console's admin screen: taking a dashboard tile off the air for a
// window, and writing what partners are told in the bell. The forms are checked
// here and then posted: the backend keeps the window or the notice, and the page
// comes back with the schedule as it now stands.
(function () {
  var rows = document.getElementById('admRows');
  if (!rows) return;

  var featureRows = Array.prototype.slice.call(rows.querySelectorAll('tr'));
  var picks = document.getElementById('admPicks');
  var windowForm = document.getElementById('admWindowForm');
  var windowError = document.getElementById('admWindowError');
  var title = document.getElementById('admTitle');

  var noticeRows = document.getElementById('admNotices');
  var noticeForm = document.getElementById('admNoticeForm');
  var noticeError = document.getElementById('admNoticeError');
  var noticeHead = document.getElementById('admNoticeHead');
  var noticeDetail = document.getElementById('admNoticeDetail');
  var noticesEmpty = document.getElementById('admNoticesEmpty');

  // The date and time boxes roll on from one to the next through date-parts.js.

  function two(n) { return ('0' + n).slice(-2); }

  // A moment as the server reads it: yyyy-MM-ddTHH:mm, local time.
  function local(d) {
    return d.getFullYear() + '-' + two(d.getMonth() + 1) + '-' + two(d.getDate()) + 'T' + two(d.getHours()) + ':' + two(d.getMinutes());
  }

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

  function syncNotices() {
    noticesEmpty.hidden = noticeRows.children.length > 0;
  }

  // Removing a notice, and ending or cancelling a window, are posts: the backend
  // does it, and the page comes back with the schedule as it now stands.

  // ---- A tile's row --------------------------------------------------------
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

    document.getElementById('admFromValue').value = local(from);
    document.getElementById('admToValue').value = local(to);
    windowForm.submit();
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

    document.getElementById('admAtValue').value = local(at);
    noticeForm.submit();
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
