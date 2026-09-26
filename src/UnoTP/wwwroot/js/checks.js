// What a bank's form checks before it sends anything: that a field is filled in and
// in the right shape. A field says what it needs -
//   data-required="Enter the PAN"   it may not be left empty (the words to say)
//   data-check="pan"                pan, mobile, email, pin, account, digits:6,
//                                   match:<id of the field it repeats>, dmy (a
//                                   typed DD/MM/YYYY date), date (a three-box date,
//                                   on its group), amount (data-min, data-max,
//                                   data-step, in rupees)
//   data-message="..."              the words for a shape that is wrong, in place
//                                   of the check's own
// It is checked when the partner leaves it, and cleared as soon as it is put right.
// A button marked data-validate="form" checks every field of its form first - or,
// with a selector for its value, data-validate=".csi-search", the part of the
// form it stands in - and sends nothing while one is wrong: on a slow connection a round trip only to be told a
// PAN is short is the wait not worth having. The server checks everything again -
// this only saves the trip. A field that is hidden or disabled is not checked.
//
// The message goes where the server puts its own: the element <id>Error (or the
// one data-error-id names), made if the page has none.
(function () {
  var shapes = {
    pan: [/^[A-Z]{5}[0-9]{4}[A-Z]$/, 'Enter a valid PAN, like ABCDE1234F'],
    mobile: [/^[6-9]\d{9}$/, 'Enter a 10-digit mobile number'],
    email: [/^[^@\s]+@[^@\s]+\.[^@\s]+$/, 'Enter a valid e-mail'],
    pin: [/^[1-9]\d{5}$/, 'Enter a 6-digit PIN code'],
    account: [/^\d{6,18}$/, 'Enter the account number, 6 to 18 digits']
  };

  function valueOf(el) {
    if (el.matches('[role=group], .csi-date')) {
      return Array.prototype.map.call(el.querySelectorAll('input'), function (i) { return i.value.trim(); }).join('/');
    }
    return (el.value || '').trim();
  }

  function realDate(d, m, y) {
    d = +d; m = +m; y = +y;
    if (!(y >= 1900) || !(m >= 1 && m <= 12) || !(d >= 1)) return null;
    var date = new Date(y, m - 1, d);
    return date.getMonth() === m - 1 && date.getDate() === d ? date : null;
  }

  // What is wrong with a field, or null.
  function problem(el) {
    var value = valueOf(el);
    var empty = el.matches('[role=group], .csi-date') ? value.replace(/\//g, '') === '' : value === '';
    if (empty) return el.getAttribute('data-required') || null;
    var said = checkProblem(el, value);
    return said && (el.getAttribute('data-message') || said);
  }

  // What is wrong with the shape of a field that is filled in, or null.
  function checkProblem(el, value) {
    var check = el.getAttribute('data-check') || '';
    var name = check.split(':')[0];
    var arg = check.slice(name.length + 1);
    if (shapes[name]) {
      var v = name === 'pan' ? value.toUpperCase() : name === 'account' ? value.replace(/\D/g, '') : value;
      return shapes[name][0].test(v) ? null : shapes[name][1];
    }
    if (name === 'digits') return new RegExp('^\\d{' + arg + '}$').test(value) ? null : 'Enter the ' + arg + '-digit number';
    if (name === 'match') {
      var other = document.getElementById(arg);
      return other && value.replace(/\D/g, '') !== other.value.replace(/\D/g, '') ? 'Does not match the account number' : null;
    }
    if (name === 'date' || name === 'dmy') {
      var parts = value.replace(/\s/g, '').split('/');
      if (name === 'dmy' && parts.length !== 3) parts = [value.replace(/\D/g, '').slice(0, 2), value.replace(/\D/g, '').slice(2, 4), value.replace(/\D/g, '').slice(4)];
      var date = realDate(parts[0], parts[1], parts[2]);
      if (!date) return 'Enter a real date, DD/MM/YYYY';
      if (name === 'date' && date > new Date()) return 'Enter a date that is not in the future';
      return null;
    }
    if (name === 'amount') {
      var n = +value.replace(/[^\d]/g, '');
      var min = +el.getAttribute('data-min'), max = +el.getAttribute('data-max'), step = +el.getAttribute('data-step');
      if (min && n < min) return 'Below the ₹ ' + min.toLocaleString('en-IN') + ' minimum';
      if (max && n > max) return 'Above the ₹ ' + max.toLocaleString('en-IN') + ' maximum';
      if (step && n % step !== 0) return 'Not a multiple of ₹ ' + step.toLocaleString('en-IN');
      return null;
    }
    return null;
  }

  function errorOf(el, make) {
    var id = el.getAttribute('data-error-id') || (el.id + 'Error');
    var error = document.getElementById(id);
    if (!error && make) {
      error = document.createElement('p');
      error.className = 'csi-error';
      error.id = id;
      error.setAttribute('role', 'alert');
      var after = el.closest('.cud-select, .csi-date') || el;
      after.insertAdjacentElement('afterend', error);
    }
    return error;
  }

  function mark(el, message) {
    var control = el.matches('[role=group], .csi-date') ? el : el;
    var bad = el.classList.contains('inv-control') ? 'inv-control--error' : 'is-invalid';
    var error = errorOf(el, !!message);
    if (message) {
      error.textContent = message;
      error.hidden = false;
      control.classList.add(bad);
      control.setAttribute('aria-invalid', 'true');
      control.setAttribute('aria-describedby', error.id);
    } else {
      if (error) { error.textContent = ''; error.hidden = true; }
      control.classList.remove(bad);
      control.removeAttribute('aria-invalid');
    }
  }

  function checked(el) {
    return el.hasAttribute('data-required') || el.hasAttribute('data-check');
  }

  // Seen by the partner, and not switched off.
  function inPlay(el) {
    if (el.closest('[hidden], fieldset[disabled]')) return false;
    if (!el.getClientRects().length) return false;
    var input = el.matches('input, select, textarea') ? el : el.querySelector('input, select, textarea');
    return !(input && input.disabled);
  }

  function holder(target) {
    return target.closest && (target.closest('[data-check], [data-required]'));
  }

  // Left: said now.
  document.addEventListener('focusout', function (e) {
    var el = holder(e.target);
    if (!el || !checked(el) || !inPlay(el)) return;
    // A date's three boxes are one field: said once the caret has left all three.
    if (el.contains(e.relatedTarget)) return;
    mark(el, problem(el));
  });
  document.addEventListener('change', function (e) {
    var el = holder(e.target);
    if (el && checked(el) && inPlay(el) && e.target.tagName === 'SELECT') mark(el, problem(el));
  });

  // Put right: cleared at once, not only when the field is left.
  document.addEventListener('input', function (e) {
    var el = holder(e.target);
    if (!el || !checked(el)) return;
    var error = errorOf(el, false);
    if (error && !error.hidden && error.textContent && !problem(el)) mark(el, null);
  });

  // A button that checks its form first sends nothing while a field is wrong.
  document.addEventListener('submit', function (e) {
    var button = e.submitter;
    var within = button && button.getAttribute('data-validate');
    if (!within) return;
    var form = e.target;
    var scope = within === 'form' ? form : button.closest(within) || form;
    var first = null;
    scope.querySelectorAll('[data-check], [data-required]').forEach(function (el) {
      if (!inPlay(el)) return;
      var message = problem(el);
      mark(el, message);
      if (message && !first) first = el;
    });
    // Fields that stand outside the form but post with it.
    if (scope === form && form.id) document.querySelectorAll('[form="' + form.id + '"][data-check], [form="' + form.id + '"][data-required]').forEach(function (el) {
      if (!inPlay(el)) return;
      var message = problem(el);
      mark(el, message);
      if (message && !first) first = el;
    });
    if (!first) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    var focus = first.matches('input, select, textarea') ? first : first.querySelector('input, select, textarea');
    if (focus) {
      focus.focus({ preventScroll: true });
      if (first.scrollIntoView) first.scrollIntoView({ block: 'center' });
    }
  }, true);
})();
