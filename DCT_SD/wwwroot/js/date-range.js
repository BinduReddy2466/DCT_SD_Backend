// Shared Date From / Date To dependency rule, applied generically to every date-range pair in
// the app - no per-page wiring needed. A pair is any <form> containing exactly two
// <input type="date"> elements in From-then-To DOM order (every screen already renders them
// that way: Fetch History, Root Source Path Update History, Manual Validation, Empty Folders,
// Failed Extraction, Migration Monitoring, User Management, Reports).
//
// Selecting Date From sets Date To's `min` (the browser's native date picker then disables/
// greys out anything earlier); selecting Date To sets Date From's `max` the same way. The same
// date is always valid (min == max is not an empty range). Neither field's chosen VALUE is ever
// silently overwritten - only the allowed range changes, so native HTML5 validation covers a
// value that becomes stale without this script rewriting anything the user picked.
//
// Works for both static forms (present at page load) and forms whose date fields are injected
// later via AJAX (Reports' filter fields swap inside the one #reportForm when Report Type
// changes) via a MutationObserver - each pair is (re)wired the moment it appears, keyed off the
// input elements themselves rather than the form, since the form itself is often not replaced,
// only its contents are.
(function () {
  'use strict';

  function wirePair(fromInput, toInput) {
    function syncMinMax() {
      if (fromInput.value) {
        toInput.min = fromInput.value;
      } else {
        toInput.removeAttribute('min');
      }
      if (toInput.value) {
        fromInput.max = toInput.value;
      } else {
        fromInput.removeAttribute('max');
      }
    }

    fromInput.addEventListener('change', syncMinMax);
    toInput.addEventListener('change', syncMinMax);

    // form.reset() (used by every page's Clear button) resets values without firing 'change' on
    // the fields it resets, so the min/max this script set would otherwise survive a Clear and
    // incorrectly restrict the very next selection.
    var form = fromInput.form;
    if (form) {
      form.addEventListener('reset', function () {
        toInput.removeAttribute('min');
        fromInput.removeAttribute('max');
      });
    }

    syncMinMax();
  }

  function wireForm(form) {
    var dateInputs = form.querySelectorAll('input[type="date"]');
    if (dateInputs.length !== 2) return;
    var fromInput = dateInputs[0];
    var toInput = dateInputs[1];
    if (fromInput.dataset.dateRangeWired === 'true') return;
    fromInput.dataset.dateRangeWired = 'true';
    toInput.dataset.dateRangeWired = 'true';
    wirePair(fromInput, toInput);
  }

  function scan(node) {
    if (!node || node.nodeType !== 1) return;
    if (node.tagName === 'FORM') {
      wireForm(node);
      return;
    }
    if (node.querySelectorAll) {
      node.querySelectorAll('form').forEach(wireForm);
    }
    if (node.closest) {
      var enclosingForm = node.closest('form');
      if (enclosingForm) wireForm(enclosingForm);
    }
  }

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('form').forEach(wireForm);

    var observer = new MutationObserver(function (mutations) {
      mutations.forEach(function (mutation) {
        mutation.addedNodes.forEach(scan);
      });
    });
    observer.observe(document.body, { childList: true, subtree: true });
  });
})();
