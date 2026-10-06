// Upload Documents: a code searched against its register - the brokers, the staff
// - as it is typed. Any part of the code or the name is looked up on the backend,
// and what it finds drops down under the field. Picking one puts its code in the
// field, shows its name at once, and lets the field's own change post through, so
// the server confirms the name and redraws the page.
//
// The registers are big (the staff one has tens of thousands of rows) and the
// connection may be slow, so the page never holds a register. Nothing is asked
// until three characters are typed; the staff are searched only within the
// sourcing mode chosen; a search still on its way is called off when the text
// changes; and what was found for a text is shown again without asking.
//
// The page's <main> is redrawn after every post, so everything here listens on the
// document rather than on the fields themselves. The list is the bank search's.
(function () {
  if (!window.fetch) return;

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

  // The name shown under the field, at once; the server says it again on the post.
  function showPickedName(input, text) {
    var col = input.closest('.doc-column') || input.parentNode;
    var line = col.querySelector('.doc-resolved');
    if (!line) {
      line = document.createElement('p');
      line.className = 'doc-resolved';
      input.insertAdjacentElement('afterend', line);
    }
    line.classList.remove('doc-resolved__none');
    line.textContent = text;
  }

  // Puts the picked code in the input, shows its name and closes the list.
  function pickSuggestion(input, option) {
    input.value = option.getAttribute('data-code');
    showPickedName(input, (input.getAttribute('data-name-label') || 'Name') + ' — ' + option.getAttribute('data-name'));
    closeSuggestions(input);
    input.dispatchEvent(new Event('change', { bubbles: true }));
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

  // Draws the found brokers or staff as options under the input.
  function showSuggestions(input, parties) {
    var list = suggestionListFor(input);
    if (!list) return;
    if (parties.length === 0) {
      showNote(input, message('UploadDocuments.NothingOnRegister', { typed: input.value.trim() }));
      return;
    }
    list.innerHTML = '';
    parties.forEach(function (p, i) {
      var li = document.createElement('li');
      li.id = list.id + '-' + i;
      li.setAttribute('role', 'option');
      li.setAttribute('aria-selected', 'false');
      li.setAttribute('data-code', p.code);
      li.setAttribute('data-name', p.name);
      li.className = 'bank-suggest__option';
      var who = document.createElement('span');
      who.className = 'bank-suggest__name';
      who.textContent = p.name;
      var code = document.createElement('span');
      code.className = 'bank-suggest__codes';
      code.textContent = p.code;
      li.appendChild(who);
      li.appendChild(code);
      list.appendChild(li);
    });
    list.hidden = false;
    input.setAttribute('aria-expanded', 'true');
  }

  // Looks the typed text up in the register (brokers or staff) on the backend.
  function searchRegister(input) {
    var q = input.value.trim();
    // Whatever was being looked up is for text that has since changed.
    if (underWay) { underWay.abort(); underWay = null; }
    var mine = ++asked;
    if (q.length === 0) { closeSuggestions(input); return; }
    if (q.length < searchFrom(input)) {
      showNote(input, 'Type ' + searchFrom(input) + ' or more characters to search');
      return;
    }
    var url = input.getAttribute('data-register-search')
      + '?register=' + encodeURIComponent(input.getAttribute('data-register'))
      + '&mode=' + encodeURIComponent(input.getAttribute('data-mode') || '')
      + '&q=' + encodeURIComponent(q);
    if (found[url]) { showSuggestions(input, found[url]); return; }

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
      .then(function (parties) {
        // Nothing found is not kept: a party added to the register is found by the next search.
        if (parties.length > 0) found[url] = parties;
        // Only the answer to the latest question, and only while the field still has the caret.
        if (mine === asked && document.activeElement === input) showSuggestions(input, parties);
      })
      .catch(function (error) {
        // Called off for newer text: that search draws the list.
        if (error && error.name === 'AbortError') return;
        if (mine === asked && document.activeElement === input) showNote(input, message('Shared.SearchFailed'));
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
    var input = e.target.closest && e.target.closest('[data-register-search]');
    if (!input) return;
    clearTimeout(timer);
    timer = setTimeout(function () { searchRegister(input); }, PAUSE);
  });

  document.addEventListener('keydown', function (e) {
    var input = e.target.closest && e.target.closest('[data-register-search]');
    if (!input) return;
    var list = suggestionListFor(input);
    if (e.key === 'ArrowDown') { e.preventDefault(); moveSuggestionHighlight(input, 1); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); moveSuggestionHighlight(input, -1); }
    else if (e.key === 'Escape') { closeSuggestions(input); }
    else if (e.key === 'Enter') {
      // Enter picks the party highlighted, or the only one found; it never posts the form half-typed.
      e.preventDefault();
      var all = list && !list.hidden ? suggestionOptions(list) : [];
      var chosen = all.find(function (o) { return o.getAttribute('aria-selected') === 'true'; });
      if (!chosen && all.length === 1) chosen = all[0];
      if (chosen) pickSuggestion(input, chosen);
      else input.blur();
    }
  });

  // A press on a suggestion keeps the caret in the field, so the field does not
  // change - and post - before the pick lands.
  document.addEventListener('mousedown', function (e) {
    var option = e.target.closest && e.target.closest('.bank-suggest__option');
    if (option && option.parentNode && document.querySelector('[data-register-search][aria-controls="' + option.parentNode.id + '"]')) e.preventDefault();
  });
  document.addEventListener('click', function (e) {
    var option = e.target.closest && e.target.closest('.bank-suggest__option');
    if (!option) return;
    var input = document.querySelector('[data-register-search][aria-controls="' + option.parentNode.id + '"]');
    if (input) pickSuggestion(input, option);
  });

  document.addEventListener('focusout', function (e) {
    var input = e.target.closest && e.target.closest('[data-register-search]');
    if (input) closeSuggestions(input);
  });
})();
