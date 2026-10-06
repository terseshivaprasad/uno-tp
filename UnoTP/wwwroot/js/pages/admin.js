// The classic console's admin screen: taking a dashboard tile off the air for a
// window, and writing what partners are told in the bell. The forms are checked
// here and then posted: the backend keeps the window or the notice, and the page
// comes back with the schedule as it now stands.
(function () {
  var rows = document.getElementById('adminRows');
  if (!rows) return;

  var featureRows = Array.prototype.slice.call(rows.querySelectorAll('tr'));
  var picks = document.getElementById('adminPicks');
  var windowForm = document.getElementById('adminWindowForm');
  var windowError = document.getElementById('adminWindowError');
  var title = document.getElementById('adminTitle');

  var noticeRows = document.getElementById('adminNotices');
  var noticeForm = document.getElementById('adminNoticeForm');
  var noticeError = document.getElementById('adminNoticeError');
  var noticeHead = document.getElementById('adminNoticeHead');
  var noticeDetail = document.getElementById('adminNoticeDetail');
  var noticesEmpty = document.getElementById('adminNoticesEmpty');

  // The date and time boxes roll on from one to the next through date-input.js.

  // Pads a number to two digits: 7 -> "07".
  function padToTwoDigits(n) { return ('0' + n).slice(-2); }

  // A moment as the server reads it: yyyy-MM-ddTHH:mm, local time.
  function toServerDateTime(d) {
    return d.getFullYear() + '-' + padToTwoDigits(d.getMonth() + 1) + '-' + padToTwoDigits(d.getDate()) + 'T' + padToTwoDigits(d.getHours()) + ':' + padToTwoDigits(d.getMinutes());
  }

  // The five boxes under one label read as one moment, or as null while any part
  // is unfilled or the parts together are not a real one.
  function readDateTimeBoxes(id) {
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

  // Marks (or clears) a date and time box pair as invalid.
  function markDateTimeInvalid(id, bad) {
    document.getElementById(id + 'Date').classList.toggle('is-invalid', !!bad);
    document.getElementById(id + 'Time').classList.toggle('is-invalid', !!bad);
  }

  // Shows the "no notices" line when the notice list is empty.
  function showNoNoticesMessage() {
    noticesEmpty.hidden = noticeRows.children.length > 0;
  }

  // Removing a notice, and ending or cancelling a window, are posts: the backend
  // does it, and the page comes back with the schedule as it now stands.

  // ---- A tile's row --------------------------------------------------------
  function wireTileRow(row) {
    var schedule = row.querySelector('[data-schedule]');
    if (schedule) {
      schedule.addEventListener('click', function () {
        // The row hands the tile to the form rather than opening one of its
        // own: a window often covers more than one tile.
        var pick = picks.querySelector('[data-pick="' + row.dataset.feature + '"] input');
        if (pick) pick.checked = true;
        document.getElementById('adminScheduleTitle').scrollIntoView({ behavior: 'smooth', block: 'center' });
        title.focus({ preventScroll: true });
      });
    }
  }

  featureRows.forEach(wireTileRow);

  // A tile already in a window cannot be picked for another until that one is
  // ended or cancelled.
  function disableTilesAlreadyInWindow() {
    Array.prototype.slice.call(picks.querySelectorAll('[data-pick]')).forEach(function (label) {
      var row = rows.querySelector('[data-feature="' + label.dataset.pick + '"]');
      var busy = row && row.dataset.state !== 'available';
      var input = label.querySelector('input');
      input.disabled = !!busy;
      if (busy) input.checked = false;
      label.classList.toggle('admin-pick--off', !!busy);
      label.title = busy ? 'Already in a window' : '';
    });
  }

  // ---- Setting a window ----------------------------------------------------
  function showFormError(box, message, field) {
    box.textContent = message;
    box.hidden = false;
    if (field) field.focus();
    return false;
  }

  windowForm.addEventListener('submit', function (e) {
    e.preventDefault();
    windowError.hidden = true;
    markDateTimeInvalid('adminFrom', false);
    markDateTimeInvalid('adminTo', false);

    var chosen = Array.prototype.slice.call(picks.querySelectorAll('input:checked')).map(function (i) { return i.value; });
    if (!chosen.length) return showFormError(windowError, message('Admin.PickATile'));

    var from = readDateTimeBoxes('adminFrom');
    if (!from) { markDateTimeInvalid('adminFrom', true); return showFormError(windowError, message('Admin.FromIncomplete')); }
    var to = readDateTimeBoxes('adminTo');
    if (!to) { markDateTimeInvalid('adminTo', true); return showFormError(windowError, message('Admin.ToIncomplete')); }
    if (to <= from) { markDateTimeInvalid('adminTo', true); return showFormError(windowError, message('Admin.EndsBeforeItStarts')); }
    if (from < new Date()) { markDateTimeInvalid('adminFrom', true); return showFormError(windowError, 'A window cannot start in the past. To take a tile off right now, set From to the next minute.'); }

    document.getElementById('adminFromValue').value = toServerDateTime(from);
    document.getElementById('adminToValue').value = toServerDateTime(to);
    windowForm.submit();
  });

  document.getElementById('adminWindowReset').addEventListener('click', function () {
    windowError.hidden = true;
    markDateTimeInvalid('adminFrom', false);
    markDateTimeInvalid('adminTo', false);
    Array.prototype.slice.call(picks.querySelectorAll('input')).forEach(function (i) { i.checked = false; });
    title.value = '';
  });

  // ---- Adding a notification ----------------------------------------------
  noticeForm.addEventListener('submit', function (e) {
    e.preventDefault();
    noticeError.hidden = true;
    markDateTimeInvalid('adminAt', false);
    noticeHead.classList.remove('is-invalid');

    var at = readDateTimeBoxes('adminAt');
    if (!at) { markDateTimeInvalid('adminAt', true); return showFormError(noticeError, message('Admin.NoticeTimeIncomplete')); }
    if (at < new Date()) { markDateTimeInvalid('adminAt', true); return showFormError(noticeError, 'A notice about something already past tells nobody anything.'); }
    if (!noticeHead.value.trim()) {
      noticeHead.classList.add('is-invalid');
      return showFormError(noticeError, message('Admin.NoticeHeadingMissing'), noticeHead);
    }

    document.getElementById('adminAtValue').value = toServerDateTime(at);
    noticeForm.submit();
  });

  document.getElementById('adminNoticeReset').addEventListener('click', function () {
    noticeError.hidden = true;
    markDateTimeInvalid('adminAt', false);
    noticeHead.value = '';
    noticeDetail.value = '';
  });

  disableTilesAlreadyInWindow();
  showNoNoticesMessage();
})();
