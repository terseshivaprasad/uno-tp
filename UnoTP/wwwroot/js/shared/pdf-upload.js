// A one-page PDF picked for a document box is drawn as a JPEG before it is uploaded,
// so what reaches the document check (IDfy) and the file system is an image within
// what IDfy takes: no more than the box's data-shrink pixels on its long side (2560)
// and under 2 MB (MAX_BYTES, the slots' 2 MB; a lower JPEG quality first, then smaller).
// A PDF of more than one page is refused here, before anything is sent: the box says
// so (data-pages-message, Messages.UploadDocuments.PdfHasManyPages) and the pick is
// cleared. One the browser cannot read or draw goes exactly as picked. The PDF is
// drawn by pdf.js (js/vendor/pdfjs), loaded only when a PDF is picked. The
// application form's box has no data-shrink, and the employee proof's says
// data-pdf-as-picked, so those go as picked whatever their length.
//
// It runs before image-shrink.js and partial-forms.js see the file: the change is
// held, the file in the box replaced with the JPEG, and the change let through again.
(function () {
  var JPEG_QUALITY = 0.9;
  var LEAST_QUALITY = 0.5;
  var MAX_BYTES = 2 * 1024 * 1024;
  if (!window.DataTransfer || !HTMLCanvasElement.prototype.toBlob) return;
  // Where pdf.js is, beside this script.
  var here = document.currentScript && document.currentScript.src;
  if (!here) return;
  var pdfjsLoading = null;

  document.addEventListener('change', function (e) {
    var input = e.target;
    if (!input || input.type !== 'file' || !input.files || input.files.length !== 1) return;
    if (input.dataset.pdfDrawn === '1') { delete input.dataset.pdfDrawn; return; }
    var longEdge = parseInt(input.dataset.shrink, 10);
    var file = input.files[0];
    if (!longEdge || !isPdf(file)) return;
    // A box that takes a PDF as picked, whatever its length: the employee proof.
    if (input.dataset.pdfAsPicked === '1') return;

    // Held: the change goes on once the JPEG is in the box - or the PDF, as picked.
    // A PDF of more than one page is refused: the pick is cleared and nothing goes on.
    e.stopImmediatePropagation();
    clearRefusal(input);
    onePageAsJpeg(file, longEdge).then(function (jpeg) {
      if (jpeg === MANY_PAGES) { refuse(input); return false; }
      if (jpeg) {
        var box = new DataTransfer();
        box.items.add(jpeg);
        input.files = box.files;
      }
      return true;
    }).catch(function () { return true; /* the PDF goes as picked */ }).then(function (goesOn) {
      if (!goesOn) return;
      input.dataset.pdfDrawn = '1';
      input.dispatchEvent(new Event('change', { bubbles: true }));
    });
  }, true);

  var MANY_PAGES = 'many-pages';

  // The box's error line, under it, as the server draws one (the slot's notes).
  function refuse(input) {
    input.value = '';
    var key = input.id.replace(/^file-/, '');
    var notes = document.querySelector('#slot-' + key + ' .doc-slot__notes');
    if (!notes) return;
    var line = document.getElementById(key + 'Error');
    if (!line) {
      line = document.createElement('p');
      line.className = 'field-error';
      line.id = key + 'Error';
      line.setAttribute('role', 'alert');
      notes.insertBefore(line, notes.firstChild);
    }
    line.textContent = input.dataset.pagesMessage || 'Multiple pages found.';
    line.dataset.pagesRefusal = '1';
  }

  // A refusal said here is taken off once another file is picked.
  function clearRefusal(input) {
    var line = document.getElementById(input.id.replace(/^file-/, '') + 'Error');
    if (line && line.dataset.pagesRefusal === '1') line.remove();
  }

  function isPdf(file) {
    return /^application\/pdf$/i.test(file.type) || /\.pdf$/i.test(file.name);
  }

  function pdfjs() {
    if (!pdfjsLoading) {
      pdfjsLoading = import(new URL('../vendor/pdfjs/pdf.min.mjs', here).href).then(function (lib) {
        lib.GlobalWorkerOptions.workerSrc = new URL('../vendor/pdfjs/pdf.worker.min.mjs', here).href;
        return lib;
      });
    }
    return pdfjsLoading;
  }

  // The PDF's one page as a JPEG file; MANY_PAGES for one of more pages, null for one of none.
  function onePageAsJpeg(file, longEdge) {
    return Promise.all([pdfjs(), file.arrayBuffer()]).then(function (ready) {
      return ready[0].getDocument({ data: ready[1] }).promise;
    }).then(function (pdf) {
      if (pdf.numPages > 1) return MANY_PAGES;
      if (pdf.numPages !== 1) return null;
      return pdf.getPage(1).then(function (page) {
        // Drawn with its long edge at the box's size, whatever the page's own.
        var size = page.getViewport({ scale: 1 });
        var viewport = page.getViewport({ scale: longEdge / Math.max(size.width, size.height) });
        var canvas = document.createElement('canvas');
        canvas.width = Math.ceil(viewport.width);
        canvas.height = Math.ceil(viewport.height);
        var ctx = canvas.getContext('2d');
        ctx.fillStyle = '#fff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        return page.render({ canvasContext: ctx, viewport: viewport }).promise.then(function () { return toJpeg(canvas); });
      }).then(function (blob) {
        var name = file.name.replace(/\.pdf$/i, '') + '.jpg';
        return new File([blob], name, { type: 'image/jpeg', lastModified: file.lastModified });
      });
    });
  }

  // The canvas as a JPEG under the size: a lower quality first, and smaller only when that is not enough.
  function toJpeg(canvas) {
    var drawn = canvas;
    function attempt(quality) {
      return encode(drawn, quality).then(function (blob) {
        if (blob.size <= MAX_BYTES) return blob;
        if (quality - 0.1 >= LEAST_QUALITY - 0.001) return attempt(quality - 0.1);
        var smaller = document.createElement('canvas');
        smaller.width = Math.round(drawn.width * 0.8);
        smaller.height = Math.round(drawn.height * 0.8);
        smaller.getContext('2d').drawImage(drawn, 0, 0, smaller.width, smaller.height);
        drawn = smaller;
        return attempt(JPEG_QUALITY);
      });
    }
    return attempt(JPEG_QUALITY);
  }

  function encode(canvas, quality) {
    return new Promise(function (resolve, reject) {
      canvas.toBlob(function (blob) { blob ? resolve(blob) : reject(new Error('encode')); }, 'image/jpeg', quality);
    });
  }
})();
