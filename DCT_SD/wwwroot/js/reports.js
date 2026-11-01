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
    var searchBtn = document.getElementById('reportSearchBtn');
    var clearBtn = document.getElementById('reportClearBtn');
    if (!typeSelect || !filtersContainer || !form || !resultsContainer || !generateBtn || !generateForm) return;

    function loadResults() {
      var params = new URLSearchParams(new FormData(form));
      var url = form.getAttribute('action') + '?' + params.toString();
      resultsContainer.setAttribute('aria-busy', 'true');
      fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) {
          if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
          if (!r.ok) {
            if (window.showToast) window.showToast('Could not load results. Please try again.', 'error');
            return null;
          }
          return r.text();
        })
        .then(function (html) { if (html !== null) resultsContainer.innerHTML = html; })
        .catch(function () {
          // leave whatever was already shown rather than blanking it, but say so this time -
          // silently doing nothing here reads exactly like the page froze.
          if (window.showToast) window.showToast('Could not load results. Please check your connection and try again.', 'error');
        })
        .finally(function () { resultsContainer.removeAttribute('aria-busy'); });
    }

    function loadForType(reportType) {
      filtersContainer.innerHTML = '';
      resultsContainer.innerHTML = '';
      if (errorEl) errorEl.classList.add('d-none');
      // Hidden while no Report Type is selected (nothing to search/clear yet); shown again as
      // soon as one is - Search and Clear themselves are unchanged otherwise.
      if (searchBtn) searchBtn.classList.toggle('d-none', !reportType);
      if (clearBtn) clearBtn.classList.toggle('d-none', !reportType);
      if (!reportType) return;

      filtersContainer.setAttribute('aria-busy', 'true');
      fetch('/Reports/Filters?reportType=' + encodeURIComponent(reportType), {
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
      })
        .then(function (r) {
          if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
          if (!r.ok) {
            if (window.showToast) window.showToast('Could not load the filters for this report type. Please try again.', 'error');
            return null;
          }
          return r.text();
        })
        .then(function (html) {
          if (html === null || typeSelect.value !== reportType) return; // selection changed again mid-fetch, or request failed
          filtersContainer.innerHTML = html;
          loadResults();
        })
        .catch(function () {
          if (window.showToast) window.showToast('Could not load the filters for this report type. Please check your connection and try again.', 'error');
        })
        .finally(function () { filtersContainer.removeAttribute('aria-busy'); });
    }

    typeSelect.addEventListener('change', function () {
      loadForType(typeSelect.value);
    });

    // Clears only this report type's own filter field values (the "row" of inputs rendered into
    // #reportFiltersContainer) and refreshes the table - Report Type itself is left alone, same
    // as every other list page's Clear only resets its own filter fields, never the page/view
    // you're on.
    if (clearBtn) {
      clearBtn.addEventListener('click', function () {
        filtersContainer.querySelectorAll('input, select').forEach(function (el) {
          if (el.tagName === 'SELECT') {
            el.selectedIndex = 0;
          } else {
            el.value = '';
          }
        });
        loadResults();
      });
    }

    // Browsers can restore a <select>'s value on a plain reload/back-forward navigation
    // (the option shows as selected) without ever firing a 'change' event - which would leave
    // this page showing a report type with an empty table under it, since nothing told the JS
    // to actually go fetch that type's filters/results. If the type is already set when this
    // script runs, treat it exactly as if the user had just picked it.
    if (typeSelect.value) {
      loadForType(typeSelect.value);
    }

    generateBtn.addEventListener('click', function () {
      if (generateBtn.disabled) return;
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

      // This submit is a real top-level form POST (needed so the browser treats the response as
      // a file download), so there's no fetch promise to key a "done" state off of - on success
      // the browser stays on this same page (no navigation happens for a file-attachment
      // response), and on failure the server redirects back to Index, which reloads the page and
      // resets this button along with everything else. The only gap that leaves is the success
      // case, so re-enable after a generous fixed delay rather than leaving it disabled forever;
      // this also stops a double-click from firing two concurrent report generations, each a
      // multi-second, single-threaded Excel build on the server.
      var originalLabel = generateBtn.textContent;
      generateBtn.disabled = true;
      generateBtn.textContent = 'Generating...';
      setTimeout(function () {
        generateBtn.disabled = false;
        generateBtn.textContent = originalLabel;
      }, 15000);

      generateForm.submit();
    });
  });
})();
