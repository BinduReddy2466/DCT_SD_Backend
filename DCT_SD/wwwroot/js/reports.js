// Reports page. Search + pagination on #reportForm/#reportResults is handled entirely by the
// generic list-page.js (data-list-page-form="reportResults") - untouched here. This file only
// adds the two things specific to Reports:
//   1. Changing Report Type clears the previous filters and table, loads the new report's
//      filter fields, then immediately loads its (unfiltered) results - no separate click needed
//      to see the initial record set.
//   2. Generate Report validates a Report Type is selected, then mirrors the current filter
//      form's values into a hidden POST form and submits it - a real form POST is what makes
//      the browser treat the response as a file download, which fetch()/AJAX can't trigger.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    var typeSelect = document.getElementById('reportType');
    var filtersContainer = document.getElementById('reportFiltersContainer');
    var errorEl = document.getElementById('reportTypeError');
    var form = document.getElementById('reportForm');
    var resultsContainer = document.getElementById('reportResults');
    var generateBtn = document.getElementById('generateReportBtn');
    var generateForm = document.getElementById('reportGenerateForm');
    if (!typeSelect || !filtersContainer || !form || !resultsContainer || !generateBtn || !generateForm) return;

    function loadResults() {
      var params = new URLSearchParams(new FormData(form));
      var url = form.getAttribute('action') + '?' + params.toString();
      resultsContainer.setAttribute('aria-busy', 'true');
      fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { return r.text(); })
        .then(function (html) { resultsContainer.innerHTML = html; })
        .catch(function () { /* leave whatever was already shown rather than blanking it */ })
        .finally(function () { resultsContainer.removeAttribute('aria-busy'); });
    }

    function loadForType(reportType) {
      filtersContainer.innerHTML = '';
      resultsContainer.innerHTML = '';
      if (errorEl) errorEl.classList.add('d-none');
      if (!reportType) return;

      fetch('/Reports/Filters?reportType=' + encodeURIComponent(reportType), {
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
      })
        .then(function (r) { return r.text(); })
        .then(function (html) {
          if (typeSelect.value !== reportType) return; // selection changed again mid-fetch
          filtersContainer.innerHTML = html;
          loadResults();
        });
    }

    typeSelect.addEventListener('change', function () {
      loadForType(typeSelect.value);
    });

    // Browsers can restore a <select>'s value on a plain reload/back-forward navigation
    // (the option shows as selected) without ever firing a 'change' event - which would leave
    // this page showing a report type with an empty table under it, since nothing told the JS
    // to actually go fetch that type's filters/results. If the type is already set when this
    // script runs, treat it exactly as if the user had just picked it.
    if (typeSelect.value) {
      loadForType(typeSelect.value);
    }

    generateBtn.addEventListener('click', function () {
      if (!typeSelect.value) {
        if (errorEl) errorEl.classList.remove('d-none');
        return;
      }
      if (errorEl) errorEl.classList.add('d-none');

      // Only the previous round's dynamic fields are cleared - the antiforgery token that the
      // asp-action tag helper injected into this form on page load is left alone.
      generateForm.querySelectorAll('[data-dynamic-field]').forEach(function (el) { el.remove(); });

      new FormData(form).forEach(function (value, key) {
        var hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.name = key;
        hidden.value = value;
        hidden.setAttribute('data-dynamic-field', '');
        generateForm.appendChild(hidden);
      });

      generateForm.submit();
    });
  });
})();
