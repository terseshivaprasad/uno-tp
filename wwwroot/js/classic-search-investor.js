// The classic investor search, with the new Investor Identification screen's
// behaviour: the investor is checked against the register before the application
// can go on, and Proceed waits for that match. The register is mocked - nothing
// is sent anywhere.
(function () {
  var form = document.getElementById('csiForm');
  if (!form) return;

  var holder = form.querySelector('.csi-holder');
  var search = holder.querySelector('[data-search]');
  var result = holder.querySelector('[data-result]');
  var gap = holder.querySelector('[data-gap]');
  var gapTitle = holder.querySelector('[data-gap-title]');
  var gapList = holder.querySelector('[data-gap-list]');
  var gapFoot = holder.querySelector('[data-gap-foot]');
  var complete = holder.querySelector('[data-complete]');
  var genderHint = holder.querySelector('[data-gender-hint]');
  var verify = holder.querySelector('[data-verify]');
  var upload = holder.querySelector('[data-upload]');
  var panFile = holder.querySelector('[data-panfile]');
  var fileName = holder.querySelector('[data-filename]');
  var log = holder.querySelector('[data-log]');
  var checks = holder.querySelector('[data-checks]');
  var nameFix = holder.querySelector('[data-namefix]');
  var nsdlName = holder.querySelector('[data-nsdlname]');
  var nsdlNameError = document.getElementById('csiNsdlNameError');
  var verifyFail = holder.querySelector('[data-verify-fail]');
  var verifyFailText = holder.querySelector('[data-verify-fail-text]');
  var verifyOk = holder.querySelector('[data-verify-ok]');
  var appNoItem = holder.querySelector('[data-item="appno"]');
  var genderItem = holder.querySelector('[data-item="gender"]');
  var status = holder.querySelector('[data-status]');
  var fields = holder.querySelector('.csi-fields');
  var pan = holder.querySelector('[data-pan]');
  var folio = holder.querySelector('[data-folio]');
  var dateBox = holder.querySelector('[data-date]');
  var dd = holder.querySelector('[data-dd]');
  var mm = holder.querySelector('[data-mm]');
  var yyyy = holder.querySelector('[data-yyyy]');
  var panError = document.getElementById('csiPanError');
  var dobError = document.getElementById('csiDobError');
  var folioError = document.getElementById('csiFolioError');
  var proceed = document.getElementById('csiProceed');
  var hint = document.getElementById('csiHint');
  var PAN_RE = /^[A-Z]{5}[0-9]{4}[A-Z]$/;

  // The register and the mock NSDL both come from the page, which writes them
  // from one list in its model - so the test data card at the foot of the page
  // and what the search answers can never drift apart.
  var MOCK = JSON.parse(document.getElementById('csiMockData').textContent);
  var REGISTER = MOCK.folios;

  // What each document is called on the card and at the upload step.
  var DOC_LABELS = { pan: 'PAN card', photo: 'Photograph', poa: 'Proof of address' };

  // The register masks a holder's identifiers the way the consent tracker does:
  // a PAN keeps its first five and last character, a date of birth its year.
  function maskPan(value) {
    return value.slice(0, 5) + '\u2022\u2022\u2022\u2022' + value.slice(9);
  }

  function maskDob(value) {
    return '\u2022\u2022/\u2022\u2022/' + value.slice(6);
  }

  function findRecord(by, value, dob) {
    for (var i = 0; i < REGISTER.length; i++) {
      var r = REGISTER[i];
      var hit = by === 'folio' ? r.folio === value
        : by === 'pan-only' ? r.pan === value
        : (r.pan === value && r.dob === dob);
      if (hit) return r;
    }
    return null;
  }

  function mode() {
    return form.querySelector('input[name="searchBy"]:checked').value;
  }

  // box is the control that turns red, error the line under it.
  function setError(box, error, message) {
    error.textContent = message || '';
    error.hidden = !message;
    box.classList.toggle('is-invalid', !!message);
    return !message;
  }

  function clearErrors() {
    form.querySelectorAll('.csi-error').forEach(function (e) { e.hidden = true; e.textContent = ''; });
    form.querySelectorAll('.is-invalid').forEach(function (e) { e.classList.remove('is-invalid'); });
  }

  function syncFooter() {
    var identified = holder.dataset.identified === 'yes';
    proceed.disabled = !identified;
    hint.innerHTML = identified
      ? 'Investor identified — <strong>ready to proceed</strong>'
      : 'Identify the investor to continue — <strong>check the record once the fields are filled</strong>';
  }

  // Search By swaps which fields the card shows, and drops the errors of the
  // fields it hides so a stale message never blocks the other route.
  function syncMode() {
    var m = mode();
    fields.dataset.mode = m;
    fields.querySelectorAll('[data-for]').forEach(function (f) { f.hidden = f.dataset.for !== m; });
    holder.querySelectorAll('[data-hint-for]').forEach(function (t) { t.hidden = t.dataset.hintFor !== m; });
    clearErrors();
  }

  form.querySelectorAll('input[name="searchBy"]').forEach(function (r) {
    r.addEventListener('change', syncMode);
  });

  [pan, folio].forEach(function (input) {
    input.addEventListener('input', function () {
      input.value = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
    });
  });

  // The date boxes take digits only and carry the caret on once one is full.
  var date = [dd, mm, yyyy];
  date.forEach(function (part, i) {
    part.addEventListener('input', function () {
      part.value = part.value.replace(/\D/g, '');
      if (part.value.length === part.maxLength && i < date.length - 1) date[i + 1].focus();
    });
    part.addEventListener('keydown', function (e) {
      if (e.key === 'Backspace' && !part.value && i > 0) date[i - 1].focus();
    });
  });

  function checkDob() {
    var d = dd.value, m = mm.value, y = yyyy.value;
    if (!d || !m || !y) return setError(dateBox, dobError, 'Enter the date of birth');
    if (y.length < 4) return setError(dateBox, dobError, 'Enter the year in full');

    var when = new Date(+y, +m - 1, +d);
    var real = when.getFullYear() === +y && when.getMonth() === +m - 1 && when.getDate() === +d;
    if (!real) return setError(dateBox, dobError, 'Enter a valid date');
    if (when > new Date()) return setError(dateBox, dobError, 'The date of birth cannot be in the future');

    // The note on this page: an investor under 18 cannot hold a deposit.
    var eighteen = new Date(when.getFullYear() + 18, when.getMonth(), when.getDate());
    if (eighteen > new Date()) return setError(dateBox, dobError, 'The depositor must be 18 years or above');
    return setError(dateBox, dobError, '');
  }

  function pad(v) { return v.length === 1 ? '0' + v : v; }

  // A field the register does not hold reads as missing rather than blank, so the
  // gap is visible on the card and not only in the message under it.
  function setField(key, value) {
    // A page held in the browser cache can be older than this script, so a cell
    // it does not carry is skipped rather than left to throw and take the rest
    // of the check down with it.
    var cell = result.querySelector('[data-field="' + key + '"]');
    if (!cell) return;
    cell.textContent = value || 'Not available with us';
    cell.classList.toggle('csi-record__missing', !value);
  }

  // Everything the register is short of against this record. The documents are
  // collected at Upload Documents and the address at Investor Information, so
  // the two are named separately in the message.
  function showGaps(record) {
    if (!gap || !complete) return;
    var docs = Object.keys(DOC_LABELS).filter(function (k) { return !record.docs[k]; });
    var missing = docs.map(function (k) { return DOC_LABELS[k] + ' is not available with us'; });
    if (!record.address) missing.push('The address is not available with us');

    complete.hidden = missing.length > 0;
    gap.hidden = missing.length === 0;
    if (!missing.length) return;

    // A first-time investor has no folio at all, so nothing is "missing" from a
    // record that does not exist yet - it is all collected from scratch.
    gapTitle.textContent = !record.folio
      ? 'Nothing against this PAN is available with us'
      : missing.length === 1
        ? 'One thing is missing from this record'
        : missing.length + ' things are missing from this record';
    gapList.innerHTML = '';
    missing.forEach(function (m) {
      var li = document.createElement('li');
      li.textContent = m;
      gapList.appendChild(li);
    });

    var where = [];
    if (docs.length) where.push('the ' + (docs.length === 1 ? 'document' : 'documents') + ' at Upload Documents');
    if (!record.address) where.push('the address at Investor Information');
    gapFoot.textContent = 'You will be asked for ' + where.join(' and ')
      + ', later in this application. Carry on with Proceed, or search again if this is not the right '
      + (record.folio ? 'record.' : 'PAN.');
  }

  // established is false for a PAN the register does not hold: the card opens, but
  // the holder is not identified until NSDL says so.
  function identify(record, kind, detail, established) {
    holder.dataset.identified = established === false ? '' : 'yes';
    // The heading carries the folio and the name together. A first-time investor
    // has neither, so it falls back to the PAN the search was made with.
    var heading = record.name || record.pan;
    if (record.folio) heading = record.folio + ' \u2013 ' + heading;
    holder.querySelector('[data-result-name]').textContent = heading;
    var chip = holder.querySelector('[data-result-kind]');
    chip.textContent = kind;
    chip.classList.toggle('csi-chip--success', kind === 'Existing customer');
    holder.querySelector('[data-result-detail]').textContent = detail;

    setField('pan', maskPan(record.pan));
    setField('dob', maskDob(record.dob));
    setField('gender', record.gender);
    // The address itself is not shown on this card; only whether it is missing,
    // which showGaps says.
    // A PAN with no folio behind it carries no gender either, so the row goes
    // rather than standing there empty; it is collected during entry.
    genderItem.hidden = !record.gender;
    genderHint.hidden = !record.gender;
    appNoItem.hidden = !record.appNo;
    if (record.appNo) setField('appno', record.appNo);

    // The gaps are what the folio is short of; a PAN being established has no
    // folio yet, so the verification steps take that place until it passes.
    if (established === false) {
      gap.hidden = true;
      complete.hidden = true;
    } else {
      showGaps(record);
    }

    search.hidden = true;
    result.hidden = false;
    setStatus(established !== false);
    syncFooter();
  }

  function setStatus(identified) {
    status.textContent = identified ? 'Identified' : 'Not identified';
    status.classList.toggle('csi-status--ok', identified);
  }

  function reopen() {
    holder.dataset.identified = '';
    search.hidden = false;
    result.hidden = true;
    resetVerify();
    setStatus(false);
    syncFooter();
  }

  function resetVerify() {
    verify.hidden = true;
    upload.hidden = false;
    panFile.value = '';
    fileName.hidden = true;
    fileName.textContent = '';
    log.hidden = true;
    log.innerHTML = '';
    checks.hidden = true;
    nameFix.hidden = true;
    nsdlName.value = '';
    setError(nsdlName, nsdlNameError, '');
    verifyFail.hidden = true;
    verifyOk.hidden = true;
  }

  // ----- A PAN the register does not hold -------------------------------------
  // It opens a new application instead of a folio, and the holder is established
  // from the PAN copy: OCR reads the document, then the PAN, date of birth and
  // name go to NSDL.
  //
  // The pairs to demo with are listed on the page itself, in the test data card
  // at its foot. NSDL holds the name against the PAN; ocr is what the mock reads
  // off the document, which is that same name when the scan is clean and a
  // misreading when it is not. A listed PAN with another date of birth is turned
  // back, since NSDL is asked for the two together.
  var NSDL_CASES = MOCK.pans;

  // Any other PAN still reaches every outcome, decided by its last digit, so a
  // demo is never stuck with a PAN somebody made up on the spot:
  //   0 - no such PAN and date of birth pair   1 - the name does not match
  //   anything else - all three match
  var NAMES = ['AMIT KUMAR SHARMA', 'PRIYA RAMESH IYER', 'SUNIL DATTA JOSHI', 'MEERA ANAND NAIR'];
  var pending = null;

  function caseFor(value) {
    for (var i = 0; i < NSDL_CASES.length; i++) {
      if (NSDL_CASES[i].pan === value) return NSDL_CASES[i];
    }
    return null;
  }

  // Names are compared the way a clerk would read them: case and spacing aside.
  function normalise(name) {
    return name.toUpperCase().replace(/\s+/g, ' ').trim();
  }

  function seedOf(value) {
    var n = 0;
    for (var i = 0; i < value.length; i++) n += value.charCodeAt(i) * (i + 1);
    return n;
  }

  // Same shape as the application numbers the console lists, and the same number
  // every time for a given PAN so a demo can be repeated.
  function makeAppNo(value) {
    var seed = seedOf(value);
    var letters = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    return 'FBBMFL26F' + (10 + seed % 90)
      + letters.charAt(seed % 24) + letters.charAt(seed * 7 % 24) + (seed % 10);
  }

  function outcomeOf(value) {
    var last = value.charAt(8);
    return last === '0' ? 'pan-dob' : last === '1' ? 'name' : 'all';
  }

  function addLog(text) {
    log.hidden = false;
    var li = document.createElement('li');
    li.textContent = text;
    log.appendChild(li);
    return li;
  }

  // Each row carries what went to NSDL and what came back.
  function setRow(key, value, ok) {
    holder.querySelector('[data-row-value="' + key + '"]').textContent = value;
    var cell = holder.querySelector('[data-row-status="' + key + '"]');
    cell.textContent = ok ? 'Matched' : 'Not matched';
    cell.classList.toggle('csi-checkrow__status--ok', ok);
    cell.classList.toggle('csi-checkrow__status--bad', !ok);
  }

  function startNewPan(value, dob) {
    var known = caseFor(value);
    var seed = seedOf(value);
    var ocrName = NAMES[seed % NAMES.length];

    pending = {
      pan: value,
      dob: dob,
      folio: '',
      name: '',
      gender: '',
      appNo: makeAppNo(value),
      // A pair from the list is answered exactly as the list says, and a typed
      // name has to match what NSDL holds. Anything else is answered from the
      // last digit, and any name typed in place of OCR's reading is taken.
      strict: !!known,
      outcome: known ? (known.dob === dob ? known.outcome : 'pan-dob') : outcomeOf(value),
      nsdlName: known ? known.name : ocrName,
      ocrName: known ? known.ocr : ocrName,
      docs: { pan: false, photo: false, poa: false },
    };
    // Off the list, a name that does not match is one NSDL holds differently.
    if (!known && pending.outcome === 'name') {
      pending.nsdlName = NAMES[(seed + 1) % NAMES.length];
    }
    identify(pending, 'Not identified', 'No folio against this PAN \u2014 application '
      + pending.appNo + ' opens once the PAN is established below.', false);
    resetVerify();
    verify.hidden = false;
  }

  // The document is read where it was picked, so nothing is uploaded anywhere.
  panFile.addEventListener('change', function () {
    if (!panFile.files.length || !pending) return;
    upload.hidden = true;
    fileName.hidden = false;
    fileName.textContent = panFile.files[0].name;
    log.innerHTML = '';
    addLog('PAN copy received \u2014 ' + panFile.files[0].name);

    var reading = addLog('Running OCR on the document\u2026');
    window.setTimeout(function () {
      reading.textContent = 'OCR read the PAN, the date of birth and the name from the document';
      runNsdl(pending.ocrName);
    }, 900);
  });

  // PAN and date of birth are always put to NSDL as typed; the name comes from
  // OCR the first time and from the field after that.
  function runNsdl(name) {
    checks.hidden = true;
    nameFix.hidden = true;
    verifyFail.hidden = true;
    verifyOk.hidden = true;

    var line = addLog('Verifying PAN, date of birth and name with NSDL\u2026');
    window.setTimeout(function () {
      var pairOk = pending.outcome !== 'pan-dob';
      // A pair from the list is held against one name and nothing else. Off the
      // list, a name typed from the card is taken; OCR's reading of it is not,
      // when NSDL holds the PAN against someone else.
      var nameOk = pairOk && (pending.strict
        ? normalise(name) === normalise(pending.nsdlName)
        : pending.outcome !== 'name' || name !== pending.ocrName);

      line.textContent = 'NSDL responded';
      setRow('pan', pending.pan, pairOk);
      setRow('dob', pending.dob, pairOk);
      setRow('name', name, nameOk);
      checks.hidden = false;

      if (!pairOk) {
        verifyFailText.textContent = 'NSDL holds no record of PAN ' + pending.pan
          + ' against ' + pending.dob + '. The application cannot go on with these details.';
        verifyFail.hidden = false;
        holder.dataset.identified = '';
        setStatus(false);
      } else if (!nameOk) {
        nameFix.hidden = false;
        // The first miss is OCR's; a second is the name that was typed, so the
        // field says what happened rather than leaving it to the row above.
        setError(nsdlName, nsdlNameError, nsdlName.value.trim()
          ? 'NSDL does not hold PAN ' + pending.pan + ' against that name'
          : '');
        holder.dataset.identified = '';
        setStatus(false);
        nsdlName.focus();
      } else {
        // The PAN copy is now on the application, so only the rest is outstanding.
        pending.name = name;
        pending.docs.pan = true;
        // The heading now reads like an established record: the number the
        // application opens under, then the name NSDL holds against the PAN.
        holder.querySelector('[data-result-name]').textContent = pending.appNo + ' \u2013 ' + name;
        holder.querySelector('[data-result-kind]').textContent = 'New investor';
        verifyOk.hidden = false;
        holder.dataset.identified = 'yes';
        setStatus(true);
        showGaps(pending);
      }
      syncFooter();
    }, 900);
  }

  holder.querySelector('[data-retry]').addEventListener('click', function () {
    var typed = nsdlName.value.trim().replace(/\s+/g, ' ');
    if (!setError(nsdlName, nsdlNameError, typed.length >= 3 ? '' : 'Enter the name as printed on the PAN')) {
      return nsdlName.focus();
    }
    runNsdl(typed.toUpperCase());
  });

  holder.querySelector('[data-check]').addEventListener('click', function () {
    clearErrors();
    if (mode() === 'pan') {
      var value = pan.value.trim();
      var okPan = setError(pan, panError, !value ? 'Enter the PAN' : (PAN_RE.test(value) ? '' : 'Enter a valid PAN, like ABCDE1234F'));
      var okDob = checkDob();
      if (!okPan || !okDob) return (okPan ? dd : pan).focus();

      var dob = pad(dd.value) + '/' + pad(mm.value) + '/' + yyyy.value;
      var found = findRecord('pan', value, dob);
      if (found) {
        identify(found, 'Existing customer', 'Matched on PAN and date of birth · ' + found.note);
        return;
      }

      // A PAN that is on record against another date of birth is a typo far more
      // often than a second investor, so it is sent back to the field rather than
      // opened as a new folio.
      if (findRecord('pan-only', value)) {
        setError(dateBox, dobError, 'This PAN is on record, but against a different date of birth');
        return dd.focus();
      }

      startNewPan(value, dob);
    } else {
      var f = folio.value.trim();
      if (!setError(folio, folioError, f ? '' : 'Enter the folio number')) return folio.focus();
      var byFolio = findRecord('folio', f);
      if (!byFolio) {
        setError(folio, folioError, 'No record against that folio number — check it, or search by PAN instead');
        return folio.focus();
      }
      identify(byFolio, 'Existing customer', 'Matched on folio number · ' + byFolio.note);
    }
  });

  holder.querySelectorAll('[data-again]').forEach(function (button) {
    button.addEventListener('click', function () {
      reopen();
      (mode() === 'pan' ? pan : folio).focus();
    });
  });

  document.getElementById('csiClear').addEventListener('click', function () {
    form.reset();
    reopen();
    syncMode();
    (mode() === 'pan' ? pan : folio).focus();
  });

  // Proceed carries the holder to the upload step: whichever record the check
  // settled on, and the application number when one has just been opened.
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    if (proceed.disabled) return;

    var record = pending && pending.appNo && holder.dataset.identified === 'yes'
      ? pending
      : findRecord(mode() === 'folio' ? 'folio' : 'pan',
          mode() === 'folio' ? folio.value.trim() : pan.value.trim(),
          pad(dd.value) + '/' + pad(mm.value) + '/' + yyyy.value);
    if (!record) return;

    var query = '?pan=' + encodeURIComponent(record.pan) + '&dob=' + encodeURIComponent(record.dob)
      + '&name=' + encodeURIComponent(record.name)
      + '&folio=' + encodeURIComponent(record.folio);
    if (record.appNo) query += '&app=' + encodeURIComponent(record.appNo);
    // The PAN copy is already on the application when the register holds one or
    // the PAN was just established from one, so the next step does not ask again.
    if (record.docs && record.docs.pan) query += '&panfiled=true';
    window.location.href = '/Apps/UnoTp/Classic/UploadDocuments' + query;
  });

  syncMode();
  syncFooter();
})();
