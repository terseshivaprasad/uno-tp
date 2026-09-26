// FD Configuration: a choice is saved, and the backend's quote for the deposit
// drawn again, without the page being sent. Every control marked data-quote posts
// the form to FdQuote, which saves the deposit and answers with the quote panel
// alone; what a choice shows or hides has already changed (partial-forms.js). The
// amount is quoted as it is typed, once the partner pauses; what is wrong with it
// is said once they leave the field, not while they are still typing it.
(function () {
  var form = document.getElementById('fdConfigForm');
  if (!form || !window.fetch) return;
  var url = form.getAttribute('data-quote-url');
  var timer = null;
  var asked = 0;

  function quote(sayProblem) {
    var mine = ++asked;
    document.documentElement.classList.add('is-saving');
    fetch(url, { method: 'POST', body: new FormData(form), credentials: 'same-origin' })
      .then(function (res) {
        if (!res.ok) throw new Error(res.status);
        return res.text();
      })
      .then(function (html) {
        // A later choice is already on its way: its answer is the one to show.
        if (mine !== asked) return;
        var panel = document.getElementById('fd-quote');
        if (panel) panel.innerHTML = html;
        amount(sayProblem);
      })
      .catch(function () {
        // Not saved this way - the application changed elsewhere, or the connection
        // dropped: the page is posted the ordinary way, and says what happened.
        if (mine !== asked) return;
        var refresh = document.getElementById('fdRefresh');
        if (refresh) form.requestSubmit(refresh);
      })
      .then(function () {
        if (mine === asked) document.documentElement.classList.remove('is-saving');
      });
  }

  // What the answer says of the amount: the line under it, and its error once due.
  function amount(sayProblem) {
    var said = document.querySelector('#fd-quote [data-fd-amount]');
    var input = document.getElementById('fd-amount');
    if (!said || !input) return;
    var hint = document.getElementById('fd-amount-hint');
    if (hint) hint.textContent = said.getAttribute('data-hint') || '';
    var problem = said.getAttribute('data-problem');
    var error = document.getElementById('fd-amountError');
    // The amount sits in a box with the rupee sign: that box takes the red edge,
    // and the error goes under it.
    var box = input.closest('.fd-amount') || input;
    if (!problem) {
      box.classList.remove('fd-amount--error');
      input.classList.remove('is-invalid');
      input.removeAttribute('aria-invalid');
      if (error) error.remove();
      input.setAttribute('aria-describedby', 'fd-amount-hint');
      return;
    }
    if (!sayProblem) return;
    if (!error) {
      error = document.createElement('p');
      error.className = 'csi-error';
      error.id = 'fd-amountError';
      error.setAttribute('role', 'alert');
      box.insertAdjacentElement('afterend', error);
    }
    error.textContent = problem;
    box.classList.add('fd-amount--error');
    input.classList.add('is-invalid');
    input.setAttribute('aria-invalid', 'true');
    input.setAttribute('aria-describedby', 'fd-amountError');
  }

  // Renew for the same tenure: said as the tenure is chosen.
  function renewFor() {
    var tenure = form.querySelector('input[name="TenureMonths"]:checked');
    var field = document.getElementById('fd-renew-for');
    if (tenure && field) field.value = 'Same tenure — ' + tenure.value + ' months';
  }

  form.addEventListener('change', function (e) {
    if (!e.target.matches('[data-quote]')) return;
    clearTimeout(timer);
    renewFor();
    quote(true);
  });

  form.addEventListener('input', function (e) {
    if (e.target.id !== 'fd-amount') return;
    clearTimeout(timer);
    timer = setTimeout(function () { quote(false); }, 600);
  });
})();
