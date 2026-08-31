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
})();
