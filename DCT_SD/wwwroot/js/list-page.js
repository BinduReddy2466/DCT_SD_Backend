// Generic AJAX partial-view swap for filter/search/pagination list screens.
// Usage: a <form data-list-page-form> whose action points at a controller action that
// returns a PartialView (HTML fragment), and a container with a matching
// data-list-page-results="<id>" attribute that the fragment gets swapped into. Pagination
// links inside the results fragment are followed the same way via event delegation, so no
// per-page wiring is needed as pages get added.
(function () {
  'use strict';

  function swapResults(container, html) {
    container.innerHTML = html;
  }

  async function fetchAndSwap(url, container) {
    container.setAttribute('aria-busy', 'true');
    try {
      const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      const html = await response.text();
      swapResults(container, html);
    } finally {
      container.removeAttribute('aria-busy');
    }
  }

  function initListPage(form) {
    const targetId = form.getAttribute('data-list-page-form');
    const container = document.getElementById(targetId);
    if (!container) return;

    function submitForm() {
      const params = new URLSearchParams(new FormData(form));
      const url = form.getAttribute('action') + '?' + params.toString();
      fetchAndSwap(url, container);
      if (window.history && window.history.pushState) {
        window.history.pushState({}, '', url);
      }
    }

    form.addEventListener('submit', function (e) {
      e.preventDefault();
      submitForm();
    });

    const clearBtn = form.querySelector('[data-list-page-clear]');
    if (clearBtn) {
      clearBtn.addEventListener('click', function () {
        form.reset();
        submitForm();
      });
    }

    container.addEventListener('click', function (e) {
      const link = e.target.closest('a[data-list-page-link]');
      if (!link || link.classList.contains('disabled')) return;
      e.preventDefault();
      fetchAndSwap(link.getAttribute('href'), container);
      if (window.history && window.history.pushState) {
        window.history.pushState({}, '', link.getAttribute('href'));
      }
    });

    container.addEventListener('change', function (e) {
      const select = e.target.closest('[data-list-page-pagesize]');
      if (!select) return;
      const query = JSON.parse(select.getAttribute('data-query') || '{}');
      query.pageSize = select.value;
      query.pageNumber = 1;
      const url = select.getAttribute('data-action') + '?' + new URLSearchParams(query).toString();
      fetchAndSwap(url, container);
      if (window.history && window.history.pushState) {
        window.history.pushState({}, '', url);
      }
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-list-page-form]').forEach(initListPage);
  });
})();
