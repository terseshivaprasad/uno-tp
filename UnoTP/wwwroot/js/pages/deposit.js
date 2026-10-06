// FD Configuration: the backend's quote for the deposit is drawn again as a choice
// changes, without the page being sent. Every control marked data-quote posts the
// form to FdQuote, which answers with the quote panel alone and saves nothing - the
// step is saved by Proceed, as on a bank's form. What a choice shows or hides has
// already changed (partial-forms.js). The
// amount is quoted as it is typed, once the partner pauses; what is wrong with it
// is said once they leave the field, not while they are still typing it.
(function () {
  var form = document.getElementById('depositConfigForm') || document.querySelector('form[data-quote-url]');
  if (!form || !window.fetch) return;
  var url = form.getAttribute('data-quote-url');
  var timer = null;
  var asked = 0;

  var inFlight = null;

  // Posts the form for a new quote and redraws the quote panel with the answer.
  function refreshQuote(sayProblem) {
    var mine = ++asked;
    // A newer choice makes the one before it moot: its request is dropped, not waited for.
    if (inFlight) inFlight.abort();
    inFlight = window.AbortController ? new AbortController() : null;
    document.documentElement.classList.add('is-saving');
    // A quote that is slow to come says so (loader.js).
    var waitOver = window.whenSlow ? window.whenSlow('Still fetching the quote \u2014 the connection is slow\u2026') : null;
    fetch(url, { method: 'POST', body: new FormData(form), credentials: 'same-origin', signal: inFlight ? inFlight.signal : undefined })
      .then(function (res) {
        if (!res.ok) throw new Error(res.status);
        return res.text();
      })
      .then(function (html) {
        // A later choice is already on its way: its answer is the one to show.
        if (mine !== asked) return;
        var panel = document.getElementById('deposit-quote');
        if (panel) panel.innerHTML = html;
        showAmountMessage(sayProblem);
        showWhatTheCardOffers();
        showWhetherSourceOfFundsIsAsked();
        // The choice made is no longer offered at this amount: it moves to the first
        // that is, and the quote is asked again for it - once, since that one is open.
        if (moveOffAShutChoice()) {
          showRenewSameTenure();
          refreshQuote(sayProblem);
        }
      })
      .catch(function () {
        // No quote this time - the connection dropped: the panel says so, and the
        // next choice asks again. Proceed still saves and checks everything.
        if (mine !== asked) return;
        var panel = document.getElementById('deposit-quote');
        var note = panel && panel.querySelector('.deposit-summary__note, .page-note');
        if (note) note.textContent = message('FdConfiguration.QuoteNotFetched');
      })
      .then(function () {
        if (waitOver) waitOver();
        if (mine === asked) document.documentElement.classList.remove('is-saving');
      });
  }

  // What the answer says of the amount: the line under it, and its error once due.
  // Given a problem of its own (the amount over the maximum, seen as it is typed),
  // that is shown at once instead.
  function showAmountMessage(sayProblem, problemNow) {
    var said = document.querySelector('#deposit-quote [data-deposit-amount]');
    var input = document.getElementById('deposit-amount');
    if (!input) return;
    if (!problemNow && !said) return;
    var hint = document.getElementById('deposit-amount-hint');
    if (hint && said) hint.textContent = said.getAttribute('data-hint') || '';
    var problem = problemNow || said.getAttribute('data-problem');
    var error = document.getElementById('deposit-amountError');
    // The amount sits in a box with the rupee sign: that box takes the red edge,
    // and the error goes under it.
    var box = input.closest('.deposit-amount') || input;
    if (!problem) {
      box.classList.remove('deposit-amount--error');
      input.classList.remove('is-invalid');
      input.removeAttribute('aria-invalid');
      if (error) error.remove();
      input.setAttribute('aria-describedby', 'deposit-amount-hint');
      return;
    }
    // While the amount is still being typed, only an amount over the maximum is
    // said at once - more digits can only make it worse; anything else waits until
    // the partner leaves the field.
    var digits = Number(input.value.replace(/\D/g, '') || 0);
    var max = Number(input.getAttribute('data-max') || 0);
    var overMax = max > 0 && digits > max;
    if (!sayProblem && !overMax) return;
    if (!error) {
      error = document.createElement('p');
      error.className = 'field-error';
      error.id = 'deposit-amountError';
      error.setAttribute('role', 'alert');
      box.insertAdjacentElement('afterend', error);
    }
    error.textContent = problem;
    box.classList.add('deposit-amount--error');
    input.classList.add('is-invalid');
    input.setAttribute('aria-invalid', 'true');
    input.setAttribute('aria-describedby', 'deposit-amountError');
  }

  // The answer says which tenures and payouts the rate card offers at the amount
  // (on the chart a monthly payout needs ₹50,000): each choice is opened or shut to match.
  function showWhatTheCardOffers() {
    var offers = document.querySelectorAll('#deposit-quote [data-offer]');
    for (var i = 0; i < offers.length; i++) {
      var name = offers[i].getAttribute('data-offer');
      var value = offers[i].getAttribute('data-value');
      var offered = offers[i].getAttribute('data-offered') === 'yes';
      var input = form.querySelector('input[name="' + name + '"][value="' + value + '"]');
      if (!input) continue;
      input.disabled = !offered;
      var option = input.closest('.deposit-option');
      if (option) option.classList.toggle('deposit-option--off', !offered);
    }
  }

  // A tenure or payout chosen that the card has shut at this amount moves to the
  // first one still open, so the card and the choice never disagree. True when one moved.
  function moveOffAShutChoice() {
    var moved = false;
    ['TenureMonths', 'InterestPayout'].forEach(function (name) {
      var chosen = form.querySelector('input[name="' + name + '"]:checked');
      if (!chosen || !chosen.disabled) return;
      var open = form.querySelector('input[name="' + name + '"]:not(:disabled)');
      if (!open) return;
      open.checked = true;
      moved = true;
    });
    return moved;
  }

  // The answer says whether the source of funds is asked at this amount (the
  // investor's deposits with us pass ₹1 crore, and who they are): the section is
  // shown or hidden to match, with the reason under the field.
  function showWhetherSourceOfFundsIsAsked() {
    var said = document.querySelector('#deposit-quote [data-source-of-funds]');
    var select = document.getElementById('deposit-source-of-funds');
    var section = document.getElementById('deposit-source-of-funds-section');
    if (!said || !select) return;
    var asked = said.getAttribute('data-asked') === 'yes';
    select.disabled = !asked;
    if (section) section.hidden = !asked;
    var why = document.getElementById('deposit-source-of-funds-why');
    if (why) why.textContent = said.getAttribute('data-why') || '';
    openRemarkForOther();
  }

  // The remark box is open while the source of funds is asked and "Other" is chosen.
  function openRemarkForOther() {
    var select = document.getElementById('deposit-source-of-funds');
    var remark = document.getElementById('deposit-source-of-funds-remark');
    if (!select || !remark) return;
    var other = select.getAttribute('data-other');
    remark.disabled = select.disabled || !other || select.value !== other;
  }

  form.addEventListener('change', function (e) {
    if (e.target.id === 'deposit-source-of-funds') openRemarkForOther();
  });

  // Renew for the same tenure: said as the tenure is chosen.
  function showRenewSameTenure() {
    var tenure = form.querySelector('input[name="TenureMonths"]:checked');
    var field = document.getElementById('deposit-renew-for');
    if (tenure && field) field.value = 'Same tenure — ' + tenure.value + ' months';
  }

  // A renewal's amount follows what of the old deposit is renewed: each choice
  // carries its own figure, so the amount changes the moment it is chosen.
  function showRenewalAmount() {
    var select = document.getElementById('deposit-renewal-for');
    var input = document.getElementById('deposit-amount');
    if (!select || !input) return;
    var chosen = select.options[select.selectedIndex];
    if (!chosen) return;
    input.value = chosen.getAttribute('data-amount') || input.value;
    var aside = document.getElementById('deposit-amount-aside');
    if (aside) aside.textContent = chosen.getAttribute('data-aside') || '';
  }

  form.addEventListener('change', function (e) {
    if (!e.target.matches('[data-quote]')) return;
    clearTimeout(timer);
    if (e.target.id === 'deposit-renewal-for') showRenewalAmount();
    showRenewSameTenure();
    refreshQuote(true);
  });

  form.addEventListener('input', function (e) {
    if (e.target.id !== 'deposit-amount') return;
    // Over the maximum is said on the keystroke itself, from the limit on the field.
    var digits = Number(e.target.value.replace(/\D/g, '') || 0);
    var max = Number(e.target.getAttribute('data-max') || 0);
    if (max > 0 && digits > max) showAmountMessage(true, e.target.getAttribute('data-over-max') || message('FdConfiguration.AboveMaximumShort'));
    clearTimeout(timer);
    timer = setTimeout(function () { refreshQuote(false); }, 600);
  });


  // The page opens on what was saved: a saved choice the card shuts at that amount moves too.
  if (moveOffAShutChoice()) {
    showRenewSameTenure();
    refreshQuote(false);
  }
})();
