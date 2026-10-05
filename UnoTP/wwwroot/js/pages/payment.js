// Bank Details & Payment: the bank search. What is typed - any part of the bank's
// name, the branch, the IFSC or the MICR - is looked up on the backend as it is
// typed, and the branches it finds drop down under the field. Picking one puts its
// IFSC in the field and presses the step's Find, so the page comes back with the
// branch; the server still decides everything.
//
// The Axis CMS branch is searched the same way (data-cms-search), by its label -
// name, location and PIN code: the branches found drop down under the field, each
// shown by that label, and picking one puts its name in the field and its code in
// the hidden field beside it (data-cms-code). Typing over the name empties the
// code, so only a branch picked from the search is taken. Neither master is ever
// sent to the page whole.
//
// The page's <main> is redrawn after every post, so everything here listens on the
// document rather than on the fields themselves.
(function () {
  if (!window.fetch) return;

  var IFSC = /^[A-Za-z]{4}0[A-Za-z0-9]{6}$/;
  var timer = null;
  var asked = 0;
  // How long the typing must pause before a search is sent, milliseconds.
  var PAUSE = 300;
  // The search now on its way, so a newer one can call it off.
  var underWay = null;
  // What each search found, by its address, so typing back over the same text asks nothing.
  var found = {};

  // The fields that search as they are typed: a bank, and the Axis CMS branch.
  var SEARCHES = '[data-bank-search], [data-cms-search]';
  // Whether a field searches the Axis CMS locations rather than the banks.
  function isCmsSearch(input) { return input.hasAttribute('data-cms-search'); }
  // Where a field's search is asked.
  function searchAddress(input) { return input.getAttribute(isCmsSearch(input) ? 'data-cms-search' : 'data-bank-search'); }

  // The hidden field that carries the code of the Axis CMS branch picked.
  function cmsCodeFieldFor(input) { return document.getElementById(input.getAttribute('data-cms-code')); }

  // The fewest characters a search is run on (data-search-from; the server keeps the same rule).
  function searchFrom(input) { return parseInt(input.getAttribute('data-search-from'), 10) || 3; }

  // The suggestion list an input controls (its aria-controls).
  function suggestionListFor(input) { return document.getElementById(input.getAttribute('aria-controls')); }
  // The options now in a suggestion list.
  function suggestionOptions(list) { return Array.prototype.slice.call(list.querySelectorAll('[role="option"]')); }

  // Empties and hides an input's suggestion list.
  function closeSuggestions(input) {
    var list = suggestionListFor(input);
    if (!list) return;
    list.hidden = true;
    list.innerHTML = '';
    input.setAttribute('aria-expanded', 'false');
    input.removeAttribute('aria-activedescendant');
  }

  // The step's Find for this field: saves what was typed and looks the IFSC up.
  function findBankByIfsc(input) {
    var button = document.getElementById(input.getAttribute('data-find'));
    if (button && button.form) button.form.requestSubmit(button);
  }

  // Puts the picked branch's IFSC in the input and looks it up; an Axis CMS
  // location picked is put in the input by its name, and nothing is posted.
  function pickBranch(input, option) {
    if (isCmsSearch(input)) {
      input.value = option.getAttribute('data-name');
      cmsCodeFieldFor(input).value = option.getAttribute('data-code');
      closeSuggestions(input);
      input.dispatchEvent(new Event('change', { bubbles: true }));
      return;
    }
    input.value = option.getAttribute('data-ifsc');
    closeSuggestions(input);
    findBankByIfsc(input);
  }

  // One line under the input that is not a choice: what to type, that a search is
  // under way, or that it could not be made.
  function showNote(input, words) {
    var list = suggestionListFor(input);
    if (!list) return;
    list.innerHTML = '';
    var note = document.createElement('li');
    note.className = 'bank-suggest__none';
    note.textContent = words;
    list.appendChild(note);
    list.hidden = false;
    input.setAttribute('aria-expanded', 'true');
    input.removeAttribute('aria-activedescendant');
  }

  // Draws what was found as options under the input: bank branches, each by its
  // label of MICR, IFSC, branch and bank, or Axis CMS branches, each by its own label.
  function showBranchSuggestions(input, branches) {
    var list = suggestionListFor(input);
    if (!list) return;
    if (branches.length === 0) {
      showNote(input, 'No branch matches “' + input.value.trim() + '”');
      return;
    }
    list.innerHTML = '';
    branches.forEach(function (b, i) {
      var li = document.createElement('li');
      li.id = list.id + '-' + i;
      li.setAttribute('role', 'option');
      li.setAttribute('aria-selected', 'false');
      li.className = 'bank-suggest__option';
      var name = document.createElement('span');
      name.className = 'bank-suggest__name';
      li.appendChild(name);
      if (isCmsSearch(input)) {
        // An Axis CMS branch, shown by its label: its name, location and PIN code.
        li.setAttribute('data-name', b.name);
        li.setAttribute('data-code', b.code);
        name.textContent = b.label || b.name;
      } else {
        // A bank branch, labelled as the FD system labels it: (MICR >> IFSC >> branch >> bank).
        li.setAttribute('data-ifsc', b.ifsc);
        name.textContent = '(' + b.micr + ' >> ' + b.ifsc + ' >> ' + b.branch + ' >> ' + b.bank + ')';
      }
      list.appendChild(li);
    });
    list.hidden = false;
    input.setAttribute('aria-expanded', 'true');
  }

  // Looks the typed text (bank, branch, IFSC or MICR) up on the backend. The bank
  // master is big and the connection may be slow, so nothing is asked until three
  // characters are typed, a search still on its way is called off when the text
  // changes, and what was found for a text is shown again without asking.
  function searchBanks(input) {
    var q = input.value.trim();
    // Whatever was being looked up is for text that has since changed.
    if (underWay) { underWay.abort(); underWay = null; }
    var mine = ++asked;
    if (q.length === 0) { closeSuggestions(input); return; }
    if (q.length < searchFrom(input)) {
      showNote(input, 'Type ' + searchFrom(input) + ' or more characters to search');
      return;
    }
    var url = searchAddress(input) + '?q=' + encodeURIComponent(q);
    if (found[url]) { showBranchSuggestions(input, found[url]); return; }

    // The list keeps what it shows while the answer is awaited; an empty one says a search is on.
    var list = suggestionListFor(input);
    if (list && list.querySelectorAll('[role="option"]').length === 0) showNote(input, 'Searching\u2026');
    // A search that is slow to answer says so (loader.js).
    var waitOver = window.whenSlow ? window.whenSlow('Still searching \u2014 the connection is slow\u2026') : function () {};
    underWay = window.AbortController ? new AbortController() : null;
    fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' }, signal: underWay ? underWay.signal : undefined })
      .then(function (r) {
        if (!r.ok) throw new Error('The search was not answered');
        return r.json();
      })
      .then(function (branches) {
        // Nothing found is not kept: a branch added to the master is found by the next search.
        if (branches.length > 0) found[url] = branches;
        // Only the answer to the latest question, and only while the field still has the caret.
        if (mine === asked && document.activeElement === input) showBranchSuggestions(input, branches);
      })
      .catch(function (error) {
        // Called off for newer text: that search draws the list.
        if (error && error.name === 'AbortError') return;
        if (mine === asked && document.activeElement === input) showNote(input, 'Could not search just now. Check the connection and type again.');
      })
      .then(waitOver);
  }

  // Moves the highlighted option up or down (arrow keys).
  function moveSuggestionHighlight(input, by) {
    var list = suggestionListFor(input);
    var all = suggestionOptions(list);
    if (list.hidden || all.length === 0) return;
    var at = all.findIndex(function (o) { return o.getAttribute('aria-selected') === 'true'; });
    var next;
    if (at < 0) {
      // Nothing highlighted yet: down starts at the top, up at the bottom.
      next = by > 0 ? 0 : all.length - 1;
    } else {
      next = (at + by + all.length) % all.length;
    }
    all.forEach(function (o, i) { o.setAttribute('aria-selected', i === next ? 'true' : 'false'); });
    input.setAttribute('aria-activedescendant', all[next].id);
    all[next].scrollIntoView({ block: 'nearest' });
  }

  document.addEventListener('input', function (e) {
    var input = e.target.closest && e.target.closest(SEARCHES);
    if (!input) return;
    // A name typed over is no longer the branch that was picked.
    if (isCmsSearch(input)) cmsCodeFieldFor(input).value = '';
    clearTimeout(timer);
    timer = setTimeout(function () { searchBanks(input); }, PAUSE);
  });

  document.addEventListener('keydown', function (e) {
    var input = e.target.closest && e.target.closest(SEARCHES);
    if (!input) return;
    var list = suggestionListFor(input);
    if (e.key === 'ArrowDown') { e.preventDefault(); moveSuggestionHighlight(input, 1); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); moveSuggestionHighlight(input, -1); }
    else if (e.key === 'Escape') { closeSuggestions(input); }
    else if (e.key === 'Enter') {
      // Enter picks the branch highlighted - or the only one found - and otherwise
      // looks up an IFSC typed in full; it never posts the form half-typed.
      e.preventDefault();
      var all = list && !list.hidden ? suggestionOptions(list) : [];
      var chosen = all.find(function (o) { return o.getAttribute('aria-selected') === 'true'; });
      if (!chosen && all.length === 1) chosen = all[0];
      if (chosen) pickBranch(input, chosen);
      else if (!isCmsSearch(input) && IFSC.test(input.value.trim())) { closeSuggestions(input); findBankByIfsc(input); }
    }
  });

  // A press on a suggestion keeps the caret in the field, so the field does not
  // change - and post - before the pick lands.
  document.addEventListener('mousedown', function (e) {
    var option = e.target.closest && e.target.closest('.bank-suggest__option');
    if (option) e.preventDefault();
  });
  document.addEventListener('click', function (e) {
    var option = e.target.closest && e.target.closest('.bank-suggest__option');
    if (!option) return;
    var input = document.querySelector('[aria-controls="' + option.parentNode.id + '"]');
    if (input) pickBranch(input, option);
  });

  // Leaving the field shuts the list; an IFSC typed in full is looked up - unless
  // the caret went to a button, whose own post looks it up anyway.
  document.addEventListener('focusout', function (e) {
    var input = e.target.closest && e.target.closest(SEARCHES);
    if (!input) return;
    closeSuggestions(input);
    if (isCmsSearch(input)) return;
    var to = e.relatedTarget;
    if (to && (to.type === 'submit' || to.tagName === 'A')) return;
    if (IFSC.test(input.value.trim()) && input.value !== input.defaultValue) findBankByIfsc(input);
  });
})();
