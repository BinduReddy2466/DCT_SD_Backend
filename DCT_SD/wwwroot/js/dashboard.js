// Wires the Dashboard page's RD Code filter (searchable combobox, same shared widget used
// elsewhere in this app) and the statistics table's periodic live-refresh (no SignalR/WebSockets
// anywhere in this app - plain polling is the only option, matching the spec's "without
// requiring the user to refresh the Dashboard page").
(function () {
  'use strict';

  var LIVE_REFRESH_INTERVAL_MS = 20000;

  document.addEventListener('DOMContentLoaded', function () {
    var select = document.getElementById('dashboardRdCodeSelect');
    var host = document.getElementById('dashboardRdCodeComboboxHost');
    if (select && host && window.DctSearchableCombobox) {
      host.appendChild(window.DctSearchableCombobox.create(select, { placeholder: 'Search RD Code', minWidth: '220px', maxWidth: '260px' }));
      host.appendChild(select);
    }

    var card = document.getElementById('dashboardStatsCard');
    var resultsContainer = document.getElementById('dashboard-results');
    var resultsUrl = card ? card.getAttribute('data-dashboard-results-url') : null;
    if (!card || !resultsContainer || !resultsUrl) return;

    // Re-fetches the SAME page/filter the user is currently looking at - list-page.js already
    // keeps window.location.search in sync with every pagination click, page-size change, and
    // filter Search/Clear (via pushState), so simply re-appending the current query string here
    // is enough to preserve the user's page/filter across every refresh tick, with no separate
    // state to track.
    function liveRefresh() {
      // A mid-flight click on a pagination link/page-size select already triggers its own fetch
      // via list-page.js - skip this tick rather than racing it (the next tick will pick up
      // whatever state that click left behind anyway).
      if (resultsContainer.getAttribute('aria-busy') === 'true') return;

      fetch(resultsUrl + window.location.search, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { return r.ok ? r.text() : null; })
        .then(function (html) { if (html !== null) resultsContainer.innerHTML = html; })
        .catch(function () { /* a failed background refresh is silent - the user's last-loaded data just stays on screen */ });
    }

    setInterval(liveRefresh, LIVE_REFRESH_INTERVAL_MS);
  });
})();
