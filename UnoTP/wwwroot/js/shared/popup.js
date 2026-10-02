// A popup the server asks for. A page that carries an element marked data-popup-open
// has something to say over itself - a try turned back by the limit on documents -
// and it is opened as the page arrives: on a full load, and when the answer to a
// posted form is put in place (partial-forms.js). A browser without popovers says
// it in an alert instead.
(function () {
  function openPopups() {
    var popups = document.querySelectorAll('[data-popup-open]');
    for (var i = 0; i < popups.length; i++) {
      var popup = popups[i];
      // Once only: the mark goes, so a second pass does not open it again.
      popup.removeAttribute('data-popup-open');
      if (popup.showPopover) {
        popup.showPopover();
      } else {
        var text = popup.querySelector('#noticePopupText');
        window.alert(text ? text.textContent : '');
      }
    }
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', openPopups);
  else openPopups();
  document.addEventListener('partial:swapped', openPopups);
})();
