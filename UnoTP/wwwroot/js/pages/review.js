// Review Summary: Submit holds until nothing is missing, and opens the payment
// link dialog. Whether anything is missing is the server's, on the form as data-ready.
// Both of the dialog's buttons submit the form; only its cross closes it unsubmitted.
(function () {
    var form = document.getElementById('reviewForm');
    var modal = document.getElementById('paymentLinkModal');
    var submit = document.getElementById('openPaymentLinkModal');
    var closeBtn = document.getElementById('closePaymentLinkModal');

    submit.disabled = form.getAttribute('data-ready') !== 'true';

    // Opens the payment link dialog.
    function openPaymentLinkDialog() { modal.hidden = false; document.body.style.overflow = 'hidden'; modal.querySelector('.pay-link-actions__send').focus(); }
    // Closes the payment link dialog and returns focus to Submit.
    function closePaymentLinkDialog() { modal.hidden = true; document.body.style.overflow = ''; submit.focus(); }
    submit.addEventListener('click', function () { if (!submit.disabled) openPaymentLinkDialog(); });
    closeBtn.addEventListener('click', closePaymentLinkDialog);
    modal.addEventListener('click', function (e) { if (e.target === modal) closePaymentLinkDialog(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !modal.hidden) closePaymentLinkDialog(); });
})();
