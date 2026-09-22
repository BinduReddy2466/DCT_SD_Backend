// Topbar clock, sidebar profile dropdown, and sidebar collapse toggle. Ports the legacy HTML
// prototype's formatHistoryDate()/tickClock()/toggleProfileMenu() behavior.
(function () {
  'use strict';

  function formatClock(date) {
    var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    var hours = date.getHours();
    var minutes = String(date.getMinutes()).padStart(2, '0');
    var ampm = hours >= 12 ? 'PM' : 'AM';
    hours = hours % 12;
    if (hours === 0) hours = 12;
    return months[date.getMonth()] + ' ' + String(date.getDate()).padStart(2, '0') + ', ' + date.getFullYear() + ' ' + String(hours).padStart(2, '0') + ':' + minutes + ' ' + ampm;
  }

  document.addEventListener('DOMContentLoaded', function () {
    var clockEl = document.getElementById('clock');
    if (clockEl) {
      var tick = function () { clockEl.textContent = formatClock(new Date()); };
      tick();
      setInterval(tick, 30000);
    }

    var trigger = document.getElementById('sidebarProfileTrigger');
    var menu = document.getElementById('sidebarProfileMenu');
    if (trigger && menu) {
      trigger.addEventListener('click', function (e) {
        e.stopPropagation();
        menu.classList.toggle('d-none');
      });
      document.addEventListener('click', function (e) {
        if (!e.target.closest('#sidebarProfileTrigger') && !e.target.closest('#sidebarProfileMenu')) {
          menu.classList.add('d-none');
        }
      });
    }

    var collapseToggle = document.getElementById('sidebarCollapseToggle');
    var appShell = document.getElementById('appShell');
    if (collapseToggle && appShell) {
      collapseToggle.addEventListener('click', function () {
        appShell.classList.toggle('sidebar-collapsed');
      });
    }

    // --- Theme toggle (Light/Dark) ---
    // A single on/off switch in the topbar, next to the clock. The actual data-bs-theme
    // attribute is already set before this script even runs (see the inline head script in
    // _Layout.cshtml, which prevents a flash of the wrong theme on load) - this just wires the
    // switch and keeps its checked state in sync with it.
    var THEME_STORAGE_KEY = 'dct-theme';
    var themeSwitch = document.getElementById('themeSwitch');

    function applyTheme(theme) {
      document.documentElement.setAttribute('data-bs-theme', theme);
      try { localStorage.setItem(THEME_STORAGE_KEY, theme); } catch (e) { /* storage unavailable - theme still applies for this page view */ }
      if (themeSwitch) themeSwitch.checked = theme === 'dark';
    }

    if (themeSwitch) {
      themeSwitch.checked = document.documentElement.getAttribute('data-bs-theme') === 'dark';
      themeSwitch.addEventListener('change', function () {
        applyTheme(themeSwitch.checked ? 'dark' : 'light');
      });
    }
  });
})();
