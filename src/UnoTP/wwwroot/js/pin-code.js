// A PIN code typed for an address (data-pin-url): once all six digits are in, the
// district and state it is in are asked for and shown beside it, in the box marked
// data-place-for with the field's id. Nothing is suggested while it is typed, and
// fewer than six digits clear them. The server places the PIN code again on every
// post, so what is shown here is only ever a preview of what is saved.
(function () {
  function show(box, district, state) {
    box.querySelector('[data-place="district"]').textContent = district;
    box.querySelector('[data-place="state"]').textContent = state;
  }

  function place(input) {
    var box = document.querySelector('[data-place-for="' + input.id + '"]');
    if (!box) return;
    var pin = input.value.trim();
    if (!/^[1-9]\d{5}$/.test(pin)) { input.removeAttribute('data-placed'); show(box, '—', '—'); return; }
    if (input.getAttribute('data-placed') === pin) return;
    input.setAttribute('data-placed', pin);
    show(box, 'Finding…', '…');
    fetch(input.getAttribute('data-pin-url').replace('PIN', pin), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(function (res) { return res.ok ? res.json() : null; })
      .then(function (found) {
        if (input.value.trim() !== pin) return;
        if (found) show(box, found.district, found.state);
        else show(box, 'Not found for this PIN code', '—');
      })
      .catch(function () {
        if (input.value.trim() !== pin) return;
        input.removeAttribute('data-placed');
        show(box, 'Not fetched — placed on Proceed', '—');
      });
  }

  document.addEventListener('input', function (e) {
    if (e.target.matches && e.target.matches('input[data-pin-url]')) place(e.target);
  });
})();
