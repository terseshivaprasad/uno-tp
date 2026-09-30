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

  // Draws the found branches as options under the input.
  function showBranchSuggestions(input, branches) {
    var list = suggestionListFor(input);
    if (!list) return;
    list.innerHTML = '';
    if (branches.length === 0) {
      var none = document.createElement('li');
      none.className = 'bank-suggest__none';
      none.textContent = 'No branch matches ' + '“' + input.value.trim() + '”';
      list.appendChild(none);
    }
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

  // Looks the typed text (bank, branch, IFSC or MICR) up on the backend.
  function searchBanks(input) {
    var q = input.value.trim();
    if (q.length < 2) { closeSuggestions(input); return; }
    var mine = ++asked;
    fetch(input.getAttribute('data-bank-search') + '?q=' + encodeURIComponent(q), {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' }
    })
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(function (branches) {
        // Only the answer to the latest question, and only while the field still has the caret.
        if (mine === asked && document.activeElement === input) showBranchSuggestions(input, branches);
      })
      .catch(function () { closeSuggestions(input); });
  }

  // Moves the highlighted option up or down (arrow keys).
  function moveSuggestionHighlight(input, by) {
    var list = suggestionListFor(input);
    var all = suggestionOptions(list);
    if (list.hidden || all.length === 0) return;
    var at = all.findIndex(function (o) { return o.getAttribute('aria-selected') === 'true'; });
    var next = at < 0 ? (by > 0 ? 0 : all.length - 1) : (at + by + all.length) % all.length;
    all.forEach(function (o, i) { o.setAttribute('aria-selected', i === next ? 'true' : 'false'); });
    input.setAttribute('aria-activedescendant', all[next].id);
    all[next].scrollIntoView({ block: 'nearest' });
  }

  document.addEventListener('input', function (e) {
    var input = e.target.closest && e.target.closest('[data-bank-search]');
    if (!input) return;
    clearTimeout(timer);
    timer = setTimeout(function () { searchBanks(input); }, 180);
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
      var chosen = all.find(function (o) { return o.getAttribute('aria-selected') === 'true'; }) || (all.length === 1 ? all[0] : null);
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
