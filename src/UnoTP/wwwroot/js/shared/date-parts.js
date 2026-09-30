// The three-box dates (DD / MM / YYYY) and the time boxes beside them, on every
// page, the way the old jq-dte control behaves: only digits go in, a full box rolls
// on to the next, Backspace in an empty box goes back to the one before, and a
// whole date pasted into any box is spread across all three. Listened for on the
// document, so boxes drawn again after a post (partial-forms.js) work the same.
(function () {
  function parts(box) {
    return Array.prototype.slice.call(box.querySelectorAll('.csi-date__part'));
  }

  function boxOf(el) {
    return el.classList && el.classList.contains('csi-date__part') ? el.closest('.csi-date') : null;
  }

  document.addEventListener('input', function (e) {
    var box = boxOf(e.target);
    if (!box) return;
    var part = e.target;
    var digits = part.value.replace(/\D/g, '');
    if (digits !== part.value) part.value = digits;
    var all = parts(box);
    var next = all[all.indexOf(part) + 1];
    if (next && part.maxLength > 0 && part.value.length >= part.maxLength) {
      next.focus();
      next.select();
    }
  });

  document.addEventListener('keydown', function (e) {
    var box = boxOf(e.target);
    if (!box || e.key !== 'Backspace' || e.target.value !== '') return;
    var all = parts(box);
    var previous = all[all.indexOf(e.target) - 1];
    if (!previous) return;
    e.preventDefault();
    previous.focus();
    previous.value = previous.value.slice(0, -1);
  });

  // 14-08-1988, 14/08/1988, 14.08.1988 or 14081988, into whichever box it lands in.
  document.addEventListener('paste', function (e) {
    var box = boxOf(e.target);
    if (!box || !e.clipboardData) return;
    var all = parts(box);
    if (all.length !== 3) return;
    var text = e.clipboardData.getData('text').trim();
    var m = text.match(/^(\d{1,2})\D(\d{1,2})\D(\d{4})$/) || text.match(/^(\d{2})(\d{2})(\d{4})$/);
    if (!m) return;
    e.preventDefault();
    all[0].value = m[1].padStart(2, '0');
    all[1].value = m[2].padStart(2, '0');
    all[2].value = m[3];
    all[2].focus();
    all.forEach(function (p) { p.dispatchEvent(new Event('change', { bubbles: true })); });
  });
})();
