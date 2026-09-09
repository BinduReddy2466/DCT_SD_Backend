// Wires the Manual Validation list page's search form validation.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    // Entry Number is a mandatory search criterion (unlike Registry of Deeds/Date From/Date To,
    // which stay optional) - block Search until it's provided, without touching list-page.js
    // (shared by every other list/search screen). Registered on document with capture:true so
    // this runs - and can veto the event via stopPropagation() - before list-page.js's own
    // submit listener on this same form fires, regardless of which script tag happens to load
    // first. The Clear button is unaffected: list-page.js's handler for it calls submitForm()
    // directly, never through a "submit" event.
    var form = document.querySelector('[data-list-page-form="manual-validation-results"]');
    if (form) {
      document.addEventListener('submit', function (e) {
        if (e.target !== form) return;

        var entryNumber = document.getElementById('EntryNumbersCsv');
        if (!entryNumber || !entryNumber.value.trim()) {
          e.preventDefault();
          e.stopPropagation();
          if (window.showToast) window.showToast('Entry Number is required.', 'default');
        }
      }, true);
    }
  });
})();
