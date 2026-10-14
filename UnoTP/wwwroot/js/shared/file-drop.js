// A file dragged in from the desktop can be dropped on a document card instead of
// picked: on an empty box, or on a filed one to replace its copy. While the file is
// over a card that can take it, the card's box shows it. A card that cannot take a
// file now - not asked for, waiting on something else, final, or out of tries - has
// no picker switched on, and takes no drop either. The dropped file is put in the
// card's own picker and the picker's change is raised, so it is checked, made
// smaller and uploaded exactly as a picked file is.
(function () {
  if (!window.DataTransfer) return;

  // The box a file is over now; null when it is over none that can take it.
  var boxOver = null;

  // The picker of the card under the pointer, when the card can take a file now.
  function pickerUnder(target) {
    var card = target.closest && target.closest('.doc-slot');
    if (!card) return null;
    var picker = card.querySelector('input[type="file"]');
    if (!picker || picker.disabled) return null;
    return picker;
  }

  // Whether what is being dragged is a file, not text or a link from the page.
  function carriesFiles(e) {
    var types = e.dataTransfer && e.dataTransfer.types;
    return !!types && Array.prototype.indexOf.call(types, 'Files') !== -1;
  }

  // Shows which box the file is over, and takes the mark off the one it left.
  function markBox(box) {
    if (boxOver === box) return;
    if (boxOver) boxOver.classList.remove('is-over');
    boxOver = box;
    if (boxOver) boxOver.classList.add('is-over');
  }

  document.addEventListener('dragover', function (e) {
    if (!carriesFiles(e)) return;
    var picker = pickerUnder(e.target);
    markBox(picker ? picker.closest('.doc-slot').querySelector('.doc-drop') : null);
    if (!picker) return;
    // Says the drop is taken here; without it the browser opens the file instead.
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  });

  // The file was dragged out of the window altogether.
  document.addEventListener('dragleave', function (e) {
    if (!e.relatedTarget) markBox(null);
  });

  document.addEventListener('drop', function (e) {
    var picker = carriesFiles(e) ? pickerUnder(e.target) : null;
    markBox(null);
    if (!picker) return;
    e.preventDefault();
    var file = e.dataTransfer.files[0];
    if (!file) return;

    // One copy to a card: the first file dropped goes in the card's picker.
    var holder = new DataTransfer();
    holder.items.add(file);
    picker.files = holder.files;
    picker.dispatchEvent(new Event('change', { bubbles: true }));
  });
})();
