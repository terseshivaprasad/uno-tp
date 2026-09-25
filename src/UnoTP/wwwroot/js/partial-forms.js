// Forms that post without the page reloading. A form marked data-partial is sent
// with fetch; the server answers it exactly as it answers an ordinary post - with
// a redirect back to the page - and the <main> of that page takes the place of
// this one. The header, the scroll position and anything else outside <main>
// stay where they were. Links marked data-partial are fetched the same way.
//
// Everything is still decided on the server: this only carries the post there and
// the answer back. Without this script the same forms post and redirect the
// ordinary way, and the buttons it presses for the partner are shown instead.
(function () {
  document.documentElement.classList.add('has-js');
  if (!window.fetch || !window.DOMParser) return;

  var busy = false;

  // A control that reshapes the page submits its form as soon as it changes,
  // through the button it names: a file picked is uploaded, a choice redraws.
  document.addEventListener('change', function (e) {
    var el = e.target.closest && e.target.closest('[data-submit]');
    if (!el) return;
    var button = document.getElementById(el.getAttribute('data-submit'));
    if (!button || !button.form) return;
    // Which control changed, so the page can come back to it.
    if (button.name === 'refresh') button.value = el.id || el.name;
    button.form.requestSubmit(button);
  });

  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form.hasAttribute('data-partial') || form.dataset.partialOff) return;
    e.preventDefault();
    if (busy) return;

    var button = e.submitter || null;
    var method = ((button && button.getAttribute('formmethod')) || form.getAttribute('method') || 'get').toLowerCase();
    var action = new URL((button && button.getAttribute('formaction')) || form.getAttribute('action') || '',
      window.location.href).href;
    var data = new FormData(form, button);
    // A browser older than the submitter argument leaves the pressed button out;
    // its name and value say what the post is for, so they go in by hand.
    if (button && button.name && !data.has(button.name)) data.append(button.name, button.value);
    var url = action;
    var init = { method: method.toUpperCase(), credentials: 'same-origin' };
    if (method === 'get') {
      var query = new URL(action, window.location.href);
      query.search = new URLSearchParams(data).toString();
      url = query.toString();
    } else {
      init.body = data;
    }

    go(url, init, method === 'get', button, function () {
      // Could not reach the server this way: post it the ordinary way instead.
      form.dataset.partialOff = '1';
      form.requestSubmit(button);
    });
  });

  document.addEventListener('click', function (e) {
    var link = e.target.closest && e.target.closest('a[data-partial]');
    if (!link || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    e.preventDefault();
    if (busy) return;
    go(link.href, { credentials: 'same-origin' }, true, link, function () { window.location.href = link.href; });
  });

  // A page opened from history is drawn again from the server.
  window.addEventListener('popstate', function () { window.location.reload(); });

  function go(url, init, push, from, fallback) {
    busy = true;
    var words = from && from.getAttribute('data-loader');
    if (words && window.showLoader) window.showLoader(words, from.getAttribute('data-loader-hint') || '');

    fetch(url, init)
      .then(function (res) {
        // Anything but a page to show - a refused post, a server error - goes the
        // ordinary way, so it is seen as it would be without this script.
        if (res.ok === false) { fallback(); return null; }
        // A post that ends on another page - Proceed to the next step - is a
        // move to that page, not an update of this one.
        var to = new URL(res.url);
        if (to.pathname !== window.location.pathname) {
          window.location.href = res.url;
          return null;
        }
        return res.text().then(function (html) { swap(html, res.url, push); });
      })
      .catch(fallback)
      .then(function () {
        busy = false;
        if (window.hideLoader) window.hideLoader();
      });
  }

  function swap(html, url, push) {
    var next = new DOMParser().parseFromString(html, 'text/html');
    var incoming = next.querySelector('main');
    var here = document.querySelector('main');
    if (!incoming || !here) { window.location.href = url; return; }

    here.innerHTML = incoming.innerHTML;
    document.title = next.title;
    // The address is what a reload would show: the page, not the post.
    window.history[push ? 'pushState' : 'replaceState'](null, '', url);

    // The part the answer is about - the first field with something wrong, the
    // document just filed - takes the caret, and is brought into view if it is off.
    var mark = here.querySelector('[data-partial-focus]');
    var id = mark && mark.getAttribute('data-partial-focus');
    var target = (id && document.getElementById(id)) || here.querySelector('[autofocus]');
    if (target) {
      target.focus({ preventScroll: true });
      var box = target.getBoundingClientRect();
      if (box.top < 60 || box.bottom > window.innerHeight - 70) target.scrollIntoView({ block: 'center' });
    }
    document.dispatchEvent(new CustomEvent('partial:swapped'));
  }
})();
