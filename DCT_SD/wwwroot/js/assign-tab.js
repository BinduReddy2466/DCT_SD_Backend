// The "Assign Tab" picker used by the User form. Lives as its own top-level modal (sibling
// of #ajaxModal, not nested inside it - Bootstrap modals are fragile when nested) so it can
// be launched from a button inside the AJAX-loaded user form. Selection is written back into
// hidden AssignedMenuIds inputs inside that same still-open user form, so it submits normally.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    var modalEl = document.getElementById('assignTabModal');
    if (!modalEl || !window.bootstrap) return;

    var bsModal = new bootstrap.Modal(modalEl);
    var listEl = document.getElementById('assignTabMenuList');
    var selectAllEl = document.getElementById('assignTabSelectAll');
    var saveBtn = document.getElementById('assignTabSaveBtn');
    var messageEl = document.getElementById('assignTabMessage');
    var currentTrigger = null;

    // Save disabled and the warning message are two views of the same state (zero tabs
    // checked) - keep them driven from one place so they can never drift out of sync.
    function updateSaveState() {
      var anyChecked = listEl.querySelectorAll('input[type=checkbox]:checked').length > 0;
      saveBtn.disabled = !anyChecked;
      if (messageEl) {
        messageEl.classList.toggle('d-none', anyChecked);
      }
    }

    document.addEventListener('click', function (e) {
      var trigger = e.target.closest('[data-assign-tab-trigger]');
      if (!trigger) return;
      e.preventDefault();
      currentTrigger = trigger;

      var menus = JSON.parse(trigger.getAttribute('data-menus') || '[]');
      var selected = (trigger.getAttribute('data-selected') || '')
        .split(',')
        .filter(Boolean)
        .map(Number);

      listEl.innerHTML = '';
      menus.forEach(function (m) {
        var tr = document.createElement('tr');
        var checked = selected.indexOf(m.id) !== -1;
        var td1 = document.createElement('td');
        td1.textContent = m.label;
        var td2 = document.createElement('td');
        td2.className = 'text-center';
        var cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = m.id;
        cb.checked = checked;
        cb.addEventListener('change', updateSaveState);
        td2.appendChild(cb);
        tr.appendChild(td1);
        tr.appendChild(td2);
        listEl.appendChild(tr);
      });

      selectAllEl.checked = menus.length > 0 && selected.length === menus.length;
      updateSaveState();
      bsModal.show();
    });

    selectAllEl.addEventListener('change', function () {
      listEl.querySelectorAll('input[type=checkbox]').forEach(function (cb) {
        cb.checked = selectAllEl.checked;
      });
      updateSaveState();
    });

    saveBtn.addEventListener('click', function () {
      if (!currentTrigger) return;

      var selectedIds = Array.from(listEl.querySelectorAll('input[type=checkbox]:checked')).map(function (cb) {
        return cb.value;
      });

      var targetContainerId = currentTrigger.getAttribute('data-target');
      var container = document.getElementById(targetContainerId);
      if (container) {
        container.innerHTML = '';
        selectedIds.forEach(function (id) {
          var hidden = document.createElement('input');
          hidden.type = 'hidden';
          hidden.name = 'AssignedMenuIds';
          hidden.value = id;
          container.appendChild(hidden);
        });
      }

      currentTrigger.setAttribute('data-selected', selectedIds.join(','));
      var badge = currentTrigger.querySelector('[data-assign-tab-count]');
      if (badge) {
        if (selectedIds.length > 0) {
          badge.textContent = selectedIds.length + ' selected';
          badge.classList.remove('d-none');
        } else {
          badge.classList.add('d-none');
        }
      }

      // A prior failed Save attempt (Sub-Admin, zero tabs) leaves "Please assign a tab..."
      // sitting in the user form's validation summary. It was a server round-trip error, so
      // nothing clears it automatically - once the user has actually picked a tab here, that
      // message is stale and would otherwise linger on screen until the next submit.
      if (selectedIds.length > 0) {
        var form = currentTrigger.closest('form');
        var summary = form && form.querySelector('#formValidationSummary');
        if (summary && /assign a tab/i.test(summary.textContent)) {
          summary.innerHTML = '';
        }
      }

      bsModal.hide();
    });
  });
})();
