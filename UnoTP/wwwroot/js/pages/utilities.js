// Document utilities, all in the browser: an image drawn again as a JPEG, a PDF drawn
// as one JPEG a page, and two images drawn as one. A file picked here is read on this
// computer and never sent anywhere; a result is a JPEG the partner downloads.
// The PDF is drawn by pdf.js (Mozilla, Apache-2.0), served from js/vendor/pdfjs.
import * as pdfjs from '../vendor/pdfjs/pdf.min.mjs';

pdfjs.GlobalWorkerOptions.workerSrc = new URL('../vendor/pdfjs/pdf.worker.min.mjs', import.meta.url).href;

// Every result meets what Upload Documents and the document check (IDfy) take: a
// JPEG, no more than MAX_LONG_EDGE pixels on its long side - the same as the upload's
// own shrink (data-shrink in _DocSlot.cshtml) - and under MAX_BYTES, the slots' 2 MB
// (Accepts in DocumentsViewModel.DocumentSlots.cs; IDfy's own cap is about 2.25 MB).
// It is drawn at JPEG_QUALITY and, while still over the size, at a lower quality step
// by step, then smaller.
var JPEG_QUALITY = 0.9;
var LEAST_QUALITY = 0.5;
var MAX_LONG_EDGE = 2560;
var MAX_BYTES = 2 * 1024 * 1024;
// Larger than this on a side, the browser cannot draw it.
var MAX_SIDE = 16384;

// ---- Drawing ---------------------------------------------------------------
// A canvas of a size, white, as every result starts.
function whiteCanvas(width, height) {
  var canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  var ctx = canvas.getContext('2d');
  ctx.fillStyle = '#fff';
  ctx.fillRect(0, 0, width, height);
  return canvas;
}

// The canvas as a JPEG file in memory, at a quality.
function encode(canvas, quality) {
  return new Promise(function (resolve) { canvas.toBlob(resolve, 'image/jpeg', quality); });
}

// The canvas drawn again with its long edge at most MAX_LONG_EDGE; itself when it fits.
function fitted(canvas) {
  var scale = Math.min(1, MAX_LONG_EDGE / Math.max(canvas.width, canvas.height));
  if (scale === 1) return canvas;
  var smaller = whiteCanvas(Math.round(canvas.width * scale), Math.round(canvas.height * scale));
  smaller.getContext('2d').drawImage(canvas, 0, 0, smaller.width, smaller.height);
  return smaller;
}

// The canvas as a JPEG the upload takes: within the long edge and under the size -
// a lower quality first, and smaller only when that is not enough.
async function toJpeg(canvas) {
  var drawn = fitted(canvas);
  while (true) {
    for (var quality = JPEG_QUALITY; quality >= LEAST_QUALITY - 0.001; quality -= 0.1) {
      var blob = await encode(drawn, quality);
      if (blob.size <= MAX_BYTES) return { blob: blob, width: drawn.width, height: drawn.height };
    }
    var smaller = whiteCanvas(Math.round(drawn.width * 0.8), Math.round(drawn.height * 0.8));
    smaller.getContext('2d').drawImage(drawn, 0, 0, smaller.width, smaller.height);
    drawn = smaller;
  }
}

// The picked file's name with a new ending.
function named(file, ending) {
  return file.name.replace(/\.[^.]+$/, '') + ending;
}

// An image file decoded, upright as the camera meant it, or null when the browser cannot read it.
async function bitmapOf(file) {
  try { return await createImageBitmap(file, { imageOrientation: 'from-image' }); } catch (e) { return null; }
}

// ---- Results ---------------------------------------------------------------
// A result card: the picture, its size, and a download link that takes the JPEG.
function showResult(box, blob, fileName, caption) {
  var url = URL.createObjectURL(blob);
  var card = document.createElement('figure');
  card.className = 'utility__result';
  var img = document.createElement('img');
  img.src = url;
  img.alt = caption;
  var note = document.createElement('figcaption');
  var text = document.createElement('span');
  text.textContent = caption + ' · ' + Math.round(blob.size / 1024) + ' KB · ready to upload';
  var link = document.createElement('a');
  link.className = 'link-button';
  link.href = url;
  link.download = fileName;
  link.textContent = 'Download';
  note.appendChild(text);
  note.appendChild(link);
  card.appendChild(img);
  card.appendChild(note);
  box.appendChild(card);
  return link;
}

function clear(box) {
  box.querySelectorAll('img').forEach(function (img) { URL.revokeObjectURL(img.src); });
  box.textContent = '';
}

function sayError(el, text) {
  el.textContent = text || '';
  el.hidden = !text;
}

// The box says which file it holds; with none, what it takes.
function nameBox(picker) {
  var title = picker.closest('.doc-drop').querySelector('.doc-drop__title');
  var file = picker.files[0];
  title.textContent = file ? file.name : title.dataset.idle;
  picker.closest('.doc-drop').classList.toggle('is-filled', !!file);
}

// ---- Clear -----------------------------------------------------------------
// Each tool's Clear: it can be pressed once something was picked, and puts the
// tool back as it was - no file, no result, no message.
function clearButton(button, pickers, reset) {
  button.addEventListener('click', function () {
    pickers.forEach(function (picker) { picker.value = ''; nameBox(picker); });
    reset();
    button.disabled = true;
    button.title = 'Nothing to clear yet';
  });
  pickers.forEach(function (picker) {
    picker.addEventListener('change', function () {
      var picked = pickers.some(function (p) { return p.files.length > 0; });
      button.disabled = !picked;
      button.title = picked ? '' : 'Nothing to clear yet';
    });
  });
}

