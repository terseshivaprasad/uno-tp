// A large photo is made smaller before it is uploaded. A phone camera's JPEG runs
// to 2-4 MB, which on the slow connections most partners work over takes half a
// minute to send; drawn again at no more than the box's data-shrink pixels on its
// long side (2560) it is a third to a quarter of that, and keeps the detail OCR and
// the face match read. Only a box that asks for it, and only a JPEG over 1 MB: the
// application form, a PDF, or a photo already small goes exactly as picked. The
// copy made loses the photo's EXIF data - camera, date taken - which a check that
// looks for editing may want; that is why smaller photos are left alone. Without
// canvas support, or if anything fails, the file the partner picked goes untouched.
//
// It runs before partial-forms.js sees the file: the change is held, the file in
// the box replaced with the smaller one, and the change let through again.
(function () {
  var QUALITY = 0.9;
  var WORTH_IT = 1024 * 1024;
  if (!window.DataTransfer || !HTMLCanvasElement.prototype.toBlob) return;

  document.addEventListener('change', function (e) {
    var input = e.target;
    if (!input || input.type !== 'file' || !input.files || input.files.length !== 1) return;
    if (input.dataset.shrunk === '1') { delete input.dataset.shrunk; return; }
    var longEdge = parseInt(input.dataset.shrink, 10);
    var file = input.files[0];
    if (!longEdge || !/^image\/jpe?g$/i.test(file.type) || file.size < WORTH_IT) return;

    // Held: the change goes on once the smaller copy is in the box.
    e.stopImmediatePropagation();
    shrink(file, longEdge).then(function (smaller) {
      if (smaller && smaller.size < file.size) {
        var box = new DataTransfer();
        box.items.add(smaller);
        input.files = box.files;
      }
    }).catch(function () { /* the original goes */ }).then(function () {
      input.dataset.shrunk = '1';
      input.dispatchEvent(new Event('change', { bubbles: true }));
    });
  }, true);

  function shrink(file, longEdge) {
    return decode(file).then(function (image) {
      var width = image.naturalWidth || image.width, height = image.naturalHeight || image.height;
      var scale = Math.min(1, longEdge / Math.max(width, height));
      // Already within the size: sent as picked, not compressed a second time.
      if (scale === 1) { if (image.close) image.close(); return null; }
      var canvas = document.createElement('canvas');
      canvas.width = Math.round(width * scale);
      canvas.height = Math.round(height * scale);
      canvas.getContext('2d').drawImage(image, 0, 0, canvas.width, canvas.height);
      if (image.close) image.close();
      return new Promise(function (resolve) {
        canvas.toBlob(function (blob) {
          resolve(blob ? new File([blob], file.name, { type: 'image/jpeg', lastModified: file.lastModified }) : null);
        }, 'image/jpeg', QUALITY);
      });
    });
  }

  // Upright as the camera meant it: both ways apply the photo's own orientation.
  function decode(file) {
    if (window.createImageBitmap) {
      return createImageBitmap(file, { imageOrientation: 'from-image' }).catch(function () { return viaImage(file); });
    }
    return viaImage(file);
  }

  function viaImage(file) {
    return new Promise(function (resolve, reject) {
      var url = URL.createObjectURL(file);
      var img = new Image();
      img.onload = function () { URL.revokeObjectURL(url); resolve(img); };
      img.onerror = function () { URL.revokeObjectURL(url); reject(new Error('decode')); };
      img.src = url;
    });
  }
})();
