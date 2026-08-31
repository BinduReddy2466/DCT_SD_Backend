// Reports page: swaps in the filter fields for whichever Report Type is selected, and blocks
// Generate client-side if no Report Type was chosen. The actual Generate submit is a plain
// (non-AJAX) form POST - a normal browser download for the success case, and a redirect back
// here with a TempData toast for "No records found." (same pattern used elsewhere in the app).
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    var typeSelect = document.getElementById('reportType');
    var filtersContainer = document.getElementById('reportFiltersContainer');
    var errorEl = document.getElementById('reportTypeError');
    var form = document.getElementById('reportForm');
    if (!typeSelect || !filtersContainer || !form) return;

    typeSelect.addEventListener('change', function () {
      // Changing the report type always clears the previous report's filters, even while a
      // fetch for the new ones is still in flight.
      filtersContainer.innerHTML = '';
      if (errorEl) errorEl.classList.add('d-none');

      var reportType = typeSelect.value;
      if (!reportType) return;

      fetch('/Reports/Filters?reportType=' + encodeURIComponent(reportType), {
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
      })
        .then(function (r) { return r.text(); })
        .then(function (html) {
          if (typeSelect.value !== reportType) return; // selection changed again mid-fetch
          filtersContainer.innerHTML = html;
        });
    });

    form.addEventListener('submit', function (e) {
      if (!typeSelect.value) {
        e.preventDefault();
        if (errorEl) errorEl.classList.remove('d-none');
      }
    });
  });
})();
