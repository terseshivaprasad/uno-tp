// Bank Details & Payment: the bank search. What is typed - any part of the bank's
// name, the branch, the IFSC or the MICR - is looked up on the backend as it is
// typed, and the branches it finds drop down under the field. Picking one puts its
// IFSC in the field and presses the step's Find, so the page comes back with the
// branch; the server still decides everything.
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

  // Puts the picked branch's IFSC in the input and looks it up.
  function pickBranch(input, option) {
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

  // Draws the found branches as options under the input.
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
      li.setAttribute('data-ifsc', b.ifsc);
      li.className = 'bank-suggest__option';
      var name = document.createElement('span');
      name.className = 'bank-suggest__name';
      name.textContent = b.bank + ' · ' + b.branch;
      var codes = document.createElement('span');
      codes.className = 'bank-suggest__codes';
      codes.textContent = 'IFSC ' + b.ifsc + ' · MICR ' + b.micr;
      li.appendChild(name);
      li.appendChild(codes);
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
    var url = input.getAttribute('data-bank-search') + '?q=' + encodeURIComponent(q);
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
    var input = e.target.closest && e.target.closest('[data-bank-search]');
    if (!input) return;
    clearTimeout(timer);
    timer = setTimeout(function () { searchBanks(input); }, PAUSE);
  });

  document.addEventListener('keydown', function (e) {
    var input = e.target.closest && e.target.closest('[data-bank-search]');
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
      else if (IFSC.test(input.value.trim())) { closeSuggestions(input); findBankByIfsc(input); }
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
    var input = e.target.closest && e.target.closest('[data-bank-search]');
    if (!input) return;
    closeSuggestions(input);
    var to = e.relatedTarget;
    if (to && (to.type === 'submit' || to.tagName === 'A')) return;
    if (IFSC.test(input.value.trim()) && input.value !== input.defaultValue) findBankByIfsc(input);
  });
})();
