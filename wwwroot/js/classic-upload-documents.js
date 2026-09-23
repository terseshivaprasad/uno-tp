// The classic upload step. A file is read where it was picked and named back to
// the partner - nothing is uploaded anywhere, and nothing is sent to CKYC.
(function () {
  var form = document.getElementById('cudForm');
  if (!form) return;

  var appType = document.querySelector('[data-apptype]');
  var poaType = document.getElementById('cudPoaType');
  var mailingPoaType = document.getElementById('cudMailingPoaType');
  var mailingCol = document.querySelector('[data-when="mailing"]');
  var mailingRadios = document.querySelectorAll('input[name="cudMailing"]');
  var payMode = document.getElementById('cudPayMode');
  var ckyc = document.getElementById('cudCkyc');
  var hint = document.getElementById('cudHint');
  var proceed = document.getElementById('cudProceed');
  var formNoOptional = document.querySelector('[data-formno-optional]');
  var formNo = document.getElementById('cudFormNo');
  var sourcing = document.getElementById('cudSourcing');
  var broker = document.getElementById('cudBroker');
  var category = document.getElementById('cudCategory');
  var empCode = document.getElementById('cudEmpCode');
  var empName = document.querySelector('[data-empname]');
  var empUnknown = document.querySelector('[data-empunknown]');
  var empCompany = document.getElementById('cudEmpCompany');
  var empHolder = document.getElementById('cudEmpHolder');
  var empRelation = document.getElementById('cudEmpRelation');
  var empProofType = document.getElementById('cudEmpProofType');

  // The staff the mock can put a name to, and what the page is sourced and
  // categorised as when those fields carry their own block of questions.
  var EMPLOYEES = JSON.parse(document.getElementById('cudEmployees').textContent);
  var EMPLOYEE_SOURCING = 'MFL-EX';
  var EMPLOYEE_CATEGORY = 'EMPLOYEE';

  function group(name) {
    return Array.prototype.slice.call(document.querySelectorAll('[data-when="' + name + '"]'));
  }

  var MB = 1024 * 1024;

  // What the application allows per document. A file turned away for its type or
  // its size never reached the document, so it costs nothing; an attempt is one
  // document put to the checks, whatever they come back with.
  var MAX_ATTEMPTS = 3;
  var SPENT = JSON.parse(document.getElementById('cudAttempts').textContent);

  // What an address proof tends to be called, whatever type is picked above it.
  var POA_TOKENS = ['poa', 'address', 'aadhaar', 'aadhar', 'passport', 'licence',
    'license', 'voter', 'utility', 'bill'];

  // Every slot on the page, by the key its markup carries.
  var slots = {};
  document.querySelectorAll('[data-slot]').forEach(function (el) {
    slots[el.dataset.slot] = {
      key: el.dataset.slot,
      el: el,
      drop: el.querySelector('[data-drop]'),
      idle: el.querySelector('.cud-drop__idle'),
      busy: el.querySelector('.cud-drop__busy'),
      done: el.querySelector('.cud-drop__done'),
      // Set while the document is being identified, so the box shows neither the
      // picker nor a file it may yet hand back.
      checking: false,
      name: el.querySelector('[data-file]'),
      size: el.querySelector('[data-size]'),
      na: el.querySelector('[data-na]'),
      input: el.querySelector('[data-input]'),
      view: el.querySelector('[data-view]'),
      replace: el.querySelector('[data-replace]'),
      label: el.querySelector('.cud-drop__title').textContent.replace('Click to upload ', ''),
      // Set for a document the application already carries, which has a name but
      // no file of its own to show. filedOriginal keeps it through a replacement,
      // since the copy on the application stands whatever is picked here.
      filed: null,
      filedOriginal: null,
      error: document.getElementById(el.dataset.slot + 'Error'),
      tries: el.querySelector('[data-tries]'),
      // What the application has already put to the checks for this document,
      // from the history the page opens with.
      attempts: SPENT[el.dataset.slot] || 0,
      file: null,
      // A slot the application does not use takes nothing and is asked for
      // nothing; NOT APPLICABLE stands where its picker would be.
      used: true,
    };
  });

  function setError(slot, message) {
    slot.error.textContent = message || '';
    slot.error.hidden = !message;
    slot.el.classList.toggle('is-invalid', !!message);
  }

  function fieldError(field, box, message) {
    box.textContent = message || '';
    box.hidden = !message;
    field.classList.toggle('is-invalid', !!message);
    return !message;
  }

  function sizeOf(bytes) {
    return bytes < MB
      ? Math.max(1, Math.round(bytes / 1024)) + ' KB'
      : (bytes / MB).toFixed(bytes < 10 * MB ? 1 : 0) + ' MB';
  }

  // What the slot takes, read back off the input so the page and the check can
  // never disagree about it.
  function limitOf(slot) {
    return slot.input.accept.indexOf('pdf') > -1 ? 4 : 2;
  }

  function show(slot) {
    var has = !!slot.file || !!slot.filed;
    var used = spent(slot);
    slot.drop.hidden = !slot.used;
    slot.na.hidden = slot.used;
    slot.idle.hidden = has || slot.checking;
    slot.busy.hidden = !slot.checking;
    slot.done.hidden = !has || slot.checking;
    slot.drop.classList.toggle('is-busy', slot.checking);
    // Once a document is in the box, the box is a view of it rather than a second
    // way to pick one: it opens the sheet on click, and replacing goes through the
    // arrows above it.
    slot.drop.disabled = slot.checking || (used && !has);
    slot.drop.classList.toggle('is-filled', has && !slot.checking);
    slot.drop.classList.toggle('is-spent', used && !has);
    slot.view.disabled = !has || slot.checking;
    // Three against the application, whatever they came back with. A document
    // already filed cannot be swapped once they are gone either.
    slot.replace.disabled = !has || slot.checking || used;
    slot.tries.hidden = slot.attempts === 0;
    slot.tries.textContent = used
      ? 'All ' + MAX_ATTEMPTS + ' attempts used — this document now goes to Operations.'
      : slot.attempts + ' of ' + MAX_ATTEMPTS + ' attempts used';
    slot.tries.classList.toggle('is-spent', used);
    if (slot.file) {
      slot.name.textContent = slot.file.name;
      slot.size.textContent = sizeOf(slot.file.size);
    } else if (slot.filed) {
      slot.name.textContent = slot.filed;
      slot.size.textContent = 'already on the application';
    }
  }

  // ----- The record of what was tried -----------------------------------------
  // One entry per attempt, written as it happens: the file, what the checks made
  // of it, and what the application did with it. Entries stand for the life of
  // the page, since the count they belong to stands against the application.
  var logList = document.getElementById('cudLogList');
  var logNone = document.getElementById('cudLogNone');

  function clockNow() {
    var d = new Date();
    return [d.getHours(), d.getMinutes(), d.getSeconds()]
      .map(function (n) { return n < 10 ? '0' + n : '' + n; }).join(':');
  }

  function logStart(slot, file) {
    logNone.hidden = true;
    var row = document.createElement('li');
    row.className = 'cud-log__row';
    row.innerHTML = '<div class="cud-log__top">'
      + '<span class="cud-log__doc"></span>'
      + '<span class="cud-log__try"></span>'
      + '<span class="cud-log__at"></span>'
      + '<span class="cud-log__mark" data-mark>Checking</span></div>'
      + '<p class="cud-log__file"></p>'
      + '<ul class="cud-log__stages" data-stages></ul>';
    row.querySelector('.cud-log__doc').textContent = slot.label;
    row.querySelector('.cud-log__try').textContent = 'Attempt ' + slot.attempts + ' of ' + MAX_ATTEMPTS;
    row.querySelector('.cud-log__at').textContent = clockNow();
    row.querySelector('.cud-log__file').textContent = file.name + ' \u00b7 ' + sizeOf(file.size);
    logList.insertBefore(row, logList.firstChild);
    return { row: row, stages: row.querySelector('[data-stages]'), mark: row.querySelector('[data-mark]') };
  }

  // One line of what a check made of the document, in the plainest words it can
  // be put in: this is the page a partner is sent back to when Operations ask.
  function logStage(entry, text, kind) {
    if (!entry) return;
    var line = document.createElement('li');
    line.className = 'cud-log__stage' + (kind ? ' is-' + kind : '');
    line.textContent = text;
    entry.stages.appendChild(line);
  }

  function logEnd(entry, outcome, kind) {
    if (!entry) return;
    entry.mark.textContent = outcome;
    entry.row.classList.add('is-' + kind);
  }

  // ----- The address a proof carries ------------------------------------------
  // OCR reads the address off the copy that was just filed, and the issuer behind
  // that proof is asked whether it is the address they hold. Only a clean answer
  // replaces the address the application carries; anything else leaves it alone
  // and says so, since a proof that cannot be stood behind proves nothing.
  var ADDRESS = JSON.parse(document.getElementById('cudAddresses').textContent);

  var readCards = {};
  document.querySelectorAll('[data-read]').forEach(function (el) {
    readCards[el.dataset.read] = {
      el: el,
      state: el.querySelector('[data-state]'),
      lines: el.querySelector('[data-lines]'),
      was: el.querySelector('[data-was]'),
      from: el.querySelector('[data-from]'),
    };
  });

  // What each card says before anything is read, so it can be put back exactly.
  Object.keys(readCards).forEach(function (key) {
    var card = readCards[key];
    card.from.dataset.first = card.from.textContent;
    card.lines.dataset.first = card.lines.textContent;
    card.state.dataset.first = card.state.textContent;
  });

  function setReadState(card, text, kind) {
    card.state.textContent = text;
    card.el.classList.remove('is-working', 'is-done', 'is-failed');
    if (kind) card.el.classList.add(kind);
  }

  // The mock cannot read a scan any more than it can identify one, so the file
  // name says how the check goes: a copy named as a mismatch comes back refused.
  function issuerConfirms(file) {
    var name = file.name.toLowerCase();
    return name.indexOf('mismatch') === -1 && name.indexOf('fail') === -1;
  }

  function readAddress(key, file, entry) {
    var card = readCards[key];
    if (!card) return;
    var type = (key === 'mailing' ? mailingPoaType.value : poaType.value);
    var issuer = ADDRESS.issuers[type] || '';
    var read = ADDRESS.read[key];

    setReadState(card, 'Reading the address…', 'is-working');
    window.setTimeout(function () {
      // A bill has no register behind it, so there is nobody to confirm it with
      // and the address the application carries stands until Operations say so.
      if (!issuer) {
        setReadState(card, 'With Operations', 'is-failed');
        card.from.textContent = 'A ' + type.toLowerCase() + ' has no issuer to check with, so the address is left as it stands for Operations to settle.';
        logStage(entry, 'OCR read: ' + read);
        logStage(entry, 'A ' + type.toLowerCase() + ' has no issuer to check with.', 'warn');
        logEnd(entry, 'Filed, address unchanged', 'warn');
        return;
      }
      logStage(entry, 'OCR read: ' + read);
      setReadState(card, 'Checking with ' + issuer + '…', 'is-working');
      window.setTimeout(function () {
        if (!issuerConfirms(file)) {
          setReadState(card, 'Not confirmed', 'is-failed');
          card.from.textContent = issuer + ' did not confirm the address on this proof, so the application keeps the address it carries. Upload a clearer copy, or a different proof.';
          logStage(entry, issuer + ' did not confirm that address.', 'bad');
          logEnd(entry, 'Filed, address unchanged', 'warn');
          return;
        }
        var before = card.lines.textContent;
        card.lines.textContent = read;
        card.was.textContent = 'Was: ' + before;
        card.was.hidden = false;
        setReadState(card, 'Verified with ' + issuer, 'is-done');
        card.from.textContent = 'Read off the ' + type.toLowerCase() + ' filed above and confirmed with ' + issuer + '.';
        logStage(entry, issuer + ' confirmed that address.', 'ok');
        logStage(entry, 'Address on the application replaced. Was: ' + before, 'ok');
        logEnd(entry, 'Filed', 'ok');
      }, 900);
    }, 700);
  }

  // A cheque carries an account rather than an address, and it is the bank it is
  // drawn on that is asked to stand behind it. The account goes no further than
  // this card until that answer comes back clean.
  function readInstrument(file, entry) {
    var card = readCards.payment;
    if (!card) return;
    var mode = payMode.value.toLowerCase();
    var bank = ADDRESS.bank;

    setReadState(card, 'Reading the instrument\u2026', 'is-working');
    window.setTimeout(function () {
      setReadState(card, 'Checking with ' + bank + '\u2026', 'is-working');
      logStage(entry, 'OCR read: ' + ADDRESS.read.payment);
      window.setTimeout(function () {
        if (!issuerConfirms(file)) {
          setReadState(card, 'Not confirmed', 'is-failed');
          card.from.textContent = bank + ' did not confirm that account against this ' + mode
            + ', so nothing is carried forward. Upload a clearer copy of the instrument.';
          logStage(entry, bank + ' did not confirm that account.', 'bad');
          logEnd(entry, 'Filed, account not carried', 'warn');
          return;
        }
        card.lines.textContent = ADDRESS.read.payment;
        setReadState(card, 'Confirmed with ' + bank, 'is-done');
        card.from.textContent = 'Read off the ' + mode + ' filed above and confirmed with ' + bank
          + '. Bank Details & Payment opens with this account.';
        logStage(entry, bank + ' confirmed that account.', 'ok');
        logStage(entry, 'Account carried to Bank Details & Payment.', 'ok');
        logEnd(entry, 'Filed', 'ok');
      }, 900);
    }, 700);
  }

  // What a slot expects to be handed, for the two documents the application is
  // identified by. The mock cannot read a scan, so it goes by the file name: a
  // copy named for what it is passes, and anything else is handed back to be
  // uploaded again rather than filed as the wrong document.
  var IDENTIFY = {
    pan: { what: 'a PAN card', tokens: ['pan'], named: 'PAN' },
    poa: { what: 'a proof of address', tokens: POA_TOKENS, named: 'the proof', type: function () { return poaType.value; } },
    mailing: { what: 'a proof of address', tokens: POA_TOKENS, named: 'the proof', type: function () { return mailingPoaType.value; } },
    payment: {
      what: 'a cheque or demand draft', named: 'the instrument',
      tokens: ['cheque', 'chq', 'dd', 'draft'],
      type: function () { return payMode.value; },
    },
  };

  function identified(rule, file) {
    var name = file.name.toLowerCase();
    var tokens = rule.tokens.slice();
    // The type chosen above the box counts as a name for the document too.
    if (rule.type && rule.type()) tokens.push(rule.type().toLowerCase());
    return tokens.some(function (token) { return name.indexOf(token) > -1; });
  }

  // The check the partner sees: the box says it is looking, and comes back with
  // the document filed or with the reason it was not.
  function checkDocument(slot, rule, file, entry) {
    slot.checking = true;
    setError(slot, '');
    show(slot);
    window.setTimeout(function () {
      slot.checking = false;
      if (identified(rule, file)) {
        slot.file = file;
        slot.filed = null;
        logStage(entry, 'Identified as ' + rule.what + '.', 'ok');
        if (slot.key === 'poa' || slot.key === 'mailing') {
          readAddress(slot.key, file, entry);
        } else if (slot.key === 'payment') {
          readInstrument(file, entry);
        } else {
          logEnd(entry, 'Filed', 'ok');
        }
      } else {
        // Nothing is filed: the slot stands empty and asks for the document again.
        slot.input.value = '';
        setError(slot, 'That does not read as ' + rule.what + '. This mock identifies a '
          + 'document by its file name, so upload it again with ' + rule.named + ' in the name.');
        logStage(entry, 'Not identified as ' + rule.what + '.', 'bad');
        logEnd(entry, 'Refused', 'bad');
      }
      show(slot);
      syncFooter();
    }, 700);
  }

  function spent(slot) {
    return slot.attempts >= MAX_ATTEMPTS;
  }

  function take(slot, file) {
    if (!file) return;
    if (spent(slot)) {
      setError(slot, 'No attempts left for this document. All ' + MAX_ATTEMPTS
        + ' allowed against this application have been used — book a service call to file it.');
      show(slot);
      return;
    }
    var limit = limitOf(slot);
    var ok = slot.input.accept.split(',').indexOf(file.type) > -1;
    if (!ok) {
      setError(slot, 'That file type is not accepted here');
    } else if (file.size > limit * MB) {
      setError(slot, 'The file is over ' + limit + ' MB — ' + sizeOf(file.size));
    } else {
      // Past the type and the size, the document is put to the checks, and that
      // is what an attempt is.
      slot.attempts += 1;
      var entry = logStart(slot, file);
      if (IDENTIFY[slot.key]) {
        checkDocument(slot, IDENTIFY[slot.key], file, entry);
        return;
      }
      setError(slot, '');
      slot.file = file;
      // A replacement stands in for whatever the application carried before.
      slot.filed = null;
      logStage(entry, 'Filed as it stands — this document carries no check of its own.');
      logEnd(entry, 'Filed', 'ok');
    }
    show(slot);
    syncFooter();
  }

  // Not a tool on the slot any more: only Clear All and a slot the application
  // stops using empty a picker, and either way the copy the step before filed is
  // put back, since this step does not take it off the application.
  // The address a proof replaced goes back with the proof: what the application
  // carried is on the card's own "Was" line until a check comes back clean again.
  function resetRead(key) {
    var card = readCards[key];
    if (!card) return;
    if (!card.was.hidden) {
      // What the read replaced is on the card's own line: put that back.
      card.lines.textContent = card.was.textContent.replace('Was: ', '');
      card.was.hidden = true;
      card.was.textContent = '';
    } else {
      card.lines.textContent = card.lines.dataset.first;
    }
    setReadState(card, card.state.dataset.first, '');
    card.from.textContent = card.from.dataset.first;
  }

  function drop(slot) {
    slot.checking = false;
    if (slot.key === 'poa' || slot.key === 'mailing' || slot.key === 'payment') resetRead(slot.key);
    slot.file = null;
    // Back to whatever the application carried before this step, if anything.
    slot.filed = slot.filedOriginal;
    slot.input.value = '';
    setError(slot, '');
    show(slot);
    syncFooter();
  }

  Object.keys(slots).forEach(function (key) {
    var slot = slots[key];

    // An empty box opens the picker; a filled one opens what is in it, since the
    // document is the thing to look at from there and replacing goes through the
    // arrows above it.
    slot.drop.addEventListener('click', function () {
      if (slot.file || slot.filed) openPreview(slot);
      else slot.input.click();
    });
    slot.input.addEventListener('change', function () { take(slot, slot.input.files[0]); });

    // The same file can also be dragged onto the box.
    ['dragenter', 'dragover'].forEach(function (type) {
      slot.drop.addEventListener(type, function (e) {
        e.preventDefault();
        slot.drop.classList.add('is-over');
      });
    });
    ['dragleave', 'drop'].forEach(function (type) {
      slot.drop.addEventListener(type, function (e) {
        e.preventDefault();
        slot.drop.classList.remove('is-over');
        // Only an empty box takes what is dragged onto it, as only an empty box
        // opens the picker.
        if (type === 'drop' && e.dataTransfer && !slot.file && !slot.filed) take(slot, e.dataTransfer.files[0]);
      });
    });

    slot.view.addEventListener('click', function () { openPreview(slot); });
    slot.replace.addEventListener('click', function () { slot.input.click(); });
  });

  // A slot the application has no use for keeps nothing: whatever was picked for
  // it goes, so a switch of application type cannot leave a stray file behind.
  function use(slot, used) {
    if (slot.used === used) return;
    slot.used = used;
    if (!used) {
      slot.file = null;
      slot.filed = null;
      slot.input.value = '';
      setError(slot, '');
    } else {
      slot.filed = slot.filedOriginal;
    }
    show(slot);
  }

  // ----- The preview sheet ----------------------------------------------------
  // One sheet for every slot: the document where the browser can show it, and
  // Replace and Remove to hand so the right box does not have to be found again.
  var preview = document.getElementById('cudPreview');
  var previewBody = preview.querySelector('[data-preview-body]');
  var previewTitle = preview.querySelector('[data-preview-title]');
  var previewKind = preview.querySelector('[data-preview-kind]');
  var previewMeta = preview.querySelector('[data-preview-meta]');
  var shown = null;
  var shownUrl = '';

  function openPreview(slot) {
    if (!slot.file && !slot.filed) return;
    shown = slot;
    previewKind.textContent = slot.label;
    previewTitle.textContent = slot.file ? slot.file.name : slot.filed;
    previewBody.innerHTML = '';

    if (!slot.file) {
      // Filed before this step: it lives with the documents already on the
      // application, which this mock has no copy of.
      previewMeta.textContent = 'Already on the application';
      previewBody.appendChild(placeholder('This copy was filed before this step, so there is nothing here to show.'));
    } else {
      previewMeta.textContent = sizeOf(slot.file.size) + ' \u00b7 ' + (slot.file.type || 'file');
      shownUrl = URL.createObjectURL(slot.file);
      if (slot.file.type.indexOf('image/') === 0) {
        var img = document.createElement('img');
        img.src = shownUrl;
        img.alt = slot.label;
        previewBody.appendChild(img);
      } else {
        var frame = document.createElement('iframe');
        frame.src = shownUrl;
        frame.title = slot.label;
        previewBody.appendChild(frame);
      }
    }

    preview.hidden = false;
    document.body.style.overflow = 'hidden';
    preview.querySelector('[data-preview-close]').focus();
  }

  function placeholder(text) {
    var p = document.createElement('p');
    p.className = 'cud-preview__none';
    p.textContent = text;
    return p;
  }

  function closePreview() {
    preview.hidden = true;
    previewBody.innerHTML = '';
    document.body.style.overflow = '';
    if (shownUrl) {
      URL.revokeObjectURL(shownUrl);
      shownUrl = '';
    }
    if (shown) shown.view.focus();
    shown = null;
  }

  preview.querySelector('[data-preview-close]').addEventListener('click', closePreview);
  preview.addEventListener('click', function (e) { if (e.target === preview) closePreview(); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !preview.hidden) closePreview();
  });
  preview.querySelector('[data-preview-replace]').addEventListener('click', function () {
    var slot = shown;
    closePreview();
    if (slot) slot.input.click();
  });

  // DIGITAL is accepted through the investor's own link, so there is no signed
  // form to file and no form number to quote; CKYC answers for it instead.
  function syncAppType() {
    var physical = appType.value === 'PHYSICAL';
    use(slots.form, physical);
    // The field stays either way - a number already typed is not thrown away by
    // a change of mind - but only a paper application is asked for one.
    formNoOptional.hidden = physical;
    if (!physical) fieldError(formNo, document.getElementById('cudFormNoError'), '');
    ckyc.disabled = physical;
    ckyc.title = physical ? 'CKYC answers for a digital application only' : '';
    syncFooter();
  }

  // Address proof answers for the permanent address. Where the investor is
  // written to somewhere else, that address is proved on its own: a second type
  // and picker, asked for only while the two differ.
  function mailingDiffers() {
    return document.querySelector('input[name="cudMailing"]:checked').value === 'other';
  }

  function syncMailing() {
    var differs = mailingDiffers();
    mailingCol.hidden = !differs;
    use(slots.mailing, differs);
    readCards.mailing.el.hidden = !differs;
    if (!differs) {
      mailingPoaType.value = '';
      fieldError(mailingPoaType, document.getElementById('cudMailingPoaTypeError'), '');
      resetRead('mailing');
    }
    // With no mailing column beside it the photograph opens a row on its own, so
    // the band standing in for a neighbour's select box has nothing to meet.
    document.querySelector('.cud-grid--docs').classList.toggle('is-lone-photo', !differs);
    syncFooter();
  }

  // Only a mode settled by an instrument carries a document.
  function syncPayMode() {
    var picked = payMode.options[payMode.selectedIndex];
    var document_ = picked ? picked.dataset.document : '';
    use(slots.payment, !!document_);
    // Only a mode settled by an instrument has an account to read off one.
    document.querySelector('[data-when="instrument"]').hidden = !document_;
    if (!document_) resetRead('payment');
    if (document_) {
      slots.payment.el.querySelector('.cud-drop__title').textContent = 'Click to upload ' + document_;
    }
    syncFooter();
  }

  // Sourced by the partner: their own code stands in the field. Sourced through
  // a broker: the broker's code is typed instead, so the two swap places.
  function syncSourcing() {
    var byEmployee = sourcing.value === EMPLOYEE_SOURCING;
    var byBroker = sourcing.value === 'BROKER';
    group('sourcing-employee').forEach(function (el) { el.hidden = !byEmployee; });
    group('sourcing-broker').forEach(function (el) { el.hidden = !byBroker; });
    if (!byBroker) {
      broker.value = '';
      fieldError(broker, document.getElementById('cudBrokerError'), '');
    }
    syncFooter();
  }

  // An employee deposit is booked against a staff record; every other category
  // asks none of it, and anything already typed goes with the block.
  function syncCategory() {
    var employee = category.value === EMPLOYEE_CATEGORY;
    group('employee').forEach(function (el) { el.hidden = !employee; });
    use(slots.empproof, employee);
    if (!employee) {
      [empCode, empCompany, empHolder, empRelation, empProofType].forEach(function (f) {
        f.value = '';
        var box = document.getElementById(f.id + 'Error');
        if (box) fieldError(f, box, '');
      });
      resolveEmployee();
    }
    syncFooter();
  }

  // The name behind the code, the way the old screen resolves it under the field.
  function resolveEmployee() {
    var typed = empCode.value.trim();
    var found = employeeName();
    empName.textContent = found || (typed ? 'not found' : '\u2014');
    empName.classList.toggle('cud-resolved__none', !found);
    // Said plainly rather than left as a bare "not found": the application goes
    // on, and the partner is told who settles it and when.
    empUnknown.hidden = !typed || !!found;
  }

  // The staff record behind the code, or nothing.
  function employeeName() {
    return EMPLOYEES[empCode.value.trim().toUpperCase()] || '';
  }

  // What the footer says is what Proceed would ask for next, so the partner is
  // never sent looking for it.
  function outstanding() {
    var left = [];
    if (appType.value === 'PHYSICAL' && !slots.form.file) left.push('the application form');
    if (!slots.pan.file && !slots.pan.filed) left.push('the PAN copy');
    if (!poaType.value) left.push('the proof of address type');
    if (!slots.poa.file) left.push('the proof of address');
    if (mailingDiffers() && !mailingPoaType.value) left.push('the mailing address proof type');
    if (mailingDiffers() && !slots.mailing.file) left.push('the mailing address proof');
    if (!slots.photo.file) left.push('the photograph');
    if (!payMode.value) left.push('the payment mode');
    if (slots.payment.used && !slots.payment.file) left.push('the instrument copy');
    if (!sourcing.value) left.push('the sourcing mode');
    if (sourcing.value === 'BROKER' && !broker.value.trim()) left.push('the broker code');
    if (!category.value) left.push('the deposit category');
    if (category.value === EMPLOYEE_CATEGORY) {
      if (!empCode.value.trim()) left.push('the employee code');
      if (!empCompany.value.trim()) left.push('the employee company');
      if (!empHolder.value) left.push('the employee holder');
      if (!empRelation.value) left.push('the relation with the holder');
      if (!empProofType.value) left.push('the employee proof type');
      if (!slots.empproof.file) left.push('the employee proof');
    }
    if (appType.value === 'PHYSICAL' && !formNo.value.trim()) left.push('the form number');
    return left;
  }

  function syncFooter() {
    var left = outstanding();
    hint.innerHTML = left.length === 0
      ? 'Everything is in — <strong>ready to proceed</strong>'
      : left.length + (left.length === 1 ? ' thing' : ' things') + ' still to go — <strong>' + left[0] + '</strong>'
        + (left.length > 1 ? ' first' : '');
  }

  [poaType, payMode, empHolder, empRelation, empProofType].forEach(function (select) {
    select.addEventListener('change', function () { syncFooter(); });
  });
  [broker, formNo, empCompany].forEach(function (field) {
    field.addEventListener('input', function () { syncFooter(); });
  });
  empCode.addEventListener('input', function () {
    resolveEmployee();
    syncFooter();
  });
  appType.addEventListener('change', syncAppType);
  payMode.addEventListener('change', syncPayMode);
  sourcing.addEventListener('change', syncSourcing);
  category.addEventListener('change', syncCategory);

  mailingRadios.forEach(function (radio) {
    radio.addEventListener('change', syncMailing);
  });

  ckyc.addEventListener('click', function () {
    window.showToast('The CKYC fetch is not wired up in this mock yet.');
  });

  document.getElementById('cudClear').addEventListener('click', function () {
    form.reset();
    Object.keys(slots).forEach(function (key) { drop(slots[key]); });
    [sourcing, broker, category, formNo, poaType, payMode,
      empCode, empCompany, empHolder, empRelation, empProofType].forEach(function (f) { f.value = ''; });
    appType.value = 'DIGITAL';
    syncAppType();
    syncMailing();
    syncPayMode();
    syncSourcing();
    syncCategory();
    appType.focus();
  });

  // Proceed says what is missing where it is missing, and stops at the first one.
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var first = null;

    function need(ok, field) {
      if (!ok && !first) first = field;
      return ok;
    }

    need(fieldError(poaType, document.getElementById('cudPoaTypeError'),
      poaType.value ? '' : 'Choose the proof of address'), poaType);
    if (mailingDiffers()) {
      need(fieldError(mailingPoaType, document.getElementById('cudMailingPoaTypeError'),
        mailingPoaType.value ? '' : 'Choose the proof of the mailing address'), mailingPoaType);
    }
    need(fieldError(payMode, document.getElementById('cudPayModeError'),
      payMode.value ? '' : 'Choose the payment mode'), payMode);
    need(fieldError(sourcing, document.getElementById('cudSourcingError'),
      sourcing.value ? '' : 'Choose the sourcing mode'), sourcing);
    if (sourcing.value === 'BROKER') {
      need(fieldError(broker, document.getElementById('cudBrokerError'),
        broker.value.trim() ? '' : 'Enter the broker code'), broker);
    }
    need(fieldError(category, document.getElementById('cudCategoryError'),
      category.value ? '' : 'Choose the deposit category'), category);
    if (category.value === EMPLOYEE_CATEGORY) {
      // A code this screen cannot put a name to is not a reason to stop: the
      // staff register is Operations' to check, and the note under the field
      // says as much.
      need(fieldError(empCode, document.getElementById('cudEmpCodeError'),
        empCode.value.trim() ? '' : 'Enter the employee code'), empCode);
      need(fieldError(empCompany, document.getElementById('cudEmpCompanyError'),
        empCompany.value.trim() ? '' : 'Enter the employee company name'), empCompany);
      need(fieldError(empHolder, document.getElementById('cudEmpHolderError'),
        empHolder.value ? '' : 'Choose which holder is the employee'), empHolder);
      need(fieldError(empRelation, document.getElementById('cudEmpRelationError'),
        empRelation.value ? '' : 'Choose the relation with the holder'), empRelation);
      need(fieldError(empProofType, document.getElementById('cudEmpProofTypeError'),
        empProofType.value ? '' : 'Choose the employee proof'), empProofType);
    }
    if (appType.value === 'PHYSICAL') {
      need(fieldError(formNo, document.getElementById('cudFormNoError'),
        formNo.value.trim() ? '' : 'Enter the physical form number'), formNo);
    }

    ['form', 'pan', 'poa', 'mailing', 'photo', 'payment', 'empproof'].forEach(function (key) {
      var slot = slots[key];
      if (!slot.used) return;
      if (!slot.file && !slot.filed) {
        setError(slot, 'This document is required');
        if (!first) first = slot.drop;
      }
    });

    if (first) {
      first.focus();
      first.scrollIntoView({ block: 'center', behavior: 'smooth' });
      return;
    }
    window.showToast('Investor Information is not built in this mock yet.');
  });

  if (JSON.parse(document.getElementById('cudPanFiled').textContent)) {
    slots.pan.filed = 'PAN copy on the application';
    slots.pan.filedOriginal = slots.pan.filed;
  }

  syncAppType();
  syncMailing();
  syncPayMode();
  syncSourcing();
  syncCategory();
  Object.keys(slots).forEach(function (key) { show(slots[key]); });
  syncFooter();
})();