// ---- Image to JPEG ---------------------------------------------------------
var imageFile = document.getElementById('utilImageFile');
var imageError = document.getElementById('utilImageError');
var imageResults = document.getElementById('utilImageResults');

clearButton(document.getElementById('utilImageClear'), [imageFile], function () { clear(imageResults); sayError(imageError, ''); });

imageFile.addEventListener('change', async function () {
  nameBox(imageFile);
  clear(imageResults);
  sayError(imageError, '');
  var file = imageFile.files[0];
  if (!file) return;
  var bitmap = await bitmapOf(file);
  if (!bitmap) return sayError(imageError, message('Utilities.NotAnImage'));
  var canvas = whiteCanvas(bitmap.width, bitmap.height);
  canvas.getContext('2d').drawImage(bitmap, 0, 0);
  bitmap.close();
  var made = await toJpeg(canvas);
  showResult(imageResults, made.blob, named(file, '.jpg'), made.width + ' × ' + made.height);
});

// ---- PDF to JPEG -----------------------------------------------------------
var pdfFile = document.getElementById('utilPdfFile');
var pdfError = document.getElementById('utilPdfError');
var pdfStatus = document.getElementById('utilPdfStatus');
var pdfResults = document.getElementById('utilPdfResults');
var pdfAll = document.getElementById('utilPdfAll');
var pageLinks = [];
// Points per inch of the PDF drawn at 150 dpi: a page readable as a document copy.
var PDF_SCALE = 150 / 72;

clearButton(document.getElementById('utilPdfClear'), [pdfFile], function () {
  clear(pdfResults); sayError(pdfError, ''); pdfStatus.hidden = true; pdfAll.parentElement.hidden = true; pageLinks = [];
});

pdfFile.addEventListener('change', async function () {
  nameBox(pdfFile);
  clear(pdfResults);
  sayError(pdfError, '');
  pdfAll.parentElement.hidden = true;
  pageLinks = [];
  var file = pdfFile.files[0];
  if (!file) return;
  var pdf;
  try {
    pdf = await pdfjs.getDocument({ data: await file.arrayBuffer() }).promise;
  } catch (e) {
    return sayError(pdfError, message('Utilities.NotAPdf'));
  }
  if (pdf.numPages === 0) return sayError(pdfError, message('Utilities.PdfHasNoPages'));
  pdfStatus.hidden = false;
  for (var n = 1; n <= pdf.numPages; n++) {
    pdfStatus.textContent = 'Drawing page ' + n + ' of ' + pdf.numPages + '…';
    var page = await pdf.getPage(n);
    var viewport = page.getViewport({ scale: PDF_SCALE });
    var canvas = whiteCanvas(Math.ceil(viewport.width), Math.ceil(viewport.height));
    await page.render({ canvasContext: canvas.getContext('2d'), viewport: viewport }).promise;
    var made = await toJpeg(canvas);
    var link = showResult(pdfResults, made.blob, named(file, '-page-' + n + '.jpg'), 'Page ' + n + ' of ' + pdf.numPages);
    pageLinks.push(link);
  }
  pdfStatus.textContent = pdf.numPages === 1 ? 'One page drawn.' : pdf.numPages + ' pages drawn.';
  pdfAll.parentElement.hidden = pdf.numPages < 2;
});

// Every page saved, one after another; the browser asks once whether this page may save several files.
pdfAll.addEventListener('click', function () {
  pageLinks.forEach(function (link, i) { setTimeout(function () { link.click(); }, i * 300); });
});

// ---- Merge two images ------------------------------------------------------
var mergeFirst = document.getElementById('utilMergeFirst');
var mergeSecond = document.getElementById('utilMergeSecond');
var mergeError = document.getElementById('utilMergeError');
var mergeResults = document.getElementById('utilMergeResults');
var mergeLayouts = Array.prototype.slice.call(document.querySelectorAll('[name="utilMergeLayout"]'));

async function merge() {
  nameBox(mergeFirst);
  nameBox(mergeSecond);
  clear(mergeResults);
  sayError(mergeError, '');
  var first = mergeFirst.files[0], second = mergeSecond.files[0];
  if (!first || !second) return;
  var a = await bitmapOf(first), b = await bitmapOf(second);
  if (!a || !b) return sayError(mergeError, message('Utilities.NotAnImage'));
  var beside = mergeLayouts.filter(function (r) { return r.checked; })[0].value === 'beside';
  var width = beside ? a.width + b.width : Math.max(a.width, b.width);
  var height = beside ? Math.max(a.height, b.height) : a.height + b.height;
  if (width > MAX_SIDE || height > MAX_SIDE) return sayError(mergeError, message('Utilities.TooLargeToMerge'));
  var canvas = whiteCanvas(width, height);
  var ctx = canvas.getContext('2d');
  ctx.drawImage(a, 0, 0);
  ctx.drawImage(b, beside ? a.width : 0, beside ? 0 : a.height);
  a.close(); b.close();
  var made = await toJpeg(canvas);
  showResult(mergeResults, made.blob, named(first, '-merged.jpg'), made.width + ' × ' + made.height);
}

clearButton(document.getElementById('utilMergeClear'), [mergeFirst, mergeSecond], function () { clear(mergeResults); sayError(mergeError, ''); });

mergeFirst.addEventListener('change', merge);
mergeSecond.addEventListener('change', merge);
mergeLayouts.forEach(function (r) { r.addEventListener('change', merge); });
