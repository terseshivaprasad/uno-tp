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
  // A post made while another is on its way: it waits its turn rather than being
  // dropped, taken as the form stood when it was made. Only the latest waits - it
  // carries every change before it, since the form holds them all.
  var waiting = null;
  // Set while a changed control submits its form, so only such a post waits: a
  // button pressed twice still goes once.
  var changing = false;
  // The drop-down whose change is on its way: once the page comes back, the field
  // after it takes the caret, as it would have had the partner pressed Tab.
  var advanceFrom = null;

  // What a choice shows or hides is shown or hidden the moment it is made, before
  // the server has heard of it: the page draws every part a choice can bring up,
  // each marked with the choice it goes with -
  //   data-show-when="appType=P"          shown while the field holds P
  //   data-show-when="payMode=Cheque|DD"  shown while it holds either
  //   data-show-when="Repayment.SameAsPayment!=true"
  // Prefer data-enable-when, which keeps the part in place and greys it, so the page
  // does not move as a choice is made; data-show-when is for one part swapped for another.
  // A fieldset hidden this way is disabled too, so nothing in it is posted; a
  // control marked data-enable-when is enabled or disabled the same way. The
  // server still decides everything, and the page it sends back replaces this.
  function applyShowWhen(form) {
    if (!form) return;
    var data = new FormData(form);
    function holds(rule) {
      var r = /^([^!=]+)(!?=)(.*)$/.exec(rule || '');
      if (!r || !form.elements[r[1]]) return null;
      var value = data.has(r[1]) ? String(data.get(r[1])) : '';
      var match = r[3].split('|').indexOf(value) >= 0;
      return r[2] === '=' ? match : !match;
    }
    document.querySelectorAll('[data-show-when]').forEach(function (el) {
      var show = holds(el.getAttribute('data-show-when'));
      if (show === null) return;
      el.hidden = !show;
      if (el.tagName === 'FIELDSET') el.disabled = !show;
    });
    // A control another one fixes - data-fixed-by names it, data-fixed-when the value
    // that fixes it, data-fixed-value what it is then: fixed, it takes that value and
    // is disabled, and a hidden field ({id}Fixed) posts the value in its place; free,
    // the fixed value is not one it can be given.
    document.querySelectorAll('[data-fixed-by]').forEach(function (el) {
      var by = form.elements[el.getAttribute('data-fixed-by')];
      if (!by || by.length > 1 && !by.tagName) return;
      var fixed = String(by.value) !== '' && String(by.value) === el.getAttribute('data-fixed-when');
      var value = el.getAttribute('data-fixed-value');
      if (fixed) el.value = value; else if (el.value === value) el.value = '';
      el.disabled = fixed;
      Array.prototype.forEach.call(el.options || [], function (o) { if (o.value === value) o.disabled = !fixed; });
      var carry = document.getElementById(el.id + 'Fixed');
      if (carry) carry.disabled = !fixed;
    });
    // A control that only takes a value while a choice holds - data-enable-when -
    // is disabled otherwise; anything else so marked (a card) stays in place, greyed.
    document.querySelectorAll('[data-enable-when]').forEach(function (el) {
      var on = holds(el.getAttribute('data-enable-when'));
      if (on === null) return;
      if (/^(INPUT|SELECT|TEXTAREA|BUTTON|FIELDSET)$/.test(el.tagName)) el.disabled = !on;
      else el.setAttribute('aria-disabled', on ? 'false' : 'true');
    });
  }
  window.applyShowWhen = applyShowWhen;

  // As on a bank's form, a step is saved by its Save or Proceed, not change by
  // change. A step's form marked data-guard notes a change not yet sent, and
  // leaving the page by a link, or closing it, asks first. Any post of the form
  // sends everything in it, so a post, and the page it brings back, clears it.
  var unsaved = false;
  function guarded(el) {
    var form = el && (el.form || (el.closest && el.closest('form')));
    return !!(form && form.hasAttribute('data-guard'));
  }
  document.addEventListener('input', function (e) { if (guarded(e.target) && e.target.type !== 'file') unsaved = true; });
  document.addEventListener('change', function (e) { if (guarded(e.target) && e.target.type !== 'file') unsaved = true; }, true);
  document.addEventListener('submit', function () { unsaved = false; }, true);
  document.addEventListener('click', function (e) {
    var link = e.target.closest && e.target.closest('a[href]');
    if (!unsaved || !link || link.target === '_blank' || link.getAttribute('href').charAt(0) === '#' || e.defaultPrevented) return;
    if (!window.confirm('This step has changes that are not saved yet. Leave it without saving them?')) {
      e.preventDefault();
      e.stopImmediatePropagation();
    } else {
      unsaved = false;
    }
  }, true);
  window.addEventListener('beforeunload', function (e) {
    if (!unsaved) return;
    e.preventDefault();
    e.returnValue = '';
  });

  // A control that reshapes the page submits its form as soon as it changes,
  // through the button it names: a file picked is uploaded, a choice redraws.
  // A button that takes something off asks first; cancelled, nothing is posted.
  document.addEventListener('click', function (e) {
    var button = e.target.closest && e.target.closest('button[data-confirm]');
    if (button && !window.confirm(button.getAttribute('data-confirm'))) { e.preventDefault(); e.stopImmediatePropagation(); }
  }, true);

  document.addEventListener('change', function (e) {
    var el = e.target.closest && e.target.closest('[data-submit]');
    if (!el) { applyShowWhen(e.target.form); return; }
    var button = document.getElementById(el.getAttribute('data-submit'));
    if (!button || !button.form) return;
    // A change that takes something off is asked about first; cancelled, the
    // control goes back to what the page drew it with.
    var ask = el.getAttribute('data-confirm');
    if (ask && !window.confirm(ask)) { restore(el); return; }
    applyShowWhen(button.form);
    // Which control changed, so the page can come back to it.
    if (button.name === 'refresh') button.value = el.id || el.name;
    advanceFrom = el.tagName === 'SELECT' && el.id ? el.id : null;
    changing = true;
    try { button.form.requestSubmit(button); } finally { changing = false; }
  });

  function restore(el) {
    if (el.type === 'radio') {
      var group = el.form ? el.form.elements[el.name] : [el];
      Array.prototype.forEach.call(group.length === undefined ? [group] : group, function (r) { r.checked = r.defaultChecked; });
    } else if (el.type === 'checkbox') {
      el.checked = el.defaultChecked;
    } else if (el.options) {
      Array.prototype.forEach.call(el.options, function (o) { o.selected = o.defaultSelected; });
    } else {
      el.value = el.defaultValue;
    }
  }

  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form.hasAttribute('data-partial') || form.dataset.partialOff) return;
    e.preventDefault();

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
      // Which page this is: a post that only comes back to it is answered with the
      // page itself, in one round trip rather than a redirect and a second request
      // (see PartialFollow on the server).
      init.headers = { 'X-Partial-Page': window.location.pathname };
    }

    var fallback = function () {
      // Could not reach the server this way: post it the ordinary way instead.
      form.dataset.partialOff = '1';
      form.requestSubmit(button);
    };
    if (busy) {
      if (changing) waiting = [url, init, method === 'get', button, fallback];
      return;
    }
    go(url, init, method === 'get', button, fallback);
  });

  document.addEventListener('click', function (e) {
    var link = e.target.closest && e.target.closest('a[data-partial]');
    if (!link || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    e.preventDefault();
    if (busy) return;
    go(link.href, { credentials: 'same-origin' }, true, link, function () { window.location.href = link.href; });
  });

  // The history's attempts are folded; a link to one ("See the history") opens it.
  function openTarget() {
    var id = window.location.hash.slice(1);
    var row = id && document.getElementById(id);
    var item = row && row.querySelector('details');
    if (item) item.open = true;
  }
  window.addEventListener('hashchange', openTarget);
  document.addEventListener('click', function (e) {
    var link = e.target.closest && e.target.closest('a[href^="#log-"]');
    if (link) setTimeout(openTarget, 0);
  });
  openTarget();
  // Landed on a box (#slot-...): the browser has brought it into view; the address
  // lets it go, so a reload does not ring it again.
  if (/^#slot-/.test(window.location.hash)) window.history.replaceState(null, '', withoutSpot(window.location.href));

  // A page opened from history is drawn again from the server.
  window.addEventListener('popstate', function () { window.location.reload(); });

  // Every text box's value by id, as it stands.
  function typedNow() {
    var values = {};
    var main = document.querySelector('main');
    if (main) main.querySelectorAll('input, textarea').forEach(function (f) {
      if (f.id && !/^(checkbox|radio|file|hidden|submit|button)$/.test(f.type)) values[f.id] = f.value;
    });
    return values;
  }

  // A quick action says so on its own button, and once done in a toast - the
  // screen-wide wait is for the waits that are real (an upload, a step).
  function toast(words) {
    var box = document.getElementById('toast');
    if (!box) {
      box = document.createElement('div');
      box.id = 'toast';
      box.className = 'toast';
      box.setAttribute('role', 'status');
      box.setAttribute('aria-live', 'polite');
      document.body.appendChild(box);
    }
    box.textContent = words;
    box.classList.add('toast--visible');
    clearTimeout(toast.timer);
    toast.timer = setTimeout(function () { box.classList.remove('toast--visible'); }, 2400);
  }

  function go(url, init, push, from, fallback) {
    busy = true;
    var sent = typedNow();
    var quick = from && from.matches && from.matches('button') && !from.getAttribute('data-loader');
    var said = quick && from.getAttribute('data-toast');
    if (quick) { from.classList.add('is-busy'); from.setAttribute('aria-busy', 'true'); }
    var words = from && from.getAttribute('data-loader');
    if (words && window.showLoader) window.showLoader(words, from.getAttribute('data-loader-hint') || '');
    // Anything else - a toggle, a drop-down - says it is on its way with a thin bar
    // across the top, and leaves the page to be used meanwhile.
    else document.documentElement.classList.add('is-saving');

    // A file on its way says how far it has got - on a slow connection the send is
    // most of the wait - and then what the server is doing with it.
    var bytes = filesIn(init.body);
    var onProgress = bytes > 0 && words && window.setLoaderHint ? function (sent, total) {
      window.setLoaderHint(sent < total
        ? 'Uploading ' + Math.floor(sent * 100 / total) + '% of ' + size(bytes)
        : from.getAttribute('data-loader-hint') || '');
    } : null;

    send(url, init, onProgress)
      .then(function (res) {
        // Anything but a page to show - a refused post, a server error - goes the
        // ordinary way, so it is seen as it would be without this script.
        if (res.ok === false) { fallback(); return null; }
        // Where the answer stands: the page the server followed on to, or where
        // the browser was redirected.
        var at = res.headers.get('X-Partial-Url');
        var answered = at ? new URL(at, window.location.href).href : res.url;
        // A post that ends on another page - Proceed to the next step - is a
        // move to that page, not an update of this one.
        var to = new URL(answered);
        if (to.pathname !== window.location.pathname) {
          waiting = null;
          window.location.href = answered;
          return null;
        }
        // A newer post is waiting, so this answer is already out of date: drawing
        // it would put back what the partner has changed since.
        if (waiting) return null;
        return res.text().then(function (html) { swap(html, answered, push, sent); if (said) toast(said); });
      })
      .catch(fallback)
      .then(function () {
        busy = false;
        if (window.hideLoader) window.hideLoader();
        document.documentElement.classList.remove('is-saving');
        if (quick) { from.classList.remove('is-busy'); from.removeAttribute('aria-busy'); }
        if (waiting) {
          var next = waiting;
          waiting = null;
          go.apply(null, next);
        }
      });
  }

  // The size of the files a post carries.
  function filesIn(body) {
    var total = 0;
    if (body && body.forEach) body.forEach(function (value) { if (value instanceof File) total += value.size; });
    return total;
  }

  function size(n) {
    return n < 1024 * 1024 ? Math.max(1, Math.round(n / 1024)) + ' KB' : (n / 1024 / 1024).toFixed(1) + ' MB';
  }

  // fetch, but for a post carrying a file: XMLHttpRequest, which alone reports
  // how much of the body has gone. It answers with the little of a Response the
  // caller reads.
  function send(url, init, onProgress) {
    if (!onProgress) return fetch(url, init);
    return new Promise(function (resolve, reject) {
      var xhr = new XMLHttpRequest();
      xhr.open(init.method || 'POST', url);
      xhr.withCredentials = true;
      Object.keys(init.headers || {}).forEach(function (name) { xhr.setRequestHeader(name, init.headers[name]); });
      xhr.upload.onprogress = function (e) { if (e.lengthComputable) onProgress(e.loaded, e.total); };
      xhr.upload.onload = function () { onProgress(1, 1); };
      xhr.onload = function () {
        resolve({
          ok: xhr.status >= 200 && xhr.status < 300,
          url: xhr.responseURL,
          headers: { get: function (name) { return xhr.getResponseHeader(name); } },
          text: function () { return Promise.resolve(xhr.responseText); }
        });
      };
      xhr.onerror = xhr.onabort = function () { reject(new Error('Network')); };
      xhr.send(init.body);
    });
  }

  // A link into the history (#log-...) is kept; a box or field to come back to
  // (#slot-..., #cudCategory) is dealt with below, so it is left off the address.
  function withoutSpot(url) {
    var at = new URL(url, window.location.href);
    if (at.hash && !/^#log-/.test(at.hash)) at.hash = '';
    return at.href;
  }

  // The first field after the one named, in the page's order, that can take the caret.
  function fieldAfter(id) {
    var from = document.getElementById(id);
    if (!from) return null;
    var fields = Array.prototype.slice.call(document.querySelectorAll('main input, main select, main textarea'))
      .filter(function (f) { return !f.disabled && f.type !== 'hidden' && f.type !== 'file' && f.offsetParent !== null; });
    var at = fields.indexOf(from);
    return at >= 0 ? fields[at + 1] || null : null;
  }

  function swap(html, url, push, sent) {
    var next = new DOMParser().parseFromString(html, 'text/html');
    var incoming = next.querySelector('main');
    var here = document.querySelector('main');
    if (!incoming || !here) { window.location.href = url; return; }

    // On a slow connection the partner goes on typing while a change is on its
    // way. Whatever they typed after it was sent is kept over the page that comes
    // back, and the caret stays where they are. What was sent is the server's to
    // answer - a Clear All clears.
    var now = typedNow();
    var typed = Object.keys(now).filter(function (id) { return sent && id in sent && now[id] !== sent[id]; })
      .map(function (id) { return [id, now[id]]; });
    var active = document.activeElement;
    var typing = active && active.id && here.contains(active) && /^(INPUT|TEXTAREA|SELECT)$/.test(active.tagName)
      && !/^(checkbox|radio|file|submit|button)$/.test(active.type || '');
    var caret = typing && active.selectionStart != null ? [active.selectionStart, active.selectionEnd] : null;
    var activeId = typing ? active.id : null;

    here.innerHTML = incoming.innerHTML;
    document.title = next.title;
    // What the page now holds is what the server has; only what was typed since
    // the post, put back below, is not.
    // The address is what a reload would show: the page, not the post - and not
    // the box the answer was about, or a reload would land on it again, ringed.
    window.history[push ? 'pushState' : 'replaceState'](null, '', withoutSpot(url));

    unsaved = typed.length > 0;
    typed.forEach(function (t) {
      var f = document.getElementById(t[0]);
      if (f) f.value = t[1];
    });
    // A drop-down just changed, and the partner has not gone on to another field
    // meanwhile: the field after it takes the caret.
    var advance = advanceFrom;
    advanceFrom = null;
    if (advance && (!activeId || activeId === advance)) {
      var after = fieldAfter(advance) || document.getElementById(advance);
      if (after) {
        after.focus({ preventScroll: true });
        var r = after.getBoundingClientRect();
        if (r.top < 60 || r.bottom > window.innerHeight - 70) after.scrollIntoView({ block: 'nearest' });
        document.dispatchEvent(new CustomEvent('partial:swapped'));
        return;
      }
    }
    if (activeId && document.getElementById(activeId)) {
      var back = document.getElementById(activeId);
      back.focus({ preventScroll: true });
      if (caret && back.setSelectionRange) try { back.setSelectionRange(caret[0], caret[1]); } catch (_) { /* not a text box */ }
      document.dispatchEvent(new CustomEvent('partial:swapped'));
      return;
    }

    // The part the answer is about - the first field with something wrong, the
    // document just filed - takes the caret, and is brought into view if it is off.
    var mark = here.querySelector('[data-partial-focus]');
    var id = mark && mark.getAttribute('data-partial-focus');
    var target = (id && document.getElementById(id)) || here.querySelector('[autofocus]');
    if (target) {
      // Inside a folded card (a joint holder's), the card opens first.
      for (var fold = target.closest('details:not([open])'); fold; fold = fold.parentElement && fold.parentElement.closest('details:not([open])')) fold.open = true;
      target.focus({ preventScroll: true });
      var box = target.getBoundingClientRect();
      if (box.top < 60 || box.bottom > window.innerHeight - 70) target.scrollIntoView({ block: 'center' });
    }
    document.dispatchEvent(new CustomEvent('partial:swapped'));
  }
})();
