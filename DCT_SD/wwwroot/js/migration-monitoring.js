// Migration Monitoring list page - expandable rows (User Story 26, AC1). Each Entry row has its
// own arrow that reveals its Associated Title Records and Supporting Documents, reusing the same
// data the Details page already shows - no new backend concepts, no page navigation. Wired via
// event delegation on the stable #migrations-results container, since list-page.js replaces that
// container's entire innerHTML on every search/sort/page change (which is also what makes expand
// state reset to fully collapsed on those actions, with no extra code needed for it).
(function () {
  'use strict';

  var DOC_STATUS_DISPLAY = {
    Migrated: 'Migrated', DuplicateSd: 'Duplicate SD', Overwritten: 'Overwritten', InsertedAsNew: 'Inserted',
    Failed: 'Failed Migration', ConfirmedDuplicate: 'Confirmed Duplicate', NotApplicable: 'N/A',
  };

  // Mirrors Helpers/StatusBadgeTagHelper.cs's variant map (the subset of statuses that can
  // actually appear in these two nested tables) - same convention this codebase already follows
  // of each consumer owning its own small copy rather than sharing one across server/client.
  var BADGE_VARIANTS = {
    'Ready for Migration': 'ok',
    'Migrating': 'info',
    'Migrated': 'ok',
    'Failed Migration': 'err',
    'N/A': 'neutral',
    'Duplicate SD': 'err',
    'Overwritten': 'info',
    'Inserted': 'info',
    'Confirmed Duplicate': 'neutral',
  };

  function escapeHtml(value) {
    return String(value == null ? '' : value).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  function badgeHtml(text) {
    if (!text) return '—';
    var variant = BADGE_VARIANTS[text] || 'neutral';
    return '<span class="badge badge-' + variant + '">' + escapeHtml(text) + '</span>';
  }

  function titleRecordsTable(titleRecords) {
    if (!titleRecords || titleRecords.length === 0) {
      return '<div class="text-muted small mb-3">No Associated Title Records Found</div>';
    }
    var rows = titleRecords.map(function (t) {
      return '<tr>' +
        '<td>' + escapeHtml(t.title || '—') + '</td>' +
        '<td>' + escapeHtml(t.titleSequence || '—') + '</td>' +
        '<td>' + escapeHtml(t.targetSystemValidation || '—') + '</td>' +
        '<td>' + badgeHtml(t.migrationStatus) + '</td>' +
        '<td class="cell-wrap">' + escapeHtml(t.reasonForFailing || '—') + '</td>' +
        '</tr>';
    }).join('');
    return '<div class="table-responsive mb-3"><table class="table table-sm align-middle mb-0">' +
      '<thead><tr><th>Title Number</th><th>Title Sequence</th><th>Target System Validation</th><th>Migration Status</th><th>Reason for Failing</th></tr></thead>' +
      '<tbody>' + rows + '</tbody></table></div>';
  }

  function documentsTable(documents) {
    if (!documents || documents.length === 0) {
      return '<div class="text-muted small">No Supporting Documents Found</div>';
    }
    var rows = documents.map(function (d, i) {
      var statusText = DOC_STATUS_DISPLAY[d.status] || d.status;
      return '<tr>' +
        '<td>' + (i + 1) + '</td>' +
        '<td class="small cell-wrap">' + escapeHtml(d.documentType || '—') + '</td>' +
        '<td class="small text-muted cell-wrap">' + escapeHtml(d.fileName) + '</td>' +
        '<td class="small text-muted cell-wrap">' + escapeHtml(d.sourceImagePath || '—') + '</td>' +
        '<td class="small text-muted cell-wrap">' + escapeHtml(d.destinationPath || '—') + '</td>' +
        '<td class="small">' + escapeHtml(d.targetSystemValidation || '—') + '</td>' +
        '<td>' + badgeHtml(statusText) + '</td>' +
        '<td class="small cell-wrap">' + escapeHtml(d.reasonForFailing || '—') + '</td>' +
        '</tr>';
    }).join('');
    return '<div class="table-responsive"><table class="table table-sm align-middle mb-0">' +
      '<thead><tr><th style="width: 2.5rem;">#</th><th>Document Type</th><th>Image File Name</th><th>Source Image File Path</th><th>Target Image File Path</th><th>Target System Validation</th><th>Migration Status</th><th>Reason for Failing</th></tr></thead>' +
      '<tbody>' + rows + '</tbody></table></div>';
  }

  function renderPanel(body, data) {
    body.innerHTML =
      '<h6 class="mb-2">Entry-Title Records (' + (data.titleRecords ? data.titleRecords.length : 0) + ')</h6>' +
      titleRecordsTable(data.titleRecords) +
      '<h6 class="mb-2">Supporting Document Images (' + (data.documents ? data.documents.length : 0) + ')</h6>' +
      documentsTable(data.documents);
  }

  function loadPanel(body) {
    var url = body.getAttribute('data-expand-url');
    body.innerHTML = '<div class="text-muted small">Loading…</div>';
    fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(function (r) {
        if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
        if (!r.ok) throw new Error('request failed');
        return r.json();
      })
      .then(function (data) {
        if (!data) return; // session expired - already redirected above
        body.setAttribute('data-loaded', 'true');
        renderPanel(body, data);
      })
      .catch(function () {
        body.innerHTML = '<div class="small text-danger">Could not load this row\'s details. <button type="button" class="btn btn-link btn-sm p-0 align-baseline" data-expand-retry>Retry</button></div>';
      });
  }

  document.addEventListener('DOMContentLoaded', function () {
    // Migration Date From/To mutual restriction (task section 5) - same native min/max pattern
    // already used for Root Source Path History's date range in rd-config.js. form.reset()
    // (Clear) fires a native 'reset' event, which is also where the restrictions themselves get
    // cleared (task section 7's "reset date picker minimum and maximum restrictions").
    var dateFrom = document.getElementById('DateFrom');
    var dateTo = document.getElementById('DateTo');
    if (dateFrom && dateTo) {
      dateFrom.addEventListener('change', function () { dateTo.min = dateFrom.value || ''; });
      dateTo.addEventListener('change', function () { dateFrom.max = dateTo.value || ''; });
      var searchForm = dateFrom.closest('form');
      if (searchForm) {
        searchForm.addEventListener('reset', function () {
          dateTo.min = '';
          dateFrom.max = '';
        });
      }
    }

    // --- Reprocess (list row action, shown when Migration Status = Failed Migration) - a small,
    // self-contained Remarks-validated modal living in Index.cshtml (not swapped out by
    // list-page.js's AJAX refresh of #migrations-results, unlike the row buttons that open it). ---
    var listReprocessModalEl = document.getElementById('migrationListReprocessModal');
    var listReprocessModal = listReprocessModalEl && window.bootstrap ? new bootstrap.Modal(listReprocessModalEl) : null;
    var listReprocessRemarksText = document.getElementById('migrationListReprocessRemarksText');
    var listReprocessRemarksError = document.getElementById('migrationListReprocessRemarksError');
    var listReprocessConfirmBtn = document.getElementById('migrationListReprocessConfirmBtn');
    var listReprocessEntryId = null;

    function toast(message, variant) {
      if (window.showToast) window.showToast(message, variant);
    }

    if (listReprocessRemarksText) {
      listReprocessRemarksText.addEventListener('input', function () {
        var val = listReprocessRemarksText.value.trim();
        listReprocessConfirmBtn.disabled = val.length === 0;
        listReprocessRemarksError.classList.toggle('d-none', val.length > 0 || listReprocessRemarksText.value.length === 0);
      });
    }

    if (listReprocessConfirmBtn) {
      listReprocessConfirmBtn.addEventListener('click', function () {
        var remarks = listReprocessRemarksText.value.trim();
        if (!remarks) {
          listReprocessRemarksError.classList.remove('d-none');
          return;
        }
        if (listReprocessConfirmBtn.disabled) return;
        listReprocessConfirmBtn.disabled = true;

        var tokenInput = document.querySelector('#migrationListAntiForgeryForm input[name="__RequestVerificationToken"]');
        var token = tokenInput ? tokenInput.value : '';
        var body = new URLSearchParams({ id: listReprocessEntryId, remarks: remarks, __RequestVerificationToken: token });
        fetch('/Migrations/Reprocess', { method: 'POST', body: body, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
          .then(function (r) {
            if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
            return r.json();
          })
          .then(function (data) {
            if (!data) return;
            if (listReprocessModal) listReprocessModal.hide();
            toast(data.message || (data.success ? 'Reprocessed.' : 'Unable to reprocess this Entry Record.'), data.success ? 'success' : 'error');
            setTimeout(function () { window.location.reload(); }, 900);
          })
          .catch(function () {
            toast('Unable to reprocess this Entry Record. Please try again.', 'error');
          })
          .finally(function () { listReprocessConfirmBtn.disabled = false; });
      });
    }

    var container = document.getElementById('migrations-results');
    if (!container) return;

    container.addEventListener('click', function (e) {
      var reprocessBtn = e.target.closest('[data-list-reprocess-btn]');
      if (reprocessBtn) {
        listReprocessEntryId = reprocessBtn.getAttribute('data-entry-id');
        if (listReprocessRemarksText) listReprocessRemarksText.value = '';
        if (listReprocessRemarksError) listReprocessRemarksError.classList.add('d-none');
        if (listReprocessConfirmBtn) listReprocessConfirmBtn.disabled = true;
        if (listReprocessModal) listReprocessModal.show();
        return;
      }

      var toggle = e.target.closest('.migration-expand-toggle');
      if (toggle) {
        var id = toggle.getAttribute('data-expand-toggle');
        var panelRow = container.querySelector('tr.migration-expand-row[data-expand-panel-for="' + id + '"]');
        if (!panelRow) return;

        var expanding = panelRow.classList.contains('d-none');
        panelRow.classList.toggle('d-none', !expanding);
        toggle.setAttribute('aria-expanded', expanding ? 'true' : 'false');

        if (expanding) {
          var body = panelRow.querySelector('[data-expand-panel-body]');
          if (body && !body.getAttribute('data-loaded')) {
            loadPanel(body);
          }
        }
        return;
      }

      var retry = e.target.closest('[data-expand-retry]');
      if (retry) {
        var body2 = retry.closest('[data-expand-panel-body]');
        if (body2) loadPanel(body2);
      }
    });
  });
})();
