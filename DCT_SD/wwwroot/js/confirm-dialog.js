// Any <form data-confirm-message="..."> is intercepted on submit: shows the shared
// #confirmDialog modal, and only actually submits (via native form submission, so
// antiforgery tokens/etc. work exactly as normal) once the user confirms. Optional
// data-confirm-danger (red confirm button) and data-confirm-label (button text) attributes.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    var modalEl = document.getElementById('confirmDialog');
    if (!modalEl || !window.bootstrap) return;

    var bsModal = new bootstrap.Modal(modalEl);
    var messageEl = document.getElementById('confirmDialogMessage');
    var confirmBtn = document.getElementById('confirmDialogConfirmBtn');
    var cancelBtn = document.getElementById('confirmDialogCancelBtn');
    var pendingForm = null;

    document.addEventListener('submit', function (e) {
      var form = e.target;
      if (!form || !form.hasAttribute('data-confirm-message')) return;
      if (form.dataset.confirmed === 'true') return;

      e.preventDefault();
      messageEl.textContent = form.getAttribute('data-confirm-message');
      confirmBtn.className = 'btn ' + (form.hasAttribute('data-confirm-danger') ? 'btn-danger' : 'btn-navy');
      confirmBtn.textContent = form.getAttribute('data-confirm-label') || 'Confirm';
      if (cancelBtn) cancelBtn.textContent = form.getAttribute('data-confirm-cancel-label') || 'Cancel';
      pendingForm = form;
      bsModal.show();
    });

    confirmBtn.addEventListener('click', function () {
      bsModal.hide();
      if (pendingForm) {
        pendingForm.dataset.confirmed = 'true';
        if (pendingForm.requestSubmit) {
          pendingForm.requestSubmit();
        } else {
          pendingForm.submit();
        }
        pendingForm = null;
      }
    });
  });
})();
