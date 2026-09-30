// Review Summary: Submit holds until nothing is missing and every declaration is
// signed, and opens the payment link dialog. Whether anything is missing is the
// server's, on the form as data-ready.
(function () {
    var form = document.getElementById('reviewForm');
    var boxes = form.querySelectorAll('.review-declaration__box');
    var modal = document.getElementById('paymentLinkModal');
    var submit = document.getElementById('openPaymentLinkModal');
    var closeBtn = document.getElementById('closePaymentLinkModal');
    var ready = form.getAttribute('data-ready') === 'true';

    // Submit holds until nothing is missing and every declaration is signed.
    function enableSubmitWhenReady() {
        var unsigned = 0;
        boxes.forEach(function (box) {
            box.closest('.review-declaration').classList.toggle('review-declaration--unsigned', !box.checked);
            if (!box.checked) unsigned++;
        });
        submit.disabled = !ready || unsigned > 0;
    }
    // Opens the payment link dialog.
    function openPaymentLinkDialog() { modal.hidden = false; document.body.style.overflow = 'hidden'; closeBtn.focus(); }
    // Closes the payment link dialog and returns focus to Submit.
    function closePaymentLinkDialog() { modal.hidden = true; document.body.style.overflow = ''; submit.focus(); }
    form.addEventListener('change', enableSubmitWhenReady);
    submit.addEventListener('click', function () { if (!submit.disabled) openPaymentLinkDialog(); });
    closeBtn.addEventListener('click', closePaymentLinkDialog);
    modal.addEventListener('click', function (e) { if (e.target === modal) closePaymentLinkDialog(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !modal.hidden) closePaymentLinkDialog(); });
    enableSubmitWhenReady();
})();
