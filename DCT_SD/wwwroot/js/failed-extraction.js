// Keeps the Failed Extraction date-range filter mutually consistent: Date To can never be set
// earlier than the current Date From, and Date From can never be set later than the current
// Date To - each field's min/max is kept in sync with the other's current value.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    var dateFrom = document.getElementById('DateFrom');
    var dateTo = document.getElementById('DateTo');
    if (!dateFrom || !dateTo) return;

    function sync() {
      dateTo.min = dateFrom.value || '';
      dateFrom.max = dateTo.value || '';
    }

    dateFrom.addEventListener('change', sync);
    dateTo.addEventListener('change', sync);
    sync();
  });

  // --- Reprocess ---
  // Reprocesses ONLY the one Entry Folder for a given row (never the full Fetch process).
  // Delegated on the results container (not bound per-button) so it keeps working after
  // list-page.js swaps the container's innerHTML on every search/pagination/Reprocess refresh.
  document.addEventListener('DOMContentLoaded', function () {
    var resultsContainer = document.getElementById('failed-extraction-results');
    if (!resultsContainer) return;

    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';
    var inFlight = {};

    function refreshResults() {
      var form = document.querySelector('[data-list-page-form="failed-extraction-results"]');
      if (form && form.requestSubmit) form.requestSubmit();
    }

    resultsContainer.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-reprocess-btn]');
      if (!btn) return;

      var id = btn.getAttribute('data-id');
      if (!id || inFlight[id]) return;
      inFlight[id] = true;

      btn.disabled = true;
      btn.textContent = 'Reprocessing...';

      var body = new URLSearchParams({ id: id, __RequestVerificationToken: token });
      fetch('/FailedExtraction/Reprocess', { method: 'POST', body: body })
        .then(function (r) { return r.json(); })
        .then(function (data) {
          if (window.showToast) window.showToast(data.message, data.success ? 'success' : 'error');
        })
        .catch(function () {
          if (window.showToast) window.showToast('Could not reach the server. Please try again.', 'error');
        })
        .finally(function () {
          delete inFlight[id];
          // The results refresh replaces this row's markup entirely (removed on success,
          // rebuilt with the latest values on failure) - no need to manually re-enable this
          // specific button.
          refreshResults();
        });
    });
  });
})();
