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
  });
})();
