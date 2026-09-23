// The classic login page: the User ID and Password pair, and the OTP dialog the
// site opens once that pair checks out. Nothing is authenticated - the pair and
// the OTP the page accepts are rendered into the form by the server and checked
// here, and signing in only takes the browser to the console.
(function () {
  var form = document.getElementById('cloginForm');
  if (!form) return;

  var user = document.getElementById('cloginUser');
  var pass = document.getElementById('cloginPass');
  var error = document.getElementById('cloginError');
  var submit = document.getElementById('cloginSubmit');

  var modal = document.getElementById('cloginOtpModal');
  var boxes = Array.prototype.slice.call(modal.querySelectorAll('[data-otp-box]'));
  var otpError = document.getElementById('cotpError');
  var resend = document.getElementById('cotpResend');
  var countdown = document.getElementById('cotpCountdown');
  var verify = document.getElementById('cotpVerify');
  var cancel = document.getElementById('cotpCancel');
  var close = document.getElementById('cotpClose');

  var EXPECTED = {
    user: form.dataset.userId,
    pass: form.dataset.password,
    otp: form.dataset.otp
  };
  var RESEND_AFTER = 30;
  var ATTEMPTS = 3;

  var left = ATTEMPTS;
  var timer = null;

  // ---- The toast, and the controls that raise one ---------------------------
  // The page does not carry console.js, so it keeps its own copy of the two
  // things it needs from it.
  var toastEl = document.getElementById('toast');
  var toastTimer = null;
  function showToast(message) {
    if (!toastEl) return;
    toastEl.textContent = message;
    toastEl.classList.add('toast--visible');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove('toast--visible'); }, 2200);
  }
  document.querySelectorAll('.js-stub').forEach(function (el) {
    el.addEventListener('click', function () { showToast('Not wired up in this mock yet.'); });
  });
  // "Login" in the bar is where you already are, so it goes to the first field.
  var focusLogin = document.querySelector('[data-focus-login]');
  if (focusLogin) focusLogin.addEventListener('click', function () { user.focus(); });

  // ---- The pair -------------------------------------------------------------
  function fail(message, field) {
    error.textContent = message;
    error.hidden = false;
    if (field) { field.classList.add('clogin__input--bad'); field.focus(); }
  }
  function clearFail() {
    error.hidden = true;
    error.textContent = '';
    user.classList.remove('clogin__input--bad');
    pass.classList.remove('clogin__input--bad');
  }
  [user, pass].forEach(function (f) { f.addEventListener('input', clearFail); });

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    clearFail();
    var id = user.value.trim();
    if (!id) return fail('Please enter your User ID.', user);
    if (!pass.value) return fail('Please enter your Password.', pass);
    // The live site says no more than this about which of the two was wrong.
    if (id !== EXPECTED.user || pass.value !== EXPECTED.pass) {
      pass.value = '';
      return fail('Invalid User ID or Password.', user);
    }
    openOtp();
  });

  // ---- The OTP dialog -------------------------------------------------------
  function openOtp() {
    left = ATTEMPTS;
    modal.hidden = false;
    document.body.style.overflow = 'hidden';
    resetBoxes();
    startCountdown();
    boxes[0].focus();
  }
  function closeOtp(message) {
    stopCountdown();
    modal.hidden = true;
    document.body.style.overflow = '';
    if (message) fail(message, user); else user.focus();
  }

  function resetBoxes() {
    boxes.forEach(function (b) { b.value = ''; b.classList.remove('cotp__box--bad'); });
    otpError.hidden = true;
    otpError.textContent = '';
    verify.disabled = true;
  }
  function typed() {
    return boxes.map(function (b) { return b.value; }).join('');
  }
  function sync() {
    verify.disabled = typed().length !== boxes.length;
  }

  boxes.forEach(function (box, i) {
    box.addEventListener('input', function () {
      // A box holds one digit; anything else is dropped as it is typed.
      box.value = box.value.replace(/\D/g, '').slice(0, 1);
      box.classList.remove('cotp__box--bad');
      otpError.hidden = true;
      if (box.value && i < boxes.length - 1) boxes[i + 1].focus();
      sync();
    });
    box.addEventListener('keydown', function (e) {
      if (e.key === 'Backspace' && !box.value && i > 0) {
        e.preventDefault();
        boxes[i - 1].value = '';
        boxes[i - 1].focus();
        sync();
      } else if (e.key === 'ArrowLeft' && i > 0) {
        e.preventDefault(); boxes[i - 1].focus();
      } else if (e.key === 'ArrowRight' && i < boxes.length - 1) {
        e.preventDefault(); boxes[i + 1].focus();
      } else if (e.key === 'Enter' && !verify.disabled) {
        e.preventDefault(); check();
      }
    });
    // One paste of the whole code fills the row, wherever it is dropped.
    box.addEventListener('paste', function (e) {
      var text = (e.clipboardData || window.clipboardData).getData('text').replace(/\D/g, '');
      if (!text) return;
      e.preventDefault();
      for (var n = 0; n < text.length && i + n < boxes.length; n++) boxes[i + n].value = text[n];
      boxes[Math.min(i + text.length, boxes.length - 1)].focus();
      otpError.hidden = true;
      sync();
    });
  });

  function check() {
    if (typed() === EXPECTED.otp) {
      stopCountdown();
      verify.disabled = true;
      verify.textContent = 'Signing in…';
      // Signed in: the console, which is where the live site drops a partner.
      window.location.href = '/Classic';
      return;
    }
    left--;
    if (left <= 0) {
      closeOtp('Too many incorrect attempts. Sign in again for a fresh OTP.');
      return;
    }
    // The row is emptied to be typed again, and carries the red until it is.
    boxes.forEach(function (b) { b.value = ''; b.classList.add('cotp__box--bad'); });
    verify.disabled = true;
    otpError.textContent = 'Incorrect OTP. ' + left + (left === 1 ? ' attempt' : ' attempts') + ' left.';
    otpError.hidden = false;
    boxes[0].focus();
  }
  verify.addEventListener('click', check);

  // ---- Asking for another code ---------------------------------------------
  function startCountdown() {
    var seconds = RESEND_AFTER;
    resend.innerHTML = 'Resend OTP in <span id="cotpCountdown">00:30</span>';
    countdown = document.getElementById('cotpCountdown');
    stopCountdown();
    timer = setInterval(function () {
      seconds--;
      if (seconds <= 0) {
        stopCountdown();
        resend.innerHTML = '';
        var again = document.createElement('button');
        again.type = 'button';
        again.className = 'btn-reset cotp__again';
        again.textContent = 'Resend OTP';
        again.addEventListener('click', function () {
          resetBoxes();
          startCountdown();
          boxes[0].focus();
          showToast('A fresh OTP has been sent.');
        });
        resend.appendChild(again);
        return;
      }
      countdown.textContent = '00:' + (seconds < 10 ? '0' : '') + seconds;
    }, 1000);
  }
  function stopCountdown() {
    if (timer) { clearInterval(timer); timer = null; }
  }

  cancel.addEventListener('click', function () { closeOtp(); });
  close.addEventListener('click', function () { closeOtp(); });
  modal.addEventListener('click', function (e) { if (e.target === modal) closeOtp(); });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !modal.hidden) closeOtp();
  });
})();
