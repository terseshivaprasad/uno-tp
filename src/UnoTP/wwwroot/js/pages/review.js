// Review Summary: Submit holds until nothing is missing and every declaration is
// signed, and opens the payment link dialog. Whether anything is missing is the
// server's, on the form as data-ready.
(function () {
    var form = document.getElementById('reviewForm');
    var boxes = form.querySelectorAll('.rv-declaration__box');
    var modal = document.getElementById('paymentLinkModal');
    var submit = document.getElementById('openPaymentLinkModal');
    var closeBtn = document.getElementById('closePaymentLinkModal');
    var ready = form.getAttribute('data-ready') === 'true';

    // Submit holds until nothing is missing and every declaration is signed.
    function sync() {
        var unsigned = 0;
        boxes.forEach(function (box) {
            box.closest('.rv-declaration').classList.toggle('rv-declaration--unsigned', !box.checked);
            if (!box.checked) unsigned++;
        });
        submit.disabled = !ready || unsigned > 0;
    }
    function openDialog() { modal.hidden = false; document.body.style.overflow = 'hidden'; closeBtn.focus(); }
    function closeDialog() { modal.hidden = true; document.body.style.overflow = ''; submit.focus(); }
    form.addEventListener('change', sync);
    submit.addEventListener('click', function () { if (!submit.disabled) openDialog(); });
    closeBtn.addEventListener('click', closeDialog);
    modal.addEventListener('click', function (e) { if (e.target === modal) closeDialog(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !modal.hidden) closeDialog(); });
    sync();
})();
