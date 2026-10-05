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

    // Clear button fix: Index.cshtml echoes the current search's query-string values back into
    // each filter field's value="..." attribute, so a reload/shared link keeps showing what was
    // last searched. That also becomes each field's HTML "default" value though, and
    // list-page.js's shared Clear handler just calls form.reset() - which restores every field to
    // its HTML-parsed default, i.e. right back to whatever was last searched, not blank, whenever
    // the page had been loaded/reloaded with search criteria already in the URL (which is exactly
    // what happens after a normal search, since that same list-page.js pushes the query string
    // into the address bar). Resetting each field's defaultValue/defaultSelected once here - without
    // touching list-page.js itself, which many other pages share - makes form.reset() naturally
    // land on blank from here on, same as a freshly-loaded page with no criteria.
    if (form) {
      var entryNumberField = document.getElementById('EntryNumbersCsv');
      var dateFromField = document.getElementById('DateFrom');
      var dateToField = document.getElementById('DateTo');
      var rdCodeField = document.getElementById('RdCode');

      if (entryNumberField) entryNumberField.defaultValue = '';
      if (dateFromField) dateFromField.defaultValue = '';
      if (dateToField) dateToField.defaultValue = '';
      if (rdCodeField) {
        Array.prototype.forEach.call(rdCodeField.options, function (opt) {
          opt.defaultSelected = opt.value === '';
        });
      }
    }
  });
})();
