// Topbar clock, sidebar profile dropdown, sidebar collapse toggle, and the Light/Dark theme
// switch. Ports the legacy HTML prototype's formatHistoryDate()/tickClock()/toggleProfileMenu()
// behavior. Light is the default theme; Dark Premium (see site.css's [data-bs-theme="dark"]
// token block) is available as an opt-in via #themeSwitch, which this file also loads on the
// Login page (Login has its own copy of the same #themeSwitch control - see Login.cshtml).
(function () {
  'use strict';

  // Shows the server's own configured local time (not the viewer's browser/OS timezone) - see
  // wwwroot/js/server-time.js / Helpers/DisplayTime.cs. Whatever timezone the server this app is
  // deployed on is set to, that's what the topbar clock (and every other timestamp) follows.
  function formatClock(date) {
    return window.formatServerClock ? window.formatServerClock(date) : date.toLocaleString();
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
    // A single on/off switch (#themeSwitch) in the topbar, and an identical one on the Login
    // page. The actual data-bs-theme attribute is already set before this script even runs (see
    // the inline head script in _Layout.cshtml, which prevents a flash of the wrong theme on
    // load) - this just wires the switch and keeps its checked state in sync with it.
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
