// The classic wizard steps that work on one application - Upload Documents and
// Investor Information. Everything they do is answered by the server; the forms
// post without a reload through partial-forms.js. What is left here is the little
// a server cannot do: put the application number on the clipboard, and keep who the
// step is for in view once the head - marked data-pin-head - has scrolled off.
(function () {
  // A partner reporting a problem sends a picture of the screen, and by then the
  // head has scrolled off: the strip keeps the name and the number in shot.
  var watching = null;
  function pin() {
    var strip = document.getElementById('cudPin');
    var head = document.querySelector('[data-pin-head]');
    if (watching) watching.disconnect();
    if (!strip || !head || !window.IntersectionObserver) return;
    watching = new window.IntersectionObserver(function (entries) {
      strip.hidden = entries[0].isIntersecting;
    }, { rootMargin: '-52px 0px 0px 0px' });
    watching.observe(head);
  }
  pin();
  // The page redraws its <main> after every post, the head with it.
  document.addEventListener('partial:swapped', pin);

  // The number every question about this application starts with, one press away.
  document.addEventListener('click', function (e) {
    var button = e.target.closest && e.target.closest('[data-copy]');
    if (!button || !navigator.clipboard) return;
    navigator.clipboard.writeText(button.getAttribute('data-copy')).then(function () {
      button.classList.add('is-copied');
      button.title = 'Copied';
      window.setTimeout(function () {
        button.classList.remove('is-copied');
        button.title = 'Copy the application number';
      }, 1400);
    });
  });
})();
