// The classic upload step. A file is read where it was picked and named back to
// the partner - nothing is uploaded anywhere, and nothing is sent to CKYC.
(function () {
  var form = document.getElementById('cudForm');
  if (!form) return;

  // The application type is the new design's segmented toggle, not a select. The
  // rest of the step asks it the same questions, so it answers them the same way:
  // a value to read and set, a focus, and a change to listen for.
  var appTypeOpts = Array.prototype.slice.call(
    document.querySelectorAll('[data-apptype] .seg-toggle__opt'));

  function markAppType(next) {
    appTypeOpts.forEach(function (opt) {
      var on = opt.dataset.value === next;
      opt.classList.toggle('seg-toggle__opt--active', on);
      opt.setAttribute('aria-checked', on ? 'true' : 'false');
    });
  }

  var appType = {
    get value() {
      var on = appTypeOpts.filter(function (o) {
        return o.classList.contains('seg-toggle__opt--active');
      })[0];
      return on ? on.dataset.value : '';
    },
    set value(next) { markAppType(next); },
    focus: function () {
      var on = appTypeOpts.filter(function (o) {
        return o.classList.contains('seg-toggle__opt--active');
      })[0] || appTypeOpts[0];
      if (on) on.focus();
    },
    // Pressed rather than picked from a list, so the click is the change.
    addEventListener: function (type, fn) {
      appTypeOpts.forEach(function (opt) {
        opt.addEventListener('click', function () {
          if (opt.classList.contains('seg-toggle__opt--active')) return;
          markAppType(opt.dataset.value);
          fn();
        });
      });
    },
  };
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
  // What a digital application carries where a paper one carries its form number.
  var DIGITAL_FORM_NO = '0000';
  var typedFormNo = '';
  var sourcing = document.getElementById('cudSourcing');
  var category = document.getElementById('cudCategory');
  var categoryOnly = document.getElementById('cudCategoryOnly');
  var categoryOnlyWhy = categoryOnly.querySelector('[data-only-why]');
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
  var EMPLOYEE_CATEGORY = 'EMPLOYEE';

  // How the application is sourced, and the two registers a code is searched
  // against. Everything under Additional Details follows from the mode: what the
  // two code fields are called, which of them is typed, what the other carries,
  // and what the deposit may be booked as.
  var SOURCING = JSON.parse(document.getElementById('cudSourcingModes').textContent);

  function group(name) {
    return Array.prototype.slice.call(document.querySelectorAll('[data-when="' + name + '"]'));
  }

  var MB = 1024 * 1024;

  // Three refusals in a row for each document in this session, not three uploads:
  // a copy the checks take clears the count, because a partner who has just filed
  // the right document is not one attempt from being locked out of replacing it.
  // Three that come back refused one after another is a copy the screen cannot
  // settle, and it goes to Operations.
  //
  // The count is per document and per session: the PAN copy running out says
  // nothing about the proof of address, and what earlier sessions tried is on the
  // record without counting against what can be tried now. A file turned away for
  // its type or its size never reached the document, so it costs nothing.
  var MAX_ATTEMPTS = 3;

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
      // The stage the copy is on while the checks run, and the line that keeps
      // what the last of them came back with once it is filed.
      checkText: el.querySelector('[data-checking]'),
      stepNote: el.querySelector('[data-step]'),
      check: el.querySelector('[data-check]'),
      // Set while the document is being identified, so the box shows neither the
      // picker nor a file it may yet hand back.
      checking: false,
      // Set when the last thing handed over was turned away. What is on the card
      // is then the copy the application already carried, not what the partner
      // just tried to file, so the card has nothing to show them: the way on is
      // to upload again.
      refused: false,
      name: el.querySelector('[data-file]'),
      size: el.querySelector('[data-size]'),
      na: el.querySelector('[data-na]'),
      naWhy: el.querySelector('[data-na-why]'),
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
      tools: el.querySelector('.cud-slot__tools'),
      said: el.querySelector('[data-said]'),
      must: el.querySelector('[data-must]'),
      withWho: el.querySelector('[data-with]'),
      lockBox: el.querySelector('.cud-drop__locked'),
      lockText: el.querySelector('[data-lock]'),
      // Set for a slot that cannot take anything yet, and why.
      locked: '',
      // Refusals in a row. A document the checks take puts it back to nothing.
      attempts: 0,
      file: null,
      // A slot the application does not use takes nothing and is asked for
      // nothing; NOT APPLICABLE stands where its picker would be.
      used: true,
    };
  });

  // Said once, quietly, to whatever is reading the page aloud: the cards change
  // colour and glyph, which is no use to a screen reader.
  function say(slot, words) {
    if (slot.said) slot.said.textContent = words;
  }

  // ----- What is happening to the copy, while it happens ----------------------
  // A document handed over here is put through three checks before it is filed:
  // it is identified, OCR reads what it says, and whoever keeps the register
  // behind it is asked to confirm that reading. None of that is instant, and a
  // spinner that says nothing for three seconds is how a partner ends up
  // uploading the same file twice. The box names the stage it is on instead.
  function stage(slot, words, step) {
    slot.checkText.textContent = words;
    slot.stepNote.textContent = step || '';
    // The same words on the wait screen: the checks take a few seconds, and a
    // partner who cannot see one running picks the file again.
    if (window.showLoader) window.showLoader(words, step);
    say(slot, words);
  }

  // What the last check answered, kept on the card the document sits in. A copy
  // nobody could confirm is filed all the same - it is the application that
  // stands still, not the upload - so the line says which of the two happened
  // rather than leaving a green tick to speak for both.
  function setCheck(slot, words, kind) {
    slot.check.textContent = words || '';
    slot.check.className = 'cud-slot__check' + (kind ? ' is-' + kind : '');
    slot.check.hidden = !words;
    slot.el.classList.toggle('is-unconfirmed', kind === 'warn' || kind === 'bad');
  }

  // The end of the run, whatever it came back with: the box stops checking, the
  // card says what was answered, and the step counts the document as filed.
  function finish(slot, words, kind) {
    slot.checking = false;
    if (window.hideLoader) window.hideLoader();
    setCheck(slot, words, kind);
    // The card changes colour and glyph, which is nothing to a screen reader: it
    // hears the answer the same as everyone else.
    if (words) say(slot, words);
    show(slot);
    syncFooter();
  }

  // A register's name reads mid-sentence as "the Income Tax Department" and at
  // the head of one as "The Income Tax Department".
  function cap(words) {
    return words.charAt(0).toUpperCase() + words.slice(1);
  }

  // The history at the foot of the step holds what the checks said, and the page
  // is long: a refusal carries a way back to its own entry rather than leaving the
  // partner to go looking for it.
  function setError(slot, message, entry) {
    slot.error.textContent = message || '';
    slot.error.hidden = !message;
    slot.el.classList.toggle('is-invalid', !!message);
    if (message && entry) {
      var link = document.createElement('button');
      link.type = 'button';
      link.className = 'cud-why btn-reset';
      link.textContent = 'See the history';
      link.addEventListener('click', function () {
        entry.row.scrollIntoView({ block: 'center', behavior: 'smooth' });
        entry.row.classList.add('is-called');
        window.setTimeout(function () { entry.row.classList.remove('is-called'); }, 1600);
      });
      slot.error.appendChild(document.createTextNode(' '));
      slot.error.appendChild(link);
    }
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
    var locked = !!slot.locked && !has;
    slot.idle.hidden = has || slot.checking || locked;
    slot.lockBox.hidden = !locked || slot.checking;
    slot.lockText.textContent = slot.locked;
    slot.busy.hidden = !slot.checking;
    slot.done.hidden = !has || slot.checking;
    slot.drop.classList.toggle('is-busy', slot.checking);
    // Once a document is in the box, the box is a view of it rather than a second
    // way to pick one: it opens the sheet on click, and replacing goes through the
    // arrows above it.
    slot.drop.disabled = slot.checking || (used && !has) || locked;
    slot.drop.classList.toggle('is-filled', has && !slot.checking);
    slot.drop.classList.toggle('is-spent', used && !has);
    slot.drop.classList.toggle('is-locked', locked);
    // Nothing to view or replace until something is filed, so the buttons are
    // not there rather than there and grey.
    slot.tools.hidden = !has || !slot.used || slot.checking;
    slot.view.disabled = !has || slot.checking || slot.refused;
    // Three against the application, whatever they came back with. A document
    // already filed cannot be swapped once they are gone either.
    slot.replace.disabled = !has || slot.checking || used;
    // What the checks answered belongs to the document on the card: it goes
    // while another is being put to them, and with the card when it is emptied.
    slot.check.hidden = !slot.check.textContent || slot.checking || !has;
    slot.tries.hidden = slot.attempts === 0;
    slot.tries.textContent = used
      ? MAX_ATTEMPTS + ' refused one after another — this document now goes to Operations.'
      : slot.attempts + ' of ' + MAX_ATTEMPTS + ' refused in a row this session — a copy the checks take clears it.';
    slot.tries.classList.toggle('is-spent', used);
    if (slot.file) {
      slot.name.textContent = slot.file.name;
      slot.size.textContent = sizeOf(slot.file.size);
    } else if (slot.filed) {
      slot.name.textContent = slot.filed;
      slot.size.textContent = 'already on the application';
    }
  }

  // ----- The copy a refusal leaves behind --------------------------------------
  // A document that is turned away is the one support needs: it is what the
  // partner is complaining about. The copy is kept aside under its own reference
  // for a week - long enough to be looked at, short enough that someone's KYC
  // papers are not sitting in a store for ever - and deleted after. A document
  // that was filed goes to DMS instead and is not in here.
  var REJECT_SEQ = 884200;

  function rejectRef() {
    REJECT_SEQ += 1;
    return 'REJ-' + REJECT_SEQ;
  }

  function inAWeek() {
    var d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
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
    // The try being made now, of the three a run of refusals is allowed.
    row.querySelector('.cud-log__try').textContent = 'Attempt ' + (slot.attempts + 1) + ' of ' + MAX_ATTEMPTS;
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
    var slot = slots[key];
    var card = readCards[key];
    // No card for this document means nothing was read, so the box cannot be
    // left checking against an answer that is never coming.
    if (!card) { finish(slot, ''); return; }
    var type = (key === 'mailing' ? mailingPoaType.value : poaType.value);
    var named = type ? type.toLowerCase() : 'proof';
    var issuer = ADDRESS.issuers[type] || '';
    var read = ADDRESS.read[key];

    setReadState(card, 'Reading the address…', 'is-working');
    window.setTimeout(function () {
      logStage(entry, 'OCR read: ' + read);
      // A bill has no register behind it, so there is nobody to confirm it with
      // and the address the application carries stands until Operations say so.
      if (!issuer) {
        setReadState(card, 'With Operations', 'is-failed');
        card.from.textContent = 'A ' + named + ' has no issuer to check with, so the address is left as it stands for Operations to settle.';
        logStage(entry, 'A ' + named + ' has no register behind it to put that address to.', 'warn');
        logEnd(entry, 'Filed, address unchanged', 'warn');
        finish(slot, 'Read, but nothing outside answers for a ' + named + '. The copy is filed and '
          + 'Operations settle the address; the application keeps the one it carries until they do.', 'warn');
        return;
      }
      stage(slot, 'Checking with ' + issuer + '\u2026',
        'Step 3 of 3 \u00b7 ' + issuer + ' is asked whether that is the address they hold');
      show(slot);
      setReadState(card, 'Checking with ' + issuer + '…', 'is-working');
      window.setTimeout(function () {
        if (!issuerConfirms(file)) {
          setReadState(card, 'Not confirmed', 'is-failed');
          card.from.textContent = issuer + ' did not confirm the address on this proof, so the application keeps the address it carries. Upload a clearer copy, or a different proof.';
          logStage(entry, issuer + ' did not confirm that address.', 'bad');
          logEnd(entry, 'Filed, address unchanged', 'warn');
          finish(slot, 'Read, but ' + issuer + ' did not confirm what it says. The copy is filed and the '
            + 'application keeps the address it carries \u2014 upload a clearer copy, or another proof.', 'bad');
          return;
        }
        var before = card.lines.textContent;
        card.lines.textContent = read;
        card.was.textContent = 'Was: ' + before;
        card.was.hidden = false;
        setReadState(card, 'Verified with ' + issuer, 'is-done');
        card.from.textContent = 'Read off the ' + named + ' filed above and confirmed with ' + issuer + '.';
        logStage(entry, issuer + ' confirmed that address.', 'ok');
        logStage(entry, 'Address on the application replaced. Was: ' + before, 'ok');
        logEnd(entry, 'Filed', 'ok');
        finish(slot, 'Identified, read and confirmed with ' + issuer
          + '. The address on the application now comes from this proof.', 'ok');
      }, 900);
    }, 700);
  }

  // A cheque carries an account rather than an address, and it is the bank it is
  // drawn on that is asked to stand behind it. The account goes no further than
  // this card until that answer comes back clean.
  function readInstrument(file, entry) {
    var slot = slots.payment;
    var card = readCards.payment;
    if (!card) { finish(slot, ''); return; }
    var mode = (payMode.value || 'cheque').toLowerCase();
    var bank = ADDRESS.bank;

    setReadState(card, 'Reading the instrument\u2026', 'is-working');
    window.setTimeout(function () {
      logStage(entry, 'OCR read: ' + ADDRESS.read.payment);
      stage(slot, 'Checking with ' + bank + '\u2026',
        'Step 3 of 3 \u00b7 ' + bank + ' is asked whether it holds that account');
      show(slot);
      setReadState(card, 'Checking with ' + bank + '\u2026', 'is-working');
      window.setTimeout(function () {
        if (!issuerConfirms(file)) {
          setReadState(card, 'Not confirmed', 'is-failed');
          card.from.textContent = bank + ' did not confirm that account against this ' + mode
            + ', so nothing is carried forward. Upload a clearer copy of the instrument.';
          logStage(entry, bank + ' did not confirm that account.', 'bad');
          logEnd(entry, 'Filed, account not carried', 'warn');
          finish(slot, 'Read, but ' + bank + ' did not confirm the account on this ' + mode
            + '. The copy is filed and no account is carried to Bank Details & Payment.', 'bad');
          return;
        }
        card.lines.textContent = ADDRESS.read.payment;
        setReadState(card, 'Confirmed with ' + bank, 'is-done');
        card.from.textContent = 'Read off the ' + mode + ' filed above and confirmed with ' + bank
          + '. Bank Details & Payment opens with this account.';
        logStage(entry, bank + ' confirmed that account.', 'ok');
        logStage(entry, 'Account carried to Bank Details & Payment.', 'ok');
        logEnd(entry, 'Filed', 'ok');
        finish(slot, 'Identified, read and confirmed with ' + bank
          + '. Bank Details & Payment opens with this account.', 'ok');
      }, 900);
    }, 700);
  }

  // A PAN copy is read for the number on it, and the Income Tax Department is
  // asked whether it holds an Aadhaar against that number. An unlinked PAN does
  // not stop the application - it is booked, and TDS runs at the higher rate
  // until the investor links it - so the card says so rather than refusing.
  function readPan(file, entry) {
    var slot = slots.pan;
    var card = readCards.pan;
    if (!card) { finish(slot, ''); return; }
    var pan = ADDRESS.read.pan;
    var who = ADDRESS.panAuthority;

    setReadState(card, 'Reading the PAN\u2026', 'is-working');
    window.setTimeout(function () {
      logStage(entry, 'OCR read: PAN ' + pan);
      stage(slot, 'Checking with ' + who + '\u2026',
        'Step 3 of 3 \u00b7 ' + who + ' is asked whether it holds an Aadhaar against this PAN');
      show(slot);
      setReadState(card, 'Checking with ' + who + '\u2026', 'is-working');
      window.setTimeout(function () {
        // Named as unlinked, it comes back unlinked: the mock reads the file name
        // the way it reads every other document.
        var linked = file.name.toLowerCase().indexOf('unlinked') === -1
          && file.name.toLowerCase().indexOf('notlinked') === -1;
        if (linked) {
          card.lines.textContent = pan + ' \u00b7 linked with Aadhaar';
          setReadState(card, 'Linked with Aadhaar', 'is-done');
          card.from.textContent = 'Confirmed with ' + who + ' on the copy filed above.';
          logStage(entry, cap(who) + ' holds an Aadhaar against this PAN.', 'ok');
          logEnd(entry, 'Filed', 'ok');
          finish(slot, 'Identified, read as PAN ' + pan + ' and confirmed with ' + who
            + ': an Aadhaar is held against it.', 'ok');
          return;
        }
        card.lines.textContent = pan + ' \u00b7 not linked with Aadhaar';
        setReadState(card, 'Not linked', 'is-failed');
        card.from.textContent = cap(who)
          + ' holds no Aadhaar against this PAN. The deposit can still be booked, but TDS runs at the higher rate until the investor links it.';
        logStage(entry, 'No Aadhaar against this PAN.', 'warn');
        logEnd(entry, 'Filed, PAN not linked', 'warn');
        finish(slot, 'Read as PAN ' + pan + ', but ' + who + ' holds no Aadhaar against it. The copy is '
          + 'filed and the deposit can be booked \u2014 TDS runs at the higher rate until the investor links it.', 'warn');
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
      what: 'a cheque', named: 'the cheque',
      tokens: ['cheque', 'chq'],
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

  // The three checks as the partner sees them. The box stays busy through all of
  // them and names the one it is on, because a document is not filed until the
  // last has answered: a green tick the moment the file lands would say the copy
  // is good when nobody has yet been asked about it.
  function checkDocument(slot, rule, file, entry) {
    slot.checking = true;
    setError(slot, '');
    setCheck(slot, '');
    stage(slot, 'Identifying the ' + slot.label + '\u2026',
      'Step 1 of 3 \u00b7 is this ' + rule.what + '?');
    show(slot);
    syncFooter();
    window.setTimeout(function () {
      if (identified(rule, file) && maskedAadhaar(slot, file)) {
        slot.checking = false;
        if (window.hideLoader) window.hideLoader();
        // Refused, so it joins the run: three in a row and the document goes.
        slot.attempts += 1;
        var maskedRef = rejectRef();
        slot.refused = true;
        slot.input.value = '';
        setError(slot, 'That Aadhaar is masked. The application needs the unmasked copy, with '
          + 'all 12 digits readable \u2014 ' + (spent(slot)
            ? 'and that is ' + MAX_ATTEMPTS + ' refused one after another, so it now goes to Operations. '
              + 'Book a service call to file it, quoting ' + maskedRef + '.'
            : 'upload it again. The copy is kept for a week as ' + maskedRef + '.'), entry);
        logStage(entry, 'Identified as an Aadhaar, masked.', 'bad');
        logStage(entry, 'An Aadhaar is filed unmasked, so this copy was not taken.', 'bad');
        logStage(entry, 'Copy kept for analysis as ' + maskedRef + ' until ' + inAWeek()
          + ', and deleted after.', 'warn');
        logEnd(entry, 'Refused', 'bad');
        say(slot, 'That Aadhaar is masked. Upload the unmasked copy.');
        show(slot);
        syncFooter();
        return;
      }
      if (identified(rule, file)) {
        slot.file = file;
        slot.filed = null;
        // The copy was taken, so whatever was refused before it is behind the
        // partner: the run starts again from nothing.
        slot.attempts = 0;
        logStage(entry, 'Identified as ' + rule.what + '.', 'ok');
        // Identified, so OCR has something to read. The box carries on saying so
        // until whoever stands behind the document has answered.
        stage(slot, 'Running OCR on the ' + slot.label + '\u2026',
          'Step 2 of 3 \u00b7 reading what the copy says');
        show(slot);
        if (slot.key === 'poa' || slot.key === 'mailing') {
          readAddress(slot.key, file, entry);
        } else if (slot.key === 'payment') {
          readInstrument(file, entry);
        } else if (slot.key === 'pan') {
          readPan(file, entry);
        } else {
          logEnd(entry, 'Filed', 'ok');
          finish(slot, 'Identified and filed.', 'ok');
        }
        return;
      }

      slot.checking = false;
      if (window.hideLoader) window.hideLoader();
      // Nothing is filed: the slot asks for the document again, and what it
      // still holds is not what was just tried, so there is nothing to view.
      slot.attempts += 1;
      slot.refused = true;
      slot.input.value = '';
      // Telling a partner to upload it again when there is nothing left to
      // upload with is worse than saying nothing: on the last attempt the
      // message says where the document goes instead.
      var kept = rejectRef();
      setError(slot, spent(slot)
        ? 'That does not read as ' + rule.what + ', and that is ' + MAX_ATTEMPTS
          + ' refused one after another. It now goes to Operations — book a service call to file it, quoting ' + kept + '.'
        : 'That does not read as ' + rule.what + '. This mock identifies a document by its '
          + 'file name, so upload it again with ' + rule.named + ' in the name. The copy is kept for a week as ' + kept + '.', entry);
      logStage(entry, 'Not identified as ' + rule.what + '.', 'bad');
      say(slot, slot.label + ' was not identified. Upload it again.');
      logStage(entry, 'Copy kept for analysis as ' + kept + ' until ' + inAWeek()
        + ', and deleted after.', 'warn');
      logEnd(entry, 'Refused', 'bad');
      show(slot);
      syncFooter();
    }, 700);
  }

  // What a photograph has to be to be worth comparing a face against. The
  // dimensions are read off the image itself rather than assumed from its weight.
  var FACE = { minKb: 15, maxKb: 2048, minPx: 150, maxPx: 4096 };

  function checkPhoto(file, ok, no) {
    var kb = file.size / 1024;
    if (kb < FACE.minKb) {
      no('That photograph is ' + Math.round(kb) + ' KB. A face cannot be compared '
        + 'against anything under ' + FACE.minKb + ' KB.');
      return;
    }
    if (kb > FACE.maxKb) {
      no('That photograph is ' + Math.round(kb) + ' KB, over the ' + FACE.maxKb + ' KB a photograph may be.');
      return;
    }
    var url = window.URL.createObjectURL(file);
    var img = new window.Image();
    img.onload = function () {
      var w = img.naturalWidth, h = img.naturalHeight;
      window.URL.revokeObjectURL(url);
      if (w < FACE.minPx || h < FACE.minPx) {
        no('That photograph is ' + w + '\u00d7' + h + ' pixels. It must be at least '
          + FACE.minPx + ' pixels on both sides.');
        return;
      }
      if (w > FACE.maxPx || h > FACE.maxPx) {
        no('That photograph is ' + w + '\u00d7' + h + ' pixels, over the ' + FACE.maxPx
          + ' a side that can be handled.');
        return;
      }
      ok();
    };
    img.onerror = function () {
      window.URL.revokeObjectURL(url);
      no('That file could not be read as a photograph.');
    };
    img.src = url;
  }

  // Not every document has somebody behind it to ask. A form, a photograph and an
  // employee proof are read by people rather than registers, and a card that says
  // nothing about that looks like a card whose check has not come back yet: it
  // says who does look at the copy instead, and when.
  var NOCHECK = {
    form: 'Filed as handed over. No register outside answers for an application form \u2014 Operations '
      + 'check it against the application before the deposit is booked.',
    photo: 'Filed as handed over. No register outside answers for a photograph \u2014 Operations compare '
      + 'it with the KYC record before the deposit is booked.',
    empproof: 'Filed as handed over. No register outside answers for an employee proof \u2014 Operations '
      + 'check it against the staff register.',
    other: 'Filed as handed over. Nothing outside was asked about this document \u2014 Operations check '
      + 'it before the deposit is booked.',
  };

  function spent(slot) {
    return slot.attempts >= MAX_ATTEMPTS;
  }

  // Everything that has to be true of the file itself is true by the time this
  // runs: from here it is the document that is being checked, and that is what an
  // attempt is spent on.
  function put(slot, file) {
    slot.refused = false;
    var entry = logStart(slot, file);
    if (IDENTIFY[slot.key]) {
      checkDocument(slot, IDENTIFY[slot.key], file, entry);
      return;
    }
    setError(slot, '');
    slot.file = file;
    slot.filed = null;
    // Taken, so whatever was refused before it is behind the partner.
    slot.attempts = 0;
    logStage(entry, 'Filed as handed over \u2014 nothing outside answers for this document.');
    logEnd(entry, 'Filed', 'ok');
    setCheck(slot, NOCHECK[slot.key] || NOCHECK.other, '');
    say(slot, slot.label + ' filed.');
    show(slot);
    syncFooter();
  }

  function take(slot, file) {
    if (!file) return;
    if (slot.locked) {
      setError(slot, slot.locked);
      slot.input.value = '';
      show(slot);
      return;
    }
    if (spent(slot)) {
      setError(slot, MAX_ATTEMPTS + ' copies of this document were refused one after another this '
        + 'session, so it is no longer filed here — book a service call to file it.');
      show(slot);
      return;
    }
    var limit = limitOf(slot);
    var ok = slot.input.accept.split(',').indexOf(file.type) > -1;
    if (!ok) {
      setError(slot, 'That file type is not accepted here');
    } else if (file.size > limit * MB) {
      setError(slot, 'The file is over ' + limit + ' MB — ' + sizeOf(file.size));
    } else if (slot.key === 'photo') {
      // Read before anything is spent: a file that cannot be a photograph never
      // reached the document.
      checkPhoto(file, function () { put(slot, file); }, function (why) {
        setError(slot, why);
        slot.input.value = '';
        show(slot);
      });
      return;
    } else {
      put(slot, file);
      return;
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
    slot.refused = false;
    setCheck(slot, '');
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
  function use(slot, used, why) {
    if (why && slot.naWhy) slot.naWhy.textContent = why;
    if (slot.used === used) return;
    slot.used = used;
    if (!used) {
      slot.file = null;
      slot.filed = null;
      slot.input.value = '';
      setError(slot, '');
      setCheck(slot, '');
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

  // DIGITAL is accepted through the investor's own link, so there is no signed
  // form to file and no form number to quote; CKYC answers for it instead.
  function syncAppType() {
    var physical = appType.value === 'PHYSICAL';
    use(slots.form, physical,
      'A digital application is accepted through the investor\u2019s own link, so there is no signed form to file.');
    // A digital application has no paper form, and the register will not take an
    // empty field for one: it is filed as 0000. The field says so and is not
    // typed into. A number typed for a paper application is kept through a change
    // of mind and put back if the type changes again.
    formNoOptional.hidden = physical;
    if (physical) {
      formNo.readOnly = false;
      if (formNo.value === DIGITAL_FORM_NO) formNo.value = typedFormNo;
    } else {
      if (formNo.value !== DIGITAL_FORM_NO) typedFormNo = formNo.value;
      formNo.value = DIGITAL_FORM_NO;
      formNo.readOnly = true;
      fieldError(formNo, document.getElementById('cudFormNoError'), '');
    }
    // The other half of the pair: a CKYC application closes off the paper route,
    // and a paper application closes off CKYC. Consent is given online, through
    // the link the investor is sent, so there is nothing to fetch for a form that
    // is signed and filed by hand.
    if (ckyc) ckyc.disabled = physical;
    var route = document.getElementById('cudRoute');
    var routeNote = document.getElementById('cudCkycNote');
    if (route && !ckycAsked) {
      route.classList.toggle('is-off', physical);
      if (routeNote) routeNote.textContent = physical
        ? 'A physical application is signed and filed by hand, so the CERSAI record is not fetched for it.'
        : 'Once fetched, this cannot be undone on this application.';
    }
    syncFooter();
  }

  // Address proof answers for the permanent address. Where the investor is
  // written to somewhere else, that address is proved on its own: a second type
  // and picker, asked for only while the two differ.
  // A mailing address of its own may be off the page for this release, in which
  // case there is no question to answer and the permanent address stands alone.
  function mailingAsked() {
    return !!mailingCol && !!mailingPoaType;
  }

  function mailingDiffers() {
    if (!mailingAsked()) return false;
    var picked = document.querySelector('input[name="cudMailing"]:checked');
    return !!picked && picked.value === 'other';
  }

  function syncMailing() {
    if (!mailingAsked()) return;
    var differs = mailingDiffers();
    // The column keeps its place in the row whatever the answer is: a proof that
    // is not asked for reads as NOT APPLICABLE, the way a digital application's
    // form does, rather than leaving a hole where a document used to be.
    mailingCol.hidden = false;
    mailingPoaType.disabled = !differs;
    use(slots.mailing, differs,
      'The investor is written to at the address their proof carries, so there is nothing further to prove.');
    readCards.mailing.el.hidden = !differs;
    if (!differs) {
      mailingPoaType.value = '';
      fieldError(mailingPoaType, document.getElementById('cudMailingPoaTypeError'), '');
      resetRead('mailing');
    }
    syncPoaTypes();
    syncFooter();
  }

  // A proof of address is checked as whatever type was chosen above it, and with
  // whoever stands behind that type, so there is nothing to check it as until one
  // is. The picker stays shut and says so.
  // An Aadhaar is filed unmasked: the application needs all twelve digits, and a
  // masked e-Aadhaar is the copy a partner reaches for first, so the picker says
  // so before the file is chosen rather than after.
  var UNMASKED = 'Unmasked copy only \u2014 all 12 digits must be readable.';

  function poaTypeOf(slot) {
    return slot.key === 'mailing' ? mailingPoaType.value : poaType.value;
  }

  function sayMust(slot) {
    if (!slot.must) return;
    var must = poaTypeOf(slot) === 'Aadhaar' ? UNMASKED : '';
    slot.must.textContent = must;
    slot.must.hidden = !must;
  }

  // ----- What the copy is in for, said before it is picked ---------------------
  // The three checks are named on the empty box too. A partner who knows UIDAI
  // will be asked reaches for the copy UIDAI would recognise, rather than
  // finding out after the third attempt is spent.
  var RUN = 'Once uploaded: identified, read by OCR, then ';

  function sayWith(slot) {
    if (!slot.withWho) return;
    var words = '';
    if (slot.key === 'pan') {
      words = RUN + 'checked with ' + ADDRESS.panAuthority + '.';
    } else if (slot.key === 'poa' || slot.key === 'mailing') {
      var type = poaTypeOf(slot);
      var issuer = type ? ADDRESS.issuers[type] : undefined;
      if (issuer) {
        words = RUN + 'the address is confirmed with ' + issuer + '.';
      } else if (type) {
        // Nobody keeps a register of bills, so the address on the application
        // stands whatever this copy says. Better said now than after it is filed.
        words = 'Once uploaded: identified and read by OCR. A ' + type.toLowerCase()
          + ' has no issuer to confirm the address with, so Operations settle it.';
      }
    } else if (slot.key === 'payment') {
      words = RUN + 'the account is confirmed with the bank it is drawn on.';
    }
    slot.withWho.textContent = words;
    slot.withWho.hidden = !words;
  }

  function syncEmpProof() {
    if (!slots.empproof) return;
    slots.empproof.locked = (empProofType.value || !slots.empproof.used)
      ? '' : 'Choose the employee proof first';
    show(slots.empproof);
  }

  function syncPoaTypes() {
    slots.poa.locked = (poaType.value || ckycAsked || !slots.poa.used)
      ? '' : 'Choose the proof of address first';
    sayMust(slots.poa);
    sayWith(slots.poa);
    show(slots.poa);
    if (!mailingAsked() || !slots.mailing) return;
    slots.mailing.locked = mailingPoaType.value ? '' : 'Choose the mailing address proof first';
    sayMust(slots.mailing);
    sayWith(slots.mailing);
    show(slots.mailing);
  }

  // The mock reads the file name for this as it does for everything else: a copy
  // named as masked comes back as masked, and "unmasked" is not.
  function maskedAadhaar(slot, file) {
    if (poaTypeOf(slot) !== 'Aadhaar') return false;
    var name = file.name.toLowerCase();
    return name.indexOf('masked') > -1 && name.indexOf('unmasked') === -1;
  }

  // Only a mode settled by an instrument carries a document.
  function syncPayMode() {
    var picked = payMode.options[payMode.selectedIndex];
    var document_ = picked ? picked.dataset.document : '';
    use(slots.payment, !!document_,
      (payMode.value || 'This mode') + ' is settled electronically, so there is no instrument to copy.');
    // Only a mode settled by an instrument has an account to read off one.
    var instrumentCard = readCards.payment;
    if (instrumentCard) instrumentCard.el.hidden = !document_;
    if (!document_) resetRead('payment');
    if (document_) {
      slots.payment.el.querySelector('.cud-drop__title').textContent = 'Click to upload ' + document_;
    }
    syncFooter();
  }

  // ----- The two code fields ---------------------------------------------------
  // A code on its own says nothing: nine digits are nine digits. The old screen
  // searches the register three characters at a time and puts the name it holds
  // under the field, which is the only way a partner can tell they have the right
  // one. The mock searches what the page was given, the same way.
  var LEAST = 3;

  function finderFor(id, nameLabel) {
    var f = {
      input: document.getElementById(id),
      list: document.getElementById(id + 'List'),
      name: document.getElementById(id + 'Name'),
      error: document.getElementById(id + 'Error'),
      // The register this field is searched against under the mode in force, and
      // the house code the mode stands in it. Both are set by the mode.
      register: '',
      house: '',
      nameLabel: nameLabel,
      required: false,
      rows: [],
      active: -1,
    };

    f.input.addEventListener('input', function () {
      resolve(f);
      openFinder(f);
    });
    f.input.addEventListener('focus', function () { openFinder(f); });
    f.input.addEventListener('blur', function () {
      // A click on the list is a blur first: the list has to outlive it.
      window.setTimeout(function () { shutFinder(f); }, 120);
    });
    f.input.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') { shutFinder(f); return; }
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        if (f.list.hidden) { openFinder(f); return; }
        e.preventDefault();
        move(f, e.key === 'ArrowDown' ? 1 : -1);
        return;
      }
      if (e.key === 'Enter' && !f.list.hidden && f.active > -1 && f.rows[f.active]) {
        e.preventDefault();
        pick(f, f.rows[f.active]);
      }
    });
    return f;
  }

  function registerOf(f) {
    return (f.register && SOURCING.registers[f.register]) || [];
  }

  function matches(f, text) {
    var wanted = text.toUpperCase();
    return registerOf(f).filter(function (party) {
      return party.code.toUpperCase().indexOf(wanted) > -1
        || party.name.toUpperCase().indexOf(wanted) > -1;
    }).slice(0, 8);
  }

  // One row of the list, or one line of why there are none. A row that cannot be
  // picked is not in the listbox: it is a note, and says so.
  function listRow(f, party, index) {
    var li = document.createElement('li');
    li.className = 'cud-find__row';
    li.setAttribute('role', 'option');
    li.id = f.input.id + 'Row' + index;
    li.setAttribute('aria-selected', index === f.active ? 'true' : 'false');
    li.classList.toggle('is-active', index === f.active);
    li.innerHTML = '<span class="cud-find__code"></span><span class="cud-find__name"></span>';
    li.querySelector('.cud-find__code').textContent = party.code;
    li.querySelector('.cud-find__name').textContent = party.name;
    // mousedown, not click: the blur that closes the list would land first.
    li.addEventListener('mousedown', function (e) {
      e.preventDefault();
      pick(f, party);
    });
    return li;
  }

  function listNote(words) {
    var li = document.createElement('li');
    li.className = 'cud-find__note';
    li.textContent = words;
    return li;
  }

  function openFinder(f) {
    if (!f.register || f.input.disabled) { shutFinder(f); return; }
    var text = f.input.value.trim();
    f.list.innerHTML = '';
    f.rows = [];
    f.active = -1;
    if (text.length < LEAST) {
      f.list.appendChild(listNote('Type at least ' + LEAST + ' characters of the code or the name.'));
    } else {
      f.rows = matches(f, text);
      if (f.rows.length === 0) {
        f.list.appendChild(listNote('No record found.'));
      } else {
        f.rows.forEach(function (party, i) { f.list.appendChild(listRow(f, party, i)); });
      }
    }
    f.list.hidden = false;
    f.input.setAttribute('aria-expanded', 'true');
  }

  function shutFinder(f) {
    f.list.hidden = true;
    f.list.innerHTML = '';
    f.rows = [];
    f.active = -1;
    f.input.setAttribute('aria-expanded', 'false');
    f.input.removeAttribute('aria-activedescendant');
  }

  function move(f, by) {
    if (!f.rows.length) return;
    f.active = (f.active + by + f.rows.length) % f.rows.length;
    Array.prototype.slice.call(f.list.children).forEach(function (row, i) {
      if (!row.classList.contains('cud-find__row')) return;
      row.classList.toggle('is-active', i === f.active);
      row.setAttribute('aria-selected', i === f.active ? 'true' : 'false');
    });
    f.input.setAttribute('aria-activedescendant', f.input.id + 'Row' + f.active);
  }

  function pick(f, party) {
    f.input.value = party.code;
    shutFinder(f);
    resolve(f);
    fieldError(f.input, f.error, '');
    syncFooter();
    saveDraftSoon();
  }

  // The name the register holds against whatever is in the field. A house code
  // belongs to the mode rather than to anybody in a register, so it is not
  // searched for and does not come back unknown.
  function resolve(f) {
    var value = f.input.value.trim();
    if (!value) {
      f.name.hidden = true;
      f.name.textContent = '';
      return;
    }
    if (f.house && value.toUpperCase() === f.house.toUpperCase()) {
      f.name.textContent = 'Stands for the sourcing mode itself \u2014 filled here, not typed.';
      f.name.classList.add('cud-resolved__none');
      f.name.hidden = false;
      return;
    }
    var found = null;
    registerOf(f).forEach(function (party) {
      if (party.code.toUpperCase() === value.toUpperCase()) found = party;
    });
    if (found) {
      f.name.textContent = f.nameLabel + ' \u2014 ' + found.name;
      f.name.classList.remove('cud-resolved__none');
    } else {
      // Said the way the employee code says it: the application is not stopped by
      // a code this screen cannot put a name to.
      f.name.textContent = 'No name against this code here. You can still proceed \u2014 '
        + 'Operations check it before the deposit is booked.';
      f.name.classList.add('cud-resolved__none');
    }
    f.name.hidden = false;
  }

  var sourceCode = finderFor('cudSourceCode', 'Sourcing Employee Name');
  var subBroker = finderFor('cudSubBroker', 'Sub Broker Name');
  var sourceCodeLabel = document.getElementById('cudSourceCodeLabel');
  var subOptional = document.getElementById('cudSubBrokerOptional');

  // ----- What the sourcing mode settles ---------------------------------------
  // The old screen hangs the whole of Additional Details off this one answer:
  // what the two code fields are called, which of them is typed and which the
  // mode fills itself, whose register the typed one is searched against, and what
  // the deposit may be booked as. A field the mode fills is shut rather than
  // taken off the page, so the partner can see what they are sourcing under.
  function modeOf(value) {
    var found = null;
    SOURCING.modes.forEach(function (mode) {
      if (mode.code === String(value)) found = mode;
    });
    return found;
  }

  function setField(f, value, disabled, required) {
    f.input.value = value;
    f.input.disabled = disabled;
    f.required = required;
    f.input.setAttribute('aria-required', required ? 'true' : 'false');
    fieldError(f.input, f.error, '');
  }

  // Kept on the way in, dropped on a change of mode: coming back to a draft is
  // not the same as choosing the mode again.
  var restoredCategory = '';

  function syncSourcing(keep) {
    var mode = modeOf(sourcing.value);
    shutFinder(sourceCode);
    shutFinder(subBroker);

    if (!mode) {
      // What the old screen calls the field before a mode is chosen.
      sourceCodeLabel.textContent = 'Broker Code';
      sourceCode.register = '';
      subBroker.register = '';
      sourceCode.house = '';
      subBroker.house = '';
      setField(sourceCode, '', true, false);
      setField(subBroker, '', true, false);
      subOptional.hidden = true;
      resolve(sourceCode);
      resolve(subBroker);
      fillCategories(null);
      return;
    }

    sourceCodeLabel.textContent = mode.codeLabel;
    sourceCode.nameLabel = mode.nameLabel;
    sourceCode.house = mode.house;
    subBroker.house = mode.sub === 'house' ? mode.house : '';
    // Only one of the two is searched under any mode, and only against the
    // register that mode names.
    sourceCode.register = mode.search === 'source' ? mode.register : '';
    subBroker.register = mode.search === 'sub' ? mode.register : '';

    var typedSource = sourceCode.input.value.trim();
    var typedSub = subBroker.input.value.trim();

    if (mode.house) {
      setField(sourceCode, mode.house, true, true);
    } else {
      setField(sourceCode, keep ? typedSource : '', false, true);
    }

    if (mode.sub === 'house') {
      setField(subBroker, mode.house, true, false);
    } else if (mode.sub === 'shut') {
      setField(subBroker, '', true, false);
    } else if (mode.sub === 'free') {
      setField(subBroker, keep ? typedSub : '', false, false);
    } else {
      // Sourced by an employee: the application opens with the code of whoever is
      // at the keyboard, theirs to change under MFL-EX and not under MIBS.
      var shut = mode.sub === 'employeeShut';
      setField(subBroker, (keep && typedSub) || SOURCING.partner.code, shut, true);
    }

    subOptional.hidden = subBroker.required || subBroker.input.disabled;
    resolve(sourceCode);
    resolve(subBroker);
    fillCategories(mode);
  }

  // What a deposit may be booked as is not the same list under every mode - an
  // employee deposit only exists where an employee sourced it - so the mode fills
  // this. A mode with one category settles it and says why, rather than leaving a
  // choice that was made for no reason.
  function fillCategories(mode) {
    var want = category.value || restoredCategory;
    restoredCategory = '';
    category.innerHTML = '';
    var first = document.createElement('option');
    first.value = '';
    first.textContent = 'Select';
    category.appendChild(first);

    var list = mode ? mode.categories : [];
    list.forEach(function (name) {
      var option = document.createElement('option');
      option.value = name;
      option.textContent = name;
      category.appendChild(option);
    });

    category.disabled = !mode;
    categoryOnly.hidden = list.length !== 1;
    if (list.length === 1) {
      category.value = list[0];
      categoryOnlyWhy.textContent = 'An application sourced as ' + mode.name + ' can only be booked as '
        + list[0] + ', so it is chosen here.';
    } else if (want && list.indexOf(want) > -1) {
      category.value = want;
    }
    syncCategory();
  }

  // An employee deposit is booked against a staff record; every other category
  // asks none of it, and anything already typed goes with the block.
  function syncCategory() {
    var employee = category.value === EMPLOYEE_CATEGORY;
    group('employee').forEach(function (el) { el.hidden = !employee; });
    window.setTimeout(syncEmpProof, 0);
    use(slots.empproof, employee,
      'Only a deposit booked against a staff record carries an employee proof.');
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
  // The document that is still with the checks, if any. Until it comes back it
  // is a file on the page rather than a document on the application, so the step
  // counts it as outstanding and Proceed waits for it.
  function beingChecked() {
    var keys = Object.keys(slots);
    for (var i = 0; i < keys.length; i++) {
      if (slots[keys[i]].checking) return slots[keys[i]];
    }
    return null;
  }

  function outstanding() {
    var left = [];
    var busy = beingChecked();
    if (busy) left.push('the ' + busy.label + ', still with the checks');
    if (appType.value === 'PHYSICAL' && !slots.form.file) left.push('the application form');
    if (slots.pan.used && !slots.pan.file && !slots.pan.filed) left.push('the PAN copy');
    if (slots.poa.used && !poaType.value) left.push('the proof of address type');
    if (slots.poa.used && !slots.poa.file) left.push('the proof of address');
    if (mailingDiffers() && !mailingPoaType.value) left.push('the mailing address proof type');
    if (mailingDiffers() && !slots.mailing.file) left.push('the mailing address proof');
    if (slots.photo.used && !slots.photo.file) left.push('the photograph');
    if (!payMode.value) left.push('the payment mode');
    if (slots.payment.used && !slots.payment.file) left.push('the instrument copy');
    if (!sourcing.value) left.push('the sourcing mode');
    if (sourceCode.required && !sourceCode.input.value.trim()) {
      left.push('the ' + sourceCodeLabel.textContent.toLowerCase());
    }
    if (subBroker.required && !subBroker.input.value.trim()) left.push('the sub broker code');
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

  // The mailing type is only there in a release that asks for it.
  [poaType, mailingPoaType].forEach(function (select) {
    if (select) select.addEventListener('change', syncPoaTypes);
  });
  empProofType.addEventListener('change', syncEmpProof);

  [poaType, payMode, empHolder, empRelation, empProofType].forEach(function (select) {
    select.addEventListener('change', function () { syncFooter(); });
  });
  [sourceCode.input, subBroker.input, formNo, empCompany].forEach(function (field) {
    field.addEventListener('input', function () { syncFooter(); });
  });
  empCode.addEventListener('input', function () {
    resolveEmployee();
    syncFooter();
  });
  appType.addEventListener('change', syncAppType);
  payMode.addEventListener('change', syncPayMode);
  // A change of mode is a fresh answer: whatever was typed under the last one
  // goes, the way the old screen empties both fields before it fills them.
  sourcing.addEventListener('change', function () { syncSourcing(false); });
  category.addEventListener('change', syncCategory);

  mailingRadios.forEach(function (radio) {
    radio.addEventListener('change', syncMailing);
  });

  // ----- Fetching the record from CKYC -----------------------------------------
  // Nothing is requested of the investor from this step. Choosing CKYC only says
  // how the KYC will arrive: the consent link goes out once the application is
  // completed online, and the record is downloaded after the investor enters the
  // OTP sent to the CERSAI-registered number. What changes here is only what is
  // asked of the partner - the address and the photograph come with that record,
  // so those pickers stop being asked for. The PAN copy does not: the application
  // is filed against that card whichever way the KYC comes.
  var ckycAsk = document.getElementById('cudCkycAsk');
  var ckycOn = document.getElementById('cudCkycOn');
  var ckycNote = document.getElementById('cudCkycNote');
  var ckycAsked = false;
  var CKYC_WHY = 'CKYC supplies this once the investor consents, which is asked for after the application is completed. It is not uploaded here.';

  function openCkyc(open) {
    if (ckycAsk) ckycAsk.hidden = !open;
  }

  // Taking the CKYC route cannot be taken back within a session: the record is
  // fetched against this application, so there is nothing to fetch a second time.
  // It is not written into the draft, though - the page is not the record of what
  // the application is on. A reload starts the step where it started, and nothing
  // is disabled until the partner fetches in front of it. The real system holds
  // this against the application, and would send the page back in that state. What closes with it
  // is the physical application - consent is given online, through the link the
  // investor is sent, so there is no paper route left to take. Back still works:
  // the steps behind can be read, they just cannot turn this one around.
  function useCkyc(spoken) {
    if (ckycAsked || !ckyc) return;
    ckycAsked = true;
    ckyc.hidden = true;
    appType.value = 'DIGITAL';
    appTypeOpts.forEach(function (opt) {
      if (opt.dataset.value !== 'PHYSICAL') return;
      opt.disabled = true;
      opt.title = 'A CKYC application is completed online, so it cannot be filed on paper.';
    });
    syncAppType();
    // The panel stops offering and starts reporting.
    ckycNote.hidden = true;
    document.getElementById('cudRoute').classList.add('is-on');
    // True only now: until the record was fetched, this was an offer.
    document.getElementById('cudRouteTitle').textContent = 'KYC from CERSAI';
    document.getElementById('cudRouteOne').textContent =
      'The investor is asked for consent once the application is completed online: a link goes '
      + 'to their registered mobile number and email address, and they enter the OTP sent to the '
      + 'CERSAI-registered mobile number.';
    document.getElementById('cudRouteTwo').innerHTML =
      '<strong>The address and the photograph are CERSAI\u2019s to send.</strong> They are not '
      + 'uploaded here, and the record cannot be fetched again. The PAN copy is still filed below.';
    ckycOn.hidden = false;
    // The proof of address is CKYC's to supply, so its type is not asked for.
    poaType.value = '';
    poaType.options[0].textContent = 'Not Applicable';
    poaType.disabled = true;
    fieldError(poaType, document.getElementById('cudPoaTypeError'), '');
    // The PAN copy is asked for either way: CKYC answers for the address and the
    // photograph, not for the card the application is filed against.
    ['poa', 'photo'].forEach(function (key) {
      use(slots[key], false, CKYC_WHY);
    });
    resetRead('poa');
    syncFooter();
    if (spoken) window.showToast('The address and photograph will come from CKYC once the investor consents.');
  }

  if (ckyc && ckycAsk) {
    ckyc.addEventListener('click', function () { if (!ckycAsked) openCkyc(true); });
    document.getElementById('cudCkycClose').addEventListener('click', function () { openCkyc(false); });
    document.getElementById('cudCkycCancel').addEventListener('click', function () { openCkyc(false); });
    document.getElementById('cudCkycGo').addEventListener('click', function () {
      openCkyc(false);
      useCkyc(true);
    });
    ckycAsk.addEventListener('click', function (e) { if (e.target === ckycAsk) openCkyc(false); });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && !ckycAsk.hidden) openCkyc(false);
    });
  }

  // ----- What a screenshot has to carry ----------------------------------------
  // A partner reporting a problem sends a picture of the screen, and by then the
  // head has scrolled off: the strip keeps the name and the application number in
  // shot, and the copy button puts the number in the message they are typing.
  var pin = document.getElementById('cudPin');
  var head = document.querySelector('.cud-head');
  if (pin && head && window.IntersectionObserver) {
    new window.IntersectionObserver(function (entries) {
      pin.hidden = entries[0].isIntersecting;
    }, { rootMargin: '-52px 0px 0px 0px' }).observe(head);
  }

  document.querySelectorAll('.js-copy').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var text = btn.dataset.copy;
      var done = function () {
        btn.classList.add('is-copied');
        window.setTimeout(function () { btn.classList.remove('is-copied'); }, 1400);
        window.showToast('Application number copied \u00b7 ' + text);
      };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(done, function () {
          window.showToast('This browser would not let the page copy it.');
        });
        return;
      }
      // Older browsers: a field off the page, selected and copied the old way.
      var field = document.createElement('textarea');
      field.value = text;
      field.setAttribute('readonly', '');
      field.style.position = 'fixed';
      field.style.left = '-9999px';
      document.body.appendChild(field);
      field.select();
      try { document.execCommand('copy'); done(); } catch (e) { /* nothing to be done */ }
      document.body.removeChild(field);
    });
  });

  // ----- The draft ------------------------------------------------------------
  // Everything typed on the step, kept against this application so the partner
  // can come back to it. The documents themselves cannot be kept: a file lives on
  // the machine it was picked from, so a draft records what was filed and the
  // step asks for the copies again. Attempts are not part of the draft either -
  // they stand against the application whether or not anything is saved.
  var DRAFT_KEY = 'unotp.cud.draft.' + form.dataset.draft;
  var savedLine = document.getElementById('cudSaved');

  var draftFields = ['cudPoaType', 'cudMailingPoaType', 'cudPayMode',
    'cudSourcing', 'cudSourceCode', 'cudSubBroker', 'cudCategory', 'cudEmpCode', 'cudEmpCompany',
    'cudEmpHolder', 'cudEmpRelation', 'cudEmpProofType', 'cudFormNo'];

  function clockOf(date) {
    return [date.getHours(), date.getMinutes()]
      .map(function (n) { return n < 10 ? '0' + n : '' + n; }).join(':');
  }

  function saveDraft(spoken) {
    var draft = { at: Date.now(), fields: {}, filed: [] };
    draftFields.forEach(function (id) {
      var field = document.getElementById(id);
      if (field) draft.fields[id] = field.value;
    });
    var mailingPicked = document.querySelector('input[name="cudMailing"]:checked');
    if (mailingPicked) draft.fields.cudMailing = mailingPicked.value;
    draft.fields.cudAppType = appType.value;
    Object.keys(slots).forEach(function (key) {
      if (slots[key].file) draft.filed.push(slots[key].label);
    });
    try {
      window.localStorage.setItem(DRAFT_KEY, JSON.stringify(draft));
    } catch (e) {
      if (spoken) window.showToast('This browser will not keep a draft.');
      return;
    }
    sayDraft(new Date(draft.at), draft.filed.length, false);
    if (spoken) window.showToast('Draft saved against ' + form.dataset.draft + '.');
  }

  function sayDraft(when, filedCount, restored) {
    var what = filedCount === 0 ? '' : ' \u00b7 ' + filedCount
      + (filedCount === 1 ? ' document' : ' documents') + ' to upload again';
    savedLine.textContent = (restored ? 'Draft from ' : 'Draft saved ') + clockOf(when) + what;
    savedLine.hidden = false;
  }

  function restoreDraft() {
    var raw = null;
    try {
      raw = window.localStorage.getItem(DRAFT_KEY);
    } catch (e) {
      return;
    }
    if (!raw) return;
    var draft;
    try {
      draft = JSON.parse(raw);
    } catch (e) {
      return;
    }
    Object.keys(draft.fields || {}).forEach(function (id) {
      if (id === 'cudAppType') {
        appType.value = draft.fields[id];
        return;
      }
      if (id === 'cudMailing') {
        var radio = document.querySelector('input[name="cudMailing"][value="' + draft.fields[id] + '"]');
        if (radio) radio.checked = true;
        return;
      }
      var field = document.getElementById(id);
      if (field) field.value = draft.fields[id];
    });
    // The category list belongs to the sourcing mode, so the select is empty
    // until that mode fills it: what the draft held is kept until it can be put
    // back, and dropped if the mode no longer allows it.
    restoredCategory = String(draft.fields.cudCategory || '');
    sayDraft(new Date(draft.at), (draft.filed || []).length, true);
  }

  document.getElementById('cudDraft').addEventListener('click', function () { saveDraft(true); });

  // A draft the partner has to remember to save is a draft they lose. Everything
  // typed writes itself away a moment after they stop typing; the button stays as
  // the way to be sure, and says so out loud when pressed.
  var draftTimer = null;
  function saveDraftSoon() {
    window.clearTimeout(draftTimer);
    draftTimer = window.setTimeout(function () { saveDraft(false); }, 900);
  }
  form.addEventListener('input', saveDraftSoon);
  form.addEventListener('change', saveDraftSoon);

  // Clear All is not on this step. It could not undo what it emptied - a cleared
  // document leaves the refusals behind it standing - and with the
  // draft saving itself, wiping the form wrote the empty form straight over the
  // draft the partner had. A field typed wrongly is retyped; a document filed
  // wrongly is replaced.

  // Proceed says what is missing where it is missing, and stops at the first one.
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var first = null;

    // A copy whose checks have not answered yet is not carried to the next step:
    // it could still come back as the wrong document.
    var busy = beingChecked();
    if (busy) {
      window.showToast('The ' + busy.label + ' is still with the checks \u2014 one moment.');
      busy.drop.scrollIntoView({ block: 'center', behavior: 'smooth' });
      return;
    }

    function need(ok, field) {
      if (!ok && !first) first = field;
      return ok;
    }

    if (slots.poa.used) {
      need(fieldError(poaType, document.getElementById('cudPoaTypeError'),
        poaType.value ? '' : 'Choose the proof of address'), poaType);
    }
    if (mailingDiffers()) {
      need(fieldError(mailingPoaType, document.getElementById('cudMailingPoaTypeError'),
        mailingPoaType.value ? '' : 'Choose the proof of the mailing address'), mailingPoaType);
    }
    need(fieldError(payMode, document.getElementById('cudPayModeError'),
      payMode.value ? '' : 'Choose the payment mode'), payMode);
    need(fieldError(sourcing, document.getElementById('cudSourcingError'),
      sourcing.value ? '' : 'Choose the sourcing mode'), sourcing);
    if (sourceCode.required) {
      need(fieldError(sourceCode.input, sourceCode.error, sourceCode.input.value.trim()
        ? '' : 'Enter the ' + sourceCodeLabel.textContent.toLowerCase()), sourceCode.input);
    }
    if (subBroker.required) {
      need(fieldError(subBroker.input, subBroker.error,
        subBroker.input.value.trim() ? '' : 'Enter the sub broker code'), subBroker.input);
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
      if (!slot || !slot.used) return;
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
    // As far as the partner is concerned this is the step opening: the wait goes
    // up, and comes down with the one thing this mock can say about it.
    if (window.showLoader) window.showLoader('Opening Investor Information\u2026', 'Everything on this step is saved against ' + form.dataset.draft + '.');
    window.setTimeout(function () {
      if (window.hideLoader) window.hideLoader();
      window.showToast('Investor Information is not built in this mock yet.');
    }, 900);
  });

  if (JSON.parse(document.getElementById('cudPanFiled').textContent)) {
    slots.pan.filed = 'PAN copy on the application';
    slots.pan.filedOriginal = slots.pan.filed;
    // It went through the same three checks on the step before, so the card says
    // so rather than leaving the one filed document on the page unaccounted for.
    setCheck(slots.pan, 'Identified, read and confirmed with ' + ADDRESS.panAuthority
      + ' when the PAN was established on the step before.', 'ok');
  }

  // ----- What the folio already carries ----------------------------------------
  // An application opened on a folio inherits whatever the register holds against
  // it. Those documents are not asked for again: the card says which folio has
  // them, and Proceed stops waiting for them. What the folio is short of is asked
  // for here as usual.
  var HELD = JSON.parse(document.getElementById('cudHeld').textContent);

  ['pan', 'poa', 'photo'].forEach(function (key) {
    if (!HELD[key] || !slots[key]) return;
    use(slots[key], false, 'Already held against folio ' + HELD.folio + ', so it is not filed again.');
    if (key === 'poa') {
      poaType.value = '';
      poaType.options[0].textContent = 'Not Applicable';
      poaType.disabled = true;
    }
  });

  sayWith(slots.pan);
  sayWith(slots.payment);
  restoreDraft();
  syncPoaTypes();
  syncEmpProof();
  syncAppType();
  syncMailing();
  syncPayMode();
  // Opened on a draft, whatever was typed under the mode it was saved with
  // stands; the mode is not being answered again.
  syncSourcing(true);
  Object.keys(slots).forEach(function (key) { show(slots[key]); });
  syncFooter();
})();
