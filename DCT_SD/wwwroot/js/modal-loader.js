// Generic AJAX-loaded Bootstrap modal. Any element with data-modal-url fetches that URL
// (expected to return a PartialView containing .modal-header/.modal-body/.modal-footer
// markup) into #ajaxModalContent and shows it. Forms submitted from inside that content post
// via fetch: a JSON response `{ success: true, message }` closes the modal, toasts the
// message, and reloads the page (simplest reliable way to refresh whatever list is behind
// it); any other response body is treated as re-rendered HTML (validation errors) and swapped
// back into the modal content in place.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    var modalEl = document.getElementById('ajaxModal');
    var contentEl = document.getElementById('ajaxModalContent');
    if (!modalEl || !contentEl || !window.bootstrap) return;

    var bsModal = new bootstrap.Modal(modalEl);

    function loadInto(url, trigger) {
      // Reuses the same [aria-busy="true"] dim+spinner+pointer-events:none treatment every
      // list-page.js results container already uses (site.css) - without this, there was
      // nothing at all on screen between clicking (e.g. View/Browse) and the modal appearing,
      // which for any slow response looks indistinguishable from the page having frozen.
      if (trigger) trigger.setAttribute('aria-busy', 'true');

      fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, cache: 'no-store' })
        .then(function (r) {
          if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
          if (!r.ok) {
            if (window.showToast) window.showToast('Could not load this. Please try again.', 'error');
            return null;
          }
          return r.text();
        })
        .then(function (html) {
          if (html === null) return;
          contentEl.innerHTML = html;
          bsModal.show();
        })
        .catch(function () {
          if (window.showToast) window.showToast('Could not load this. Please check your connection and try again.', 'error');
        })
        .finally(function () {
          if (trigger) trigger.removeAttribute('aria-busy');
        });
    }

    document.addEventListener('click', function (e) {
      var trigger = e.target.closest('[data-modal-url]');
      if (!trigger || trigger.hasAttribute('aria-busy')) return;
      e.preventDefault();
      loadInto(trigger.getAttribute('data-modal-url'), trigger);
    });

    contentEl.addEventListener('submit', function (e) {
      var form = e.target.closest('form');
      if (!form || form.dataset.submitting === 'true') return;
      e.preventDefault();

      // Prevents a double-click on Save/Create from firing two concurrent submits - re-cleared
      // in `finally` below, including on failure, so a genuinely failed submit can be retried.
      form.dataset.submitting = 'true';
      var submitBtn = form.querySelector('[type="submit"]');
      if (submitBtn) submitBtn.disabled = true;

      var formData = new FormData(form);
      fetch(form.getAttribute('action') || window.location.href, {
        method: form.getAttribute('method') || 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
      }).then(function (response) {
        if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(response)) return null;
        if (!response.ok) {
          if (window.showToast) window.showToast('Could not save changes. Please try again.', 'error');
          return null;
        }

        var contentType = response.headers.get('content-type') || '';
        if (contentType.indexOf('application/json') !== -1) {
          return response.json().then(function (data) {
            bsModal.hide();
            // location.reload() below navigates away immediately - calling showToast() here
            // would create the toast only for it to be destroyed before it ever paints. Stash
            // it in sessionStorage instead; toast.js shows it once the new page loads.
            if (data.message) {
              sessionStorage.setItem('pendingToast', JSON.stringify({
                message: data.message,
                variant: data.toastVariant || 'success',
              }));
            }
            window.location.reload();
          });
        }
        return response.text().then(function (html) {
          contentEl.innerHTML = html;
          // Item 15 of the Assign Tab spec calls for this specific message with an OK button,
          // not the inline validation-summary text every other form error already uses here -
          // native alert() is the OK-button affordance, scoped to this one message only.
          var summary = contentEl.querySelector('#formValidationSummary');
          if (summary && /please assign a tab to the user before proceeding/i.test(summary.textContent)) {
            window.alert(summary.textContent.trim());
          }
        });
      }).catch(function () {
        if (window.showToast) window.showToast('Could not save changes. Please check your connection and try again.', 'error');
      }).finally(function () {
        form.dataset.submitting = 'false';
        if (submitBtn) submitBtn.disabled = false;
      });
    });
  });
})();
