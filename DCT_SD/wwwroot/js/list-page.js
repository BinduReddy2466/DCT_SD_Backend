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

  // Shared "From Date"/"To Date" range validation: every date-range filter in the app (Fetch
  // History, Root Source Path History, Empty Folders, Migrations, Users, Manual Validation,
  // Failed Extraction, Reports) names its fields exactly DateFrom/DateTo and submits through this
  // same submitForm(), so one check here covers all of them. Forms with no such fields (the
  // `!from || !to` case) are completely unaffected.
  function hasExactlyOneDate(form) {
    const from = form.querySelector('[name="DateFrom"]');
    const to = form.querySelector('[name="DateTo"]');
    if (!from || !to) return false;
    return Boolean(from.value) !== Boolean(to.value);
  }

  async function fetchAndSwap(url, container) {
    container.setAttribute('aria-busy', 'true');
    try {
      const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(response)) return;
      if (!response.ok) {
        if (window.showToast) window.showToast('Could not load results. Please try again.', 'error');
        return;
      }
      const html = await response.text();
      swapResults(container, html);
    } catch (e) {
      // Network failure (offline, DNS, server unreachable, etc.) - without this, the fetch
      // rejects silently, leaving whatever results were already on screen with no indication
      // the refresh/search/pagination click actually failed.
      if (window.showToast) window.showToast('Could not load results. Please check your connection and try again.', 'error');
    } finally {
      container.removeAttribute('aria-busy');
    }
  }

  function initListPage(form) {
    const targetId = form.getAttribute('data-list-page-form');
    const container = document.getElementById(targetId);
    if (!container) return;

    // The fetch/swap endpoint (data-list-page-form's action, or a pagination/page-size link's
    // href) returns a bare PartialView with no <head>/CSS/layout - it exists only to be
    // fetched via AJAX and swapped into the container. The address bar must therefore point at
    // the real full-page URL (data-list-page-page-url, i.e. the Index action) with the same
    // query string, not at that partial endpoint - otherwise a refresh, browser back/forward, or
    // a bookmarked/shared link loads the raw unstyled fragment as if it were the whole page.
    const pageUrl = form.getAttribute('data-list-page-page-url');

    function pushUrlState(fetchUrl, query) {
      if (!window.history || !window.history.pushState) return;
      const target = pageUrl ? (pageUrl + '?' + query) : fetchUrl;
      window.history.pushState({}, '', target);
    }

    function submitForm() {
      if (hasExactlyOneDate(form)) {
        if (window.showToast) {
          window.showToast('Please enter both From Date and To Date', 'error');
        }
        return;
      }
      const query = new URLSearchParams(new FormData(form)).toString();
      const url = form.getAttribute('action') + '?' + query;
      fetchAndSwap(url, container);
      pushUrlState(url, query);
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
      const href = link.getAttribute('href');
      fetchAndSwap(href, container);
      const queryIndex = href.indexOf('?');
      pushUrlState(href, queryIndex >= 0 ? href.slice(queryIndex + 1) : '');
    });

    container.addEventListener('change', function (e) {
      const select = e.target.closest('[data-list-page-pagesize]');
      if (!select) return;
      const query = JSON.parse(select.getAttribute('data-query') || '{}');
      query.pageSize = select.value;
      query.pageNumber = 1;
      const queryString = new URLSearchParams(query).toString();
      const url = select.getAttribute('data-action') + '?' + queryString;
      fetchAndSwap(url, container);
      pushUrlState(url, queryString);
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-list-page-form]').forEach(initListPage);
  });
})();
