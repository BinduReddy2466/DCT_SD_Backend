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

    function loadInto(url) {
      fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { return r.text(); })
        .then(function (html) {
          contentEl.innerHTML = html;
          bsModal.show();
        });
    }

    document.addEventListener('click', function (e) {
      var trigger = e.target.closest('[data-modal-url]');
      if (!trigger) return;
      e.preventDefault();
      loadInto(trigger.getAttribute('data-modal-url'));
    });

    contentEl.addEventListener('submit', function (e) {
      var form = e.target.closest('form');
      if (!form) return;
      e.preventDefault();

      var formData = new FormData(form);
      fetch(form.getAttribute('action') || window.location.href, {
        method: form.getAttribute('method') || 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
      }).then(function (response) {
        var contentType = response.headers.get('content-type') || '';
        if (contentType.indexOf('application/json') !== -1) {
          return response.json().then(function (data) {
            bsModal.hide();
            if (data.message && window.showToast) {
              window.showToast(data.message, data.toastVariant || 'success');
            }
            window.location.reload();
          });
        }
        return response.text().then(function (html) {
          contentEl.innerHTML = html;
        });
      });
    });
  });
})();
