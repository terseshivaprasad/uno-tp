(function () {
  var form = document.getElementById('dmsuForm');
  if (!form) return;

  var toastEl = document.getElementById('toast');
  var toastTimer = null;
  function showToast(message) {
    toastEl.textContent = message;
    toastEl.classList.add('toast--visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove('toast--visible'); }, 2600);
  }
  document.querySelectorAll('.js-dms-stub').forEach(function (btn) {
    btn.addEventListener('click', function () { showToast(btn.getAttribute('data-stub')); });
  });

  var MAX_MB = 5;
  var ACCEPT = ['pdf', 'jpg', 'jpeg', 'png', 'tif', 'tiff'];
  var filed = JSON.parse(document.getElementById('dmsuFiled').textContent);

  var $ = function (id) { return document.getElementById(id); };
  var control = $('dmsuControl');
  var app = $('dmsuApp');
  var holder = $('dmsuHolder');
  var docType = $('dmsuDocType');
  var subType = $('dmsuSubType');
  var fdType = $('dmsuFdType');
  var otherType = $('dmsuOtherType');
  var fileInput = $('dmsuFile');
  var drop = $('dmsuDrop');
  var notice = $('dmsuNotice');
  var done = $('dmsuDone');
  var aside = form.parentNode.querySelector('.dmsu-aside');
  var fields = Array.prototype.slice.call(form.querySelectorAll('.dmsu-grid .inv-field'));
  var fileField = form.querySelector('.dmsu-file-field');
  var file = null;

  function kind() {
    return form.querySelector('input[name="kind"]:checked').value;
  }

  function selectedSub() {
    return subType.value ? subType.options[subType.selectedIndex] : null;
  }

  // Shows the fields that belong to the chosen kind, and the reference and expiry
  // fields only for a sub-type that has them. Hidden fields are disabled so they
  // are neither checked nor sent.
  function drawFields() {
    var k = kind();
    var sub = selectedSub();
    fields.forEach(function (field) {
      var needs = field.getAttribute('data-needs');
      var on = field.getAttribute('data-kinds').split(' ').indexOf(k) !== -1
        && (!needs || (sub && sub.getAttribute('data-' + needs) === '1'));
      field.hidden = !on;
      field.querySelectorAll('input, select').forEach(function (el) {
        if (el === subType) el.disabled = !on || !docType.value;
        else el.disabled = !on;
      });
    });
  }

  function drawSubTypes() {
    var group = docType.value;
    var matches = [];
    Array.prototype.forEach.call(subType.options, function (o, i) {
      if (i === 0) return;
      o.hidden = o.getAttribute('data-group') !== group;
      if (!o.hidden) matches.push(o);
    });
    subType.options[0].textContent = group ? 'Select' : 'Choose a doc type first';
    // A doc type with a single sub-type (PAN, photograph) needs no second choice.
    subType.value = matches.length === 1 ? matches[0].value : '';
  }

  function pad(n) { return (n < 10 ? '0' : '') + n; }

  function token(text) {
    return text.replace(/[^A-Za-z0-9 ]/g, ' ').split(/\s+/).filter(Boolean)
      .map(function (w) { return w.charAt(0).toUpperCase() + w.slice(1); }).join('');
  }

  // What the upload would be filed as: class, holder type, type and file name.
  function describe() {
    var k = kind();
    var d = { cls: 'KYC', holder: null, code: '00', type: null, token: null };
    if (k === 'kyc') {
      var h = holder.value ? holder.options[holder.selectedIndex] : null;
      var sub = selectedSub();
      d.holder = h ? h.value : null;
      d.code = h ? h.getAttribute('data-code') : '··';
      d.type = sub ? sub.getAttribute('data-label') : null;
      d.token = sub ? sub.getAttribute('data-token') : null;
    } else if (k === 'fd') {
      var t = fdType.value ? fdType.options[fdType.selectedIndex] : null;
      d.cls = 'FD';
      d.type = t ? t.value : null;
      d.token = t ? t.getAttribute('data-token') : null;
    } else {
      d.cls = 'Open';
      d.type = otherType.value.trim() || null;
      d.token = d.type ? token(d.type) : null;
    }
    var now = new Date();
    var ext = file ? file.name.split('.').pop().toLowerCase() : null;
    d.file = (control.value.trim() || '······') + '_' + d.code + '_' + (d.token || '···') + '__'
      + pad(now.getDate()) + pad(now.getMonth() + 1) + now.getFullYear() + (ext ? '.' + ext : '');
    return d;
  }

  // The document this upload would replace: the same holder type and type under the
  // same control number. FD and Open documents are matched on type alone.
  function existing(d) {
    if (control.value.trim() !== filed.controlNo || !d.type) return null;
    return filed.documents.find(function (doc) {
      if (doc.type.toLowerCase() !== d.type.toLowerCase()) return false;
      return kind() !== 'kyc' || doc.holder === d.holder;
    }) || null;
  }

  function drawPreview() {
    var d = describe();
    var prior = existing(d);
    $('dmsuPreview').textContent = d.file;
    $('dmsuClass').textContent = d.cls;
    $('dmsuType').textContent = d.type || '—';
    $('dmsuHolderOut').textContent = kind() === 'kyc' ? (d.holder || '—') : 'Not holder-specific';
    $('dmsuVersion').textContent = prior
      ? 'version ' + (prior.version + 1) + ' · replaces version ' + prior.version
      : (d.type && control.value.trim() ? 'version 1' : '—');

    var known = control.value.trim() === filed.controlNo;
    notice.hidden = !known;
    notice.classList.toggle('callout--warn', !!prior);
    notice.classList.toggle('callout--info', !prior);
    if (prior) {
      notice.textContent = 'Control ' + filed.controlNo + ' already holds ' + d.type
        + (kind() === 'kyc' ? ' for ' + d.holder : '') + ' (' + prior.file + ', version ' + prior.version
        + '). Uploading files version ' + (prior.version + 1) + '; version ' + prior.version + ' stays viewable under History.';
    } else if (known) {
      notice.textContent = 'Control ' + filed.controlNo + ' is on application ' + filed.applicationNo
        + ' and holds ' + filed.documents.length + ' documents.';
    }
  }

  function setError(field, message) {
    var hint = field.querySelector('.inv-field__hint');
    var control = field.querySelector('.inv-control');
    if (!hint.hasAttribute('data-default')) hint.setAttribute('data-default', hint.hidden ? '' : hint.textContent);
    if (control) control.classList.toggle('inv-control--error', !!message);
    field.classList.toggle('dmsu-file-field--error', !!message && field === fileField);
    hint.classList.toggle('inv-field__hint--error', !!message);
    var fallback = hint.getAttribute('data-default');
    hint.textContent = message || fallback;
    hint.hidden = !message && !fallback;
  }

  function checkFile() {
    if (!file) return 'Choose the document to upload';
    var ext = file.name.split('.').pop().toLowerCase();
    if (ACCEPT.indexOf(ext) === -1) return 'Upload a PDF, JPG, PNG or TIFF';
    if (file.size > MAX_MB * 1024 * 1024) return 'The file is over ' + MAX_MB + ' MB';
    return null;
  }

  function validate() {
    var first = null;
    fields.forEach(function (field) {
      if (field.hidden) { setError(field, null); return; }
      var el = field.querySelector('input, select');
      var value = el.value.trim();
      var message = null;
      if (el.hasAttribute('data-required') && !value) message = el.getAttribute('data-required');
      else if (value && el.hasAttribute('data-pattern') && !new RegExp(el.getAttribute('data-pattern')).test(value)) {
        message = el.getAttribute('data-pattern-message');
      }
      setError(field, message);
      if (message && !first) first = el;
    });
    var fileMessage = checkFile();
    setError(fileField, fileMessage);
    if (fileMessage && !first) first = fileInput;
    return first;
  }

  function sizeText(bytes) {
    return bytes < 1024 * 1024 ? Math.max(1, Math.round(bytes / 1024)) + ' KB' : (bytes / 1024 / 1024).toFixed(1) + ' MB';
  }

  function setFile(f) {
    file = f || null;
    $('dmsuFileName').textContent = file ? file.name : 'Choose a file or drop it here';
    $('dmsuFileMeta').textContent = file ? sizeText(file.size) : 'PDF, JPG, PNG or TIFF · up to ' + MAX_MB + ' MB';
    $('dmsuFileChange').hidden = !file;
    drop.classList.toggle('dmsu-file--chosen', !!file);
    setError(fileField, file ? checkFile() : null);
    drawPreview();
  }

  form.querySelectorAll('input[name="kind"]').forEach(function (radio) {
    radio.addEventListener('change', function () {
      fields.forEach(function (field) { setError(field, null); });
      drawFields();
      drawPreview();
    });
  });

  docType.addEventListener('change', function () { drawSubTypes(); drawFields(); drawPreview(); });
  subType.addEventListener('change', function () { drawFields(); drawPreview(); });

  // Filling in the control number of a filing brings its application number with it.
  control.addEventListener('input', function () {
    if (control.value.trim() === filed.controlNo && !app.value) {
      app.value = filed.applicationNo;
      setError(app.closest('.inv-field'), null);
    }
  });

  form.querySelectorAll('.dmsu-upper').forEach(function (el) {
    el.addEventListener('input', function () { el.value = el.value.toUpperCase(); });
  });

  form.addEventListener('input', function (e) {
    var field = e.target.closest('.inv-field');
    if (field && field.querySelector('.inv-control--error')) setError(field, null);
    drawPreview();
  });
  form.addEventListener('change', drawPreview);

  fileInput.addEventListener('change', function () { setFile(fileInput.files[0]); });
  ['dragenter', 'dragover'].forEach(function (type) {
    drop.addEventListener(type, function (e) { e.preventDefault(); drop.classList.add('dmsu-file--over'); });
  });
  ['dragleave', 'drop'].forEach(function (type) {
    drop.addEventListener(type, function (e) { e.preventDefault(); drop.classList.remove('dmsu-file--over'); });
  });
  drop.addEventListener('drop', function (e) {
    if (e.dataTransfer.files.length) setFile(e.dataTransfer.files[0]);
  });

  function clearForm(keepNumbers) {
    var keep = { control: control.value, app: app.value };
    var k = kind();
    form.reset();
    form.querySelector('input[name="kind"][value="' + k + '"]').checked = true;
    if (keepNumbers) { control.value = keep.control; app.value = keep.app; }
    drawSubTypes();
    fields.forEach(function (field) { setError(field, null); });
    setFile(null);
    drawFields();
    drawPreview();
  }

  $('dmsuClear').addEventListener('click', function () { clearForm(false); });

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var invalid = validate();
    if (invalid) { invalid.focus(); return; }

    var d = describe();
    var prior = existing(d);
    var no = control.value.trim();
    $('dmsuDoneText').textContent = d.file + ' is filed against control ' + no
      + ' and waits in the sync queue until DMS takes it.';
    var facts = [
      ['Class · type', d.cls + ' · ' + d.type],
      ['Holder type', kind() === 'kyc' ? d.holder : 'Not holder-specific'],
      ['Version', prior ? (prior.version + 1) + ' · version ' + prior.version + ' kept under History' : '1'],
      ['File', file.name + ' · ' + sizeText(file.size)],
    ];
    var dl = $('dmsuDoneFacts');
    dl.innerHTML = '';
    facts.forEach(function (f) {
      var dt = document.createElement('dt');
      var dd = document.createElement('dd');
      dt.textContent = f[0];
      dd.textContent = f[1];
      dl.appendChild(dt);
      dl.appendChild(dd);
    });
    $('dmsuView').href = '/Apps/DmsExplorer?no=' + encodeURIComponent(no);
    $('dmsuView').textContent = 'View documents for ' + no;
    form.hidden = true;
    aside.hidden = true;
    done.hidden = false;
    done.focus();
  });

  $('dmsuAgain').addEventListener('click', function () {
    clearForm(true);
    done.hidden = true;
    aside.hidden = false;
    form.hidden = false;
    control.focus();
  });

  drawSubTypes();
  drawFields();
  drawPreview();
})();
