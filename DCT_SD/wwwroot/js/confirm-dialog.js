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

    // A cancel/backdrop-dismiss/Esc never reaches the confirmBtn handler above, so without this,
    // pendingForm stayed set to this form indefinitely. Anything else that later reuses this same
    // shared #confirmDialog/#confirmDialogConfirmBtn outside this form-intercept mechanism (e.g.
    // RD Configuration's own Start Fetching confirm, which wires its own click listener directly
    // onto confirmBtn) would then have its own "Yes" click also fire this stale pendingForm's
    // submit - silently re-submitting an unrelated, already-dismissed form alongside whatever that
    // other "Yes" was actually supposed to do. Already a no-op on a real confirm: pendingForm is
    // set back to null synchronously above, before this (async) event ever fires.
    modalEl.addEventListener('hidden.bs.modal', function () {
      pendingForm = null;
    });
  });
})();
