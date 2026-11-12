// Generalized version of manual-validation-details.js's original wireDocTypeCombobox - wraps an
// already-populated, already-valued hidden <select> with a visible, type-to-filter text input
// plus a filtered dropdown menu (mouse or keyboard), so any page can get a searchable dropdown
// over a plain <select> without depending on a page-specific options array. Every selection sets
// select.value and dispatches a native 'change' event on it, so existing delegated change-handler
// logic elsewhere needs no changes at all to work with this UI.
(function () {
  'use strict';

  function create(select, opts) {
    opts = opts || {};
    var placeholder = opts.placeholder || 'Select';

    var container = document.createElement('div');
    container.className = 'position-relative';
    container.style.minWidth = opts.minWidth || '200px';
    container.style.maxWidth = opts.maxWidth || '260px';

    var input = document.createElement('input');
    input.type = 'text';
    input.className = 'form-control form-control-sm';
    input.setAttribute('autocomplete', 'off');
    input.placeholder = placeholder;

    var menu = document.createElement('div');
    menu.className = 'dropdown-menu p-0';
    menu.style.maxHeight = '220px';
    menu.style.overflowY = 'auto';
    menu.style.width = '100%';

    var activeIndex = -1;
    var currentMatches = [];

    function options() {
      return Array.prototype.slice.call(select.options).filter(function (o) { return o.value; });
    }

    function textForValue(value) {
      if (!value) return '';
      var match = options().filter(function (o) { return o.value === value; })[0];
      return match ? (match.getAttribute('data-name') || match.textContent) : '';
    }

    function closeMenu() {
      menu.classList.remove('show');
      menu.innerHTML = '';
      activeIndex = -1;
      currentMatches = [];
    }

    function highlight(index) {
      var items = menu.querySelectorAll('[data-combobox-option]');
      Array.prototype.forEach.call(items, function (el, i) {
        el.classList.toggle('active', i === index);
      });
      if (items[index]) items[index].scrollIntoView({ block: 'nearest' });
    }

    function choose(option) {
      select.value = option.value;
      input.value = option.getAttribute('data-name') || option.textContent;
      closeMenu();
      select.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function openMenu(filterText) {
      var term = (filterText || '').trim().toLowerCase();
      currentMatches = options().filter(function (o) {
        var text = (o.getAttribute('data-name') || o.textContent || '').toLowerCase();
        return !term || text.indexOf(term) !== -1;
      });

      menu.innerHTML = '';
      if (currentMatches.length === 0) {
        var empty = document.createElement('span');
        empty.className = 'dropdown-item-text text-muted small px-2 py-1 d-block';
        empty.textContent = 'No matching option';
        menu.appendChild(empty);
      } else {
        currentMatches.forEach(function (o) {
          var item = document.createElement('button');
          item.type = 'button';
          item.className = 'dropdown-item small';
          item.textContent = o.getAttribute('data-name') || o.textContent;
          item.setAttribute('data-combobox-option', '');
          // mousedown (not click), with preventDefault, fires and completes before the input's
          // own blur handler - so choosing an option never races with the blur-triggered close.
          item.addEventListener('mousedown', function (e) {
            e.preventDefault();
            choose(o);
          });
          menu.appendChild(item);
        });
      }

      activeIndex = -1;
      menu.classList.add('show');
    }

    input.addEventListener('focus', function () {
      openMenu('');
      input.select();
    });

    input.addEventListener('input', function () {
      openMenu(input.value);
    });

    input.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        if (!menu.classList.contains('show')) { openMenu(input.value); return; }
        if (currentMatches.length === 0) return;
        activeIndex = (activeIndex + 1) % currentMatches.length;
        highlight(activeIndex);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        if (currentMatches.length === 0) return;
        activeIndex = (activeIndex - 1 + currentMatches.length) % currentMatches.length;
        highlight(activeIndex);
      } else if (e.key === 'Enter') {
        e.preventDefault();
        if (activeIndex >= 0 && currentMatches[activeIndex]) {
          choose(currentMatches[activeIndex]);
        }
      } else if (e.key === 'Escape') {
        closeMenu();
        input.blur();
      }
    });

    // Leaving the field without picking an option never counts as a selection - snap the visible
    // text back to whatever is actually selected, exactly like a native <select>.
    input.addEventListener('blur', function () {
      closeMenu();
      input.value = textForValue(select.value);
    });

    input.value = textForValue(select.value);
    container.appendChild(input);
    container.appendChild(menu);
    return container;
  }

  window.DctSearchableCombobox = { create: create };
})();
