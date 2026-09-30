// A PIN code typed for an address (data-pin-url): once all six digits are in, the
// district and state it is in are asked for and shown beside it, in the box marked
// data-place-for with the field's id. Nothing is suggested while it is typed, and
// fewer than six digits clear them. The server places the PIN code again on every
// post, so what is shown here is only ever a preview of what is saved.
(function () {
  // Writes the district and state beside a PIN code.
  function showDistrictAndState(box, district, state) {
    box.querySelector('[data-place="district"]').textContent = district;
    box.querySelector('[data-place="state"]').textContent = state;
  }

  // Once a full PIN code is typed, looks up its district and state.
  function lookUpPinCode(input) {
    var box = document.querySelector('[data-place-for="' + input.id + '"]');
    if (!box) return;
    // Digits alone, whatever was typed.
    var pin = input.value.replace(/\D+/g, '');
    if (pin !== input.value) input.value = pin;
    if (!/^[1-9]\d{5}$/.test(pin)) { input.removeAttribute('data-placed'); showDistrictAndState(box, '—', '—'); return; }
    if (input.getAttribute('data-placed') === pin) return;
    input.setAttribute('data-placed', pin);
    showDistrictAndState(box, 'Finding…', '…');
    fetch(input.getAttribute('data-pin-url').replace('PIN', pin), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(function (res) { return res.ok ? res.json() : null; })
      .then(function (found) {
        if (input.value.trim() !== pin) return;
        if (found) showDistrictAndState(box, found.district, found.state);
        else showDistrictAndState(box, 'Not found for this PIN code', '—');
      })
      .catch(function () {
        if (input.value.trim() !== pin) return;
        input.removeAttribute('data-placed');
        showDistrictAndState(box, 'Not fetched — placed on Proceed', '—');
      });
  }

  document.addEventListener('input', function (e) {
    if (e.target.matches && e.target.matches('input[data-pin-url]')) lookUpPinCode(e.target);
  });
})();

// Investor Information: the guardian section enabled for a minor nominee as the
// date of birth is typed (the age is the server's, on the section as
// data-minor-under), and Proceed asking first when no nominee is named.
// A guardian is named only for a minor nominee: the section is enabled once
// the date of birth typed makes them one, and disabled - its fields no longer
// posted - once it does not. The server draws it the same way from what was
// posted last.
document.addEventListener('input', function (e) {
    var box = e.target.closest && e.target.closest('#nominee .date-input');
    var guardian = document.getElementById('investorGuardian');
    if (!box || !guardian) return;
    // One part (Dd, Mm or Yyyy) of the nominee's date of birth.
    var nomineeDobPart = function (n) { return box.querySelector('[name="Nominee.' + n + '"]').value; };
    var d = +nomineeDobPart('Dd'), m = +nomineeDobPart('Mm'), y = +nomineeDobPart('Yyyy');
    var born = new Date(y, m - 1, d);
    var whole = nomineeDobPart('Yyyy').length === 4 && y >= 1900 && born.getMonth() === m - 1 && born.getDate() === d;
    var today = new Date();
    var adult = new Date(y + (+guardian.getAttribute('data-minor-under')), m - 1, d);
    var minor = whole && born <= today && adult > today;
    guardian.disabled = !minor;
});

// Proceed with no nominee named asks first. Skip is kept with the form, so
// once answered the question is not asked again; Add posts to NomineeAdd.
document.addEventListener('click', function (e) {
    var proceed = e.target.closest && e.target.closest('[data-ask-nominee]');
    var ask = proceed && document.getElementById('investorNomineeAsk');
    if (!ask || !ask.showPopover) return;
    var skipped = document.getElementById('investorNomineeSkipped');
    if (skipped && skipped.value === 'yes') return;
    e.preventDefault();
    e.stopImmediatePropagation();
    ask.showPopover();
}, true);
document.addEventListener('click', function (e) {
    if (!e.target.closest || !e.target.closest('[data-nominee-skip]')) return;
    document.getElementById('investorNomineeAsk').hidePopover();
    var skipped = document.getElementById('investorNomineeSkipped');
    if (skipped) skipped.value = 'yes';
    var proceed = document.querySelector('[data-ask-nominee]');
    if (proceed) proceed.click();
});

// The offline alert closes on its cross; Proceed with a FATCA Yes brings it back.
document.addEventListener('click', function (e) {
    var close = e.target.closest && e.target.closest('[data-investor-close]');
    if (close) document.getElementById(close.dataset.investorClose).hidden = true;
});
