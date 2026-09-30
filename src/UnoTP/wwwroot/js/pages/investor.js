// Investor Information.
//
// 1. A PIN code typed for an address (an input with data-pin-url): once all six digits
//    are in, its district and state are fetched and shown in the box marked
//    data-place-for="<the input's id>". Fewer than six digits clear them. The server
//    places the PIN code again on every post, so this is only a preview.
// 2. The guardian section is enabled while the nominee's date of birth makes them a
//    minor (the age limit is on the section as data-minor-under), and disabled - its
//    fields no longer posted - once it does not.
// 3. Proceed with no nominee named asks first. Skip is kept in a hidden field, so the
//    question is asked once per application.
// 4. The FATCA "invests offline" alert closes on its cross.
// 5. The sub occupation goes with the occupation: as an occupation is chosen, only
//    the sub occupations listed for it (data-occupations) are offered, and a choice
//    that no longer applies is cleared.

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

    // Digits only, whatever was typed.
    var pin = input.value.replace(/\D+/g, '');
    if (pin !== input.value) input.value = pin;

    var isWholePin = /^[1-9]\d{5}$/.test(pin);
    if (!isWholePin) {
      input.removeAttribute('data-placed');
      showDistrictAndState(box, '—', '—');
      return;
    }

    // Already looked up for this PIN code.
    if (input.getAttribute('data-placed') === pin) return;
    input.setAttribute('data-placed', pin);
    showDistrictAndState(box, 'Finding…', '…');

    var url = input.getAttribute('data-pin-url').replace('PIN', pin);
    fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(function (response) { return response.ok ? response.json() : null; })
      .then(function (place) {
        // The PIN code changed while the lookup was running: this answer is stale.
        if (input.value.trim() !== pin) return;
        if (place) showDistrictAndState(box, place.district, place.state);
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

(function () {
  // Reads the nominee's date of birth from its three boxes. Returns null while it is
  // not a whole, real date.
  function readNomineeDateOfBirth(box) {
    function part(name) { return box.querySelector('[name="Nominee.' + name + '"]').value; }
    var day = +part('Dd');
    var month = +part('Mm');
    var year = +part('Yyyy');
    if (part('Yyyy').length !== 4 || year < 1900) return null;

    var date = new Date(year, month - 1, day);
    var isRealDate = date.getMonth() === month - 1 && date.getDate() === day;
    return isRealDate ? date : null;
  }

  // Enables the guardian section while the nominee is a minor, and disables it otherwise.
  function enableGuardianForMinorNominee(box, guardian) {
    var born = readNomineeDateOfBirth(box);
    if (!born) { guardian.disabled = true; return; }

    var minorUnder = +guardian.getAttribute('data-minor-under');
    var today = new Date();
    var comesOfAge = new Date(born.getFullYear() + minorUnder, born.getMonth(), born.getDate());
    var isMinor = born <= today && comesOfAge > today;
    guardian.disabled = !isMinor;
  }

  document.addEventListener('input', function (e) {
    var box = e.target.closest && e.target.closest('#nominee .date-input');
    var guardian = document.getElementById('investorGuardian');
    if (!box || !guardian) return;
    enableGuardianForMinorNominee(box, guardian);
  });
})();

(function () {
  // Proceed with no nominee named opens the "No nominee is named" dialog instead of posting.
  document.addEventListener('click', function (e) {
    var proceed = e.target.closest && e.target.closest('[data-ask-nominee]');
    var dialog = proceed && document.getElementById('investorNomineeAsk');
    if (!dialog || !dialog.showPopover) return;

    var skipped = document.getElementById('investorNomineeSkipped');
    if (skipped && skipped.value === 'yes') return;

    e.preventDefault();
    e.stopImmediatePropagation();
    dialog.showPopover();
  }, true);

  // Skip in the dialog: remembers the answer and presses Proceed again.
  document.addEventListener('click', function (e) {
    if (!e.target.closest || !e.target.closest('[data-nominee-skip]')) return;
    document.getElementById('investorNomineeAsk').hidePopover();

    var skipped = document.getElementById('investorNomineeSkipped');
    if (skipped) skipped.value = 'yes';

    var proceed = document.querySelector('[data-ask-nominee]');
    if (proceed) proceed.click();
  });

  // The FATCA alert's cross hides it. Proceed with a FATCA "Yes" brings it back.
  document.addEventListener('click', function (e) {
    var close = e.target.closest && e.target.closest('[data-investor-close]');
    if (close) document.getElementById(close.dataset.investorClose).hidden = true;
  });
})();

(function () {
  // The sub occupations an occupation offers, from the select's data-lists
  // ("occupation=sub|sub;occupation=sub"): its own list, every sub occupation for an
  // occupation with no list, and none for an occupation whose list is empty.
  function subOccupationsFor(sub, occupation) {
    var every = (sub.getAttribute('data-every') || '').split('|').filter(Boolean);
    if (occupation === '') return every;
    var lists = (sub.getAttribute('data-lists') || '').split(';');
    for (var i = 0; i < lists.length; i++) {
      var parts = lists[i].split('=');
      if (parts[0] === occupation) return parts[1] ? parts[1].split('|') : [];
    }
    return every;
  }

  // Rebuilds the sub occupation dropdown for the occupation chosen, keeping the
  // choice where it still applies.
  function offerSubOccupationsFor(occupationSelect) {
    var sub = document.querySelector('select[data-sub-occupation-of="' + occupationSelect.id + '"]');
    if (!sub) return;
    var occupation = occupationSelect.value;
    var offered = subOccupationsFor(sub, occupation);
    var chosen = sub.value;
    var asksNothing = occupation !== '' && offered.length === 0;
    while (sub.options.length > 0) sub.remove(0);
    sub.add(new Option(asksNothing ? 'Not applicable' : 'Select', ''));
    for (var i = 0; i < offered.length; i++) {
      var option = new Option(offered[i], offered[i]);
      if (offered[i] === chosen) option.selected = true;
      sub.add(option);
    }
    if (asksNothing) sub.removeAttribute('data-required');
    else sub.setAttribute('data-required', 'Select the sub occupation');
  }

  document.addEventListener('change', function (e) {
    var select = e.target;
    if (!select.matches || !select.matches('select') || !/\.Occupation$/.test(select.name || '')) return;
    offerSubOccupationsFor(select);
  });
})();
