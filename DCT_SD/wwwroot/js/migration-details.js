// Wires the Migration Details page: the Document Image Viewer (single/dual pane), the
// JS-rendered Supporting Documents table (Document Type correction, Duplicate SD resolution),
// the RD Code correction combobox, Save/Ready for Migration/Initiate Migration, and the
// Close-with-unsaved-changes flow. Mirrors manual-validation-details.js's conventions
// (searchable combobox, isDirty/snapshot tracking, AJAX Save) adapted to this page's data shape.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    if (window.bootstrap) {
      document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
        new window.bootstrap.Tooltip(el);
      });
    }

    var detailsForm = document.getElementById('migrationDetailsForm');
    var migrationId = window.location.pathname.split('/').filter(Boolean).pop();
    var tokenInput = detailsForm ? detailsForm.querySelector('input[name="__RequestVerificationToken"]') : null;
    var token = tokenInput ? tokenInput.value : '';
    var rdCodeField = document.getElementById('migrationRdCode');
    var documentCorrectionsField = document.getElementById('migrationDocumentCorrectionsJson');

    var fieldIds = ['migrationRdCode', 'migrationDocumentCorrectionsJson'];
    function currentValues() {
      return {
        migrationRdCode: rdCodeField ? rdCodeField.value || '' : '',
        migrationDocumentCorrectionsJson: documentCorrectionsField ? documentCorrectionsField.value || '' : '',
      };
    }
    var snapshot = currentValues();
    function isDirty() {
      var current = currentValues();
      return fieldIds.some(function (id) { return current[id] !== snapshot[id]; });
    }

    function toast(message, variant) {
      if (window.showToast) window.showToast(message, variant);
    }

    function refreshActionHistory() {
      var container = document.getElementById('migrationActionHistoryContainer');
      if (!container) return;
      fetch('/Migrations/ActionHistoryInline?id=' + migrationId, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { return r.ok ? r.text() : null; })
        .then(function (html) { if (html !== null) container.innerHTML = html; })
        .catch(function () {});
    }

    // --- Shared Remarks-validated confirmation modal (Insert as New Image, Confirm Duplicate,
    // Reprocess) - its markup is always present in Details.cshtml regardless of whether this
    // record has any documents, so it's wired here at the top level (not inside the
    // documents.length > 0 block below) so Reprocess can use it even for a Failed record that has
    // no documents at all yet. minLength defaults to 10 (Insert as New Image/Confirm Duplicate's
    // rule); Reprocess passes 1 (just non-blank, no minimum per spec). ---
    var remarksModalEl = document.getElementById('migrationRemarksModal');
    var remarksModal = remarksModalEl && window.bootstrap ? new bootstrap.Modal(remarksModalEl) : null;
    var remarksTitleEl = document.getElementById('migrationRemarksModalTitle');
    var remarksMessageEl = document.getElementById('migrationRemarksModalMessage');
    var remarksTextEl = document.getElementById('migrationRemarksText');
    var remarksErrorEl = document.getElementById('migrationRemarksError');
    var remarksConfirmBtn = document.getElementById('migrationRemarksConfirmBtn');
    var remarksPendingSubmit = null;
    var remarksMinLength = 10;

    function updateRemarksValidity() {
      var val = remarksTextEl.value.trim();
      var valid = val.length >= remarksMinLength;
      remarksConfirmBtn.disabled = !valid;
      remarksErrorEl.classList.toggle('d-none', valid || remarksTextEl.value.length === 0);
    }
    if (remarksTextEl) {
      remarksTextEl.addEventListener('input', updateRemarksValidity);
    }

    function openRemarksModal(title, message, onConfirm, minLength) {
      if (!remarksModal) return;
      remarksMinLength = minLength || 10;
      remarksTitleEl.textContent = title;
      remarksMessageEl.textContent = message;
      remarksTextEl.value = '';
      remarksErrorEl.textContent = remarksMinLength > 1
        ? 'Remarks is required and must be at least ' + remarksMinLength + ' characters.'
        : 'Remarks is required.';
      remarksErrorEl.classList.add('d-none');
      remarksConfirmBtn.disabled = true;
      remarksPendingSubmit = onConfirm;
      remarksModal.show();
    }

    if (remarksConfirmBtn) {
      remarksConfirmBtn.addEventListener('click', function () {
        var val = remarksTextEl.value.trim();
        if (val.length < remarksMinLength) {
          remarksErrorEl.classList.remove('d-none');
          return;
        }
        if (remarksConfirmBtn.disabled) return;
        remarksConfirmBtn.disabled = true;
        var submit = remarksPendingSubmit;
        remarksPendingSubmit = null;
        if (typeof submit === 'function') {
          submit(val).finally(function () { remarksConfirmBtn.disabled = false; });
        }
      });
    }

    // --- Ready for Migration readiness (spec section 5.1): the button stays disabled while any
    // applicable correction is incomplete OR unsaved - only a successful Save (never a pending,
    // in-browser-only selection) can enable it. `documents` is assigned further down, but this is
    // only ever called after that assignment (initial render + after Save). ---
    var targetRdNotIdentified = (function () {
      var btn = document.getElementById('migrationReadyForMigrationBtn');
      return !!btn && btn.getAttribute('data-target-rd-not-identified') === 'true';
    })();
    function updateReadyForMigrationButtonState() {
      var btn = document.getElementById('migrationReadyForMigrationBtn');
      if (!btn) return;
      var hasPendingDocumentType = documents.some(function (d) { return d.canChangeDocumentType; });
      btn.disabled = targetRdNotIdentified || hasPendingDocumentType;
      btn.title = btn.disabled
        ? 'Complete and save all pending corrections (' + [targetRdNotIdentified ? 'RD Code' : null, hasPendingDocumentType ? 'Document Type' : null].filter(Boolean).join(', ') + ') before marking this Entry Record Ready for Migration.'
        : '';
    }

    var initiateMigrationBtn = document.getElementById('initiateMigrationBtn');
    if (initiateMigrationBtn) {
      initiateMigrationBtn.addEventListener('click', function () {
        if (initiateMigrationBtn.disabled) return; // prevent duplicate submissions
        initiateMigrationBtn.disabled = true;
        document.getElementById('initiateMigrationForm').requestSubmit();
      });
    }

    // Reprocess (shown when ProcessingStatus = Failed) - mandatory Remarks, no minimum length
    // (unlike Insert as New Image/Confirm Duplicate's 10-character rule). AJAX; reloads the page
    // on completion since Reprocess can change the Migration Status, Reason for Failing,
    // Supporting Documents, and Action History all at once.
    var reprocessBtn = document.getElementById('reprocessBtn');
    if (reprocessBtn) {
      reprocessBtn.addEventListener('click', function () {
        openRemarksModal(
          'Reprocess Migration',
          'Are you sure you want to reprocess the failed migration for this Entry Record? This action will retry the failed migration.',
          function (remarks) {
            var body = new URLSearchParams({ id: migrationId, remarks: remarks, __RequestVerificationToken: token });
            return fetch('/Migrations/Reprocess', { method: 'POST', body: body, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
              .then(function (r) {
                if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
                return r.json();
              })
              .then(function (data) {
                if (!data) return;
                if (!data.success) {
                  toast(data.message || 'Unable to reprocess this Entry Record.', 'error');
                  return;
                }
                toast(data.message, data.message.indexOf('did not complete') === -1 ? 'success' : 'error');
                setTimeout(function () { window.location.reload(); }, 900);
              })
              .catch(function () {
                toast('Unable to reprocess this Entry Record. Please try again.', 'error');
              });
          },
          1);
      });
    }

    // --- RD Code correction (spec section 3) ---
    var rdCodeHost = document.getElementById('migrationRdCodeComboboxHost');
    if (rdCodeHost && rdCodeField) {
      var registryOffices = JSON.parse((document.getElementById('migrationRegistryOfficesData') || {}).textContent || '[]');
      var rdSelect = document.createElement('select');
      rdSelect.className = 'form-select form-select-sm d-none';
      var rdPlaceholder = document.createElement('option');
      rdPlaceholder.value = '';
      rdPlaceholder.textContent = 'Select RD Code';
      rdSelect.appendChild(rdPlaceholder);
      registryOffices.forEach(function (o) {
        var opt = document.createElement('option');
        opt.value = o.code;
        opt.textContent = o.code + ' - ' + o.name;
        opt.setAttribute('data-name', o.code + ' - ' + o.name);
        rdSelect.appendChild(opt);
      });
      rdSelect.value = rdCodeField.value || '';
      rdSelect.addEventListener('change', function () {
        rdCodeField.value = rdSelect.value;
        var match = registryOffices.filter(function (o) { return o.code === rdSelect.value; })[0];
        var rdNameText = document.getElementById('migrationRdNameText');
        if (rdNameText) rdNameText.textContent = match ? match.name : '—';
      });
      rdCodeHost.appendChild(window.DctSearchableCombobox.create(rdSelect, { placeholder: 'Search RD Code', minWidth: '100%', maxWidth: '100%' }));
      rdCodeHost.appendChild(rdSelect);
    }

    // --- Supporting Documents + Image Viewer ---
    var card = document.querySelector('[data-compare-url-base]');
    var dataEl = document.getElementById('migrationDocumentsData');
    var documents = JSON.parse((dataEl || {}).textContent || '[]');
    var documentTypes = JSON.parse((document.getElementById('migrationDocumentTypesData') || {}).textContent || '[]');

    // Zero or more pending Others -> real Document Type corrections, keyed by doc.id (positive =
    // real MigrationDocuments row id, negative = still-"Pending" synthetic id - see
    // MigrationService.GetByIdAsync). Never sent to the server except as part of Save itself.
    var pendingDocumentChanges = {};

    function escapeRegExp(s) {
      return (s || '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    function sanitizeForFileName(s) {
      return (s || '').replace(/[\/\\]/g, '_');
    }

    // Best-effort client-side preview of the server's NextSequenceForRealDocuments/
    // NextSequenceForPendingDocuments - the server recomputes this authoritatively on Save.
    function nextDocumentSequenceNumber(excludeDocId, code, name) {
      var pattern = new RegExp('^' + escapeRegExp(sanitizeForFileName(code)) + '_' + escapeRegExp(sanitizeForFileName(name)) + '_(\\d+)\\.[^.]+$', 'i');
      var max = 0;
      documents.forEach(function (d) {
        var m = pattern.exec(d.fileName || '');
        if (m) {
          var n = parseInt(m[1], 10);
          if (!isNaN(n) && n > max) max = n;
        }
      });
      var pendingIds = Object.keys(pendingDocumentChanges).map(Number).filter(function (id) {
        var p = pendingDocumentChanges[id];
        return p && p.code.toLowerCase() === code.toLowerCase() && p.name.toLowerCase() === name.toLowerCase();
      }).sort(function (a, b) { return a - b; });
      for (var i = 0; i < pendingIds.length; i++) {
        if (pendingIds[i] === excludeDocId) break;
        max++;
      }
      return max + 1;
    }

    function generatedFileName(doc, code, name) {
      var match = /\.[^.]+$/.exec(doc.fileName || '');
      var ext = match ? match[0] : '';
      var seq = nextDocumentSequenceNumber(doc.id, code, name);
      return sanitizeForFileName(code) + '_' + sanitizeForFileName(name) + '_' + seq + ext;
    }

    function effectiveFileName(doc) {
      var p = pendingDocumentChanges[doc.id];
      return p ? generatedFileName(doc, p.code, p.name) : doc.fileName;
    }

    function effectiveDocumentType(doc) {
      var p = pendingDocumentChanges[doc.id];
      return p ? p.name : (doc.documentType || 'Others');
    }

    function syncDocumentCorrectionsField() {
      var ids = Object.keys(pendingDocumentChanges).map(Number);
      var arr = ids.map(function (id) {
        var p = pendingDocumentChanges[id];
        return { documentId: id, code: p.code, name: p.name };
      });
      if (documentCorrectionsField) documentCorrectionsField.value = arr.length ? JSON.stringify(arr) : '';
    }

    updateReadyForMigrationButtonState();

    if (!card || !dataEl || documents.length === 0) {
      wireSaveCloseReadyForMigration();
      return;
    }

    var DUPLICATE_FAMILY = ['DuplicateSd'];
    var compareUrlBase = card.getAttribute('data-compare-url-base');
    var documentImageUrlBase = card.getAttribute('data-document-image-url-base');

    function sourceImageUrl(doc) {
      return documentImageUrlBase + (documentImageUrlBase.indexOf('?') === -1 ? '?' : '&') + 'documentId=' + doc.id;
    }

    var singleViewer = window.DctDocViewer.create(document.getElementById('migrationViewerSingle'));
    var leftViewer = window.DctDocViewer.create(document.getElementById('migrationViewerLeft'));
    var rightViewer = window.DctDocViewer.create(document.getElementById('migrationViewerRight'));

    var singleWrap = document.getElementById('migrationSingleViewerWrap');
    var dualWrap = document.getElementById('migrationDualViewerWrap');
    var headerLeftEl = document.getElementById('migrationViewerHeaderLeft');
    var headerRightEl = document.getElementById('migrationViewerHeaderRight');
    var footerEl = document.getElementById('migrationViewerFooter');
    var listEl = document.getElementById('migrationDocumentList');
    var prevBtn = document.getElementById('migrationPrevBtn');
    var nextBtn = document.getElementById('migrationNextBtn');

    var activeIndex = 0;

    function renderHeaderLeft(doc, isDuplicateFamily) {
      headerLeftEl.innerHTML = '';
      if (isDuplicateFamily) {
        var compareLink = document.createElement('a');
        compareLink.className = 'btn btn-outline-secondary btn-sm';
        compareLink.textContent = 'Compare';
        compareLink.href = compareUrlBase + (compareUrlBase.indexOf('?') === -1 ? '?' : '&') + 'documentId=' + doc.id;
        headerLeftEl.appendChild(compareLink);
        return;
      }
      var staticBtn = document.createElement('button');
      staticBtn.type = 'button';
      staticBtn.className = 'btn btn-navy btn-sm';
      staticBtn.textContent = 'DCT-SD Image';
      headerLeftEl.appendChild(staticBtn);
    }

    function postDocumentAction(action, documentId, remarks) {
      var body = new URLSearchParams({ id: migrationId, documentId: documentId, remarks: remarks, __RequestVerificationToken: token });
      return fetch('/Migrations/' + action, { method: 'POST', body: body, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) {
          if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
          return r.json();
        })
        .then(function (data) {
          if (!data) return; // session expired - already redirected above
          // A failed Insert as New Image still persists a Reason for Failing on this document
          // (status stays Duplicate SD) - refresh the table so it shows without a page reload,
          // even though the action itself didn't succeed.
          if (data.documents) {
            documents = data.documents;
            renderDocumentList();
            render();
          }
          if (!data.success) {
            toast(data.message || 'Unable to complete this action.', 'error');
            return;
          }
          if (remarksModal) remarksModal.hide();
          toast(data.message, 'success');
          updateReadyForMigrationButtonState();
          refreshActionHistory();
        })
        .catch(function () {
          toast('Unable to complete this action. Please try again.', 'error');
        });
    }

    function renderFooter(doc) {
      footerEl.innerHTML = '';

      if (doc.status === 'DuplicateSd') {
        var wrap = document.createElement('div');
        wrap.className = 'border-top pt-3 d-flex justify-content-center gap-3';

        var insertBtn = document.createElement('button');
        insertBtn.type = 'button';
        insertBtn.className = 'btn btn-navy px-4';
        insertBtn.textContent = 'Insert as New Image';
        insertBtn.addEventListener('click', function () {
          openRemarksModal(
            'Insert as New Image',
            'Are you sure you want to insert this image as a new image to PHILARIS-RD? This action cannot be undone.',
            function (remarks) { return postDocumentAction('InsertAsNew', doc.id, remarks); });
        });

        var confirmDupBtn = document.createElement('button');
        confirmDupBtn.type = 'button';
        confirmDupBtn.className = 'btn btn-outline-secondary px-4';
        confirmDupBtn.textContent = 'Confirm Duplicate';
        confirmDupBtn.addEventListener('click', function () {
          openRemarksModal(
            'Confirm Duplicate',
            'Are you sure you want to confirm this image as a duplicate? This image will not be migrated to PHILARIS-RD. This action cannot be undone.',
            function (remarks) { return postDocumentAction('ConfirmDuplicate', doc.id, remarks); });
        });

        wrap.appendChild(insertBtn);
        wrap.appendChild(confirmDupBtn);
        footerEl.appendChild(wrap);
      } else if (doc.status === 'InsertedAsNew' || doc.status === 'ConfirmedDuplicate') {
        var info = document.createElement('div');
        info.className = 'text-center small text-muted mt-2';
        var badge = doc.status === 'InsertedAsNew' ? 'Inserted' : 'Confirmed Duplicate';
        info.innerHTML =
          '<div class="mb-2"><span class="badge badge-info">' + badge + '</span></div>' +
          (doc.performedBy ? '<div><strong>Performed By:</strong> ' + doc.performedBy + '</div>' : '') +
          (doc.actionDate ? '<div><strong>Action Date:</strong> ' + (window.formatServerDateTime ? window.formatServerDateTime(doc.actionDate) : new Date(doc.actionDate).toLocaleString()) + '</div>' : '');
        footerEl.appendChild(info);
      }
    }

    var viewerCol = document.getElementById('migrationViewerCol');
    var supportingDocsCol = document.getElementById('migrationSupportingDocsCol');
    var prevNextGroup = document.getElementById('migrationPrevNextGroup');
    var prevNextRow = document.getElementById('migrationPrevNextRow');
    var singleToolbarRow = document.getElementById('migrationSingleToolbarRow');

    // Widens both the Image Viewer and Supporting Documents cards to full page width while a
    // Duplicate SD document's dual DCT-SD/PHILARIS-RD panes are showing - at the normal col-xl-6
    // half-width, two full toolbars side by side had no room to breathe (toolbar buttons wrapped
    // onto a second line, panes nearly touched). Reverts to the normal side-by-side layout for
    // every other document status.
    //
    // Also relocates the single #migrationPrevNextGroup DOM node (never duplicated - moving it
    // keeps exactly one pair of Prev/Next buttons, so .disabled state set elsewhere on the same
    // cached prevBtn/nextBtn references always reaches whichever location is current) - into the
    // single-pane toolbar's own row (matching Manual Validation's Image Viewer, which keeps
    // Prev/Next in the same row as zoom/rotate/pan/reset since there's only ever one image) for a
    // single image, or back to its own dedicated row above both panes for the dual view, since
    // Prev/Next conceptually belongs to the document list, not to either individual pane.
    function setViewerWidth(isDuplicateFamily) {
      if (!viewerCol || !supportingDocsCol) return;
      viewerCol.classList.toggle('col-xl-6', !isDuplicateFamily);
      viewerCol.classList.toggle('col-xl-12', isDuplicateFamily);
      supportingDocsCol.classList.toggle('col-xl-6', !isDuplicateFamily);
      supportingDocsCol.classList.toggle('col-xl-12', isDuplicateFamily);

      if (prevNextGroup && prevNextRow && singleToolbarRow) {
        if (isDuplicateFamily) {
          prevNextRow.appendChild(prevNextGroup);
          prevNextRow.classList.remove('d-none');
        } else {
          singleToolbarRow.appendChild(prevNextGroup);
          prevNextRow.classList.add('d-none');
        }
      }
    }

    function render() {
      var doc = documents[activeIndex];
      if (!doc) { activeIndex = 0; doc = documents[0]; }
      var isDuplicateFamily = DUPLICATE_FAMILY.indexOf(doc.status) !== -1;

      if (headerRightEl) headerRightEl.textContent = 'Image ' + (activeIndex + 1) + ' of ' + documents.length;
      prevBtn.disabled = activeIndex === 0;
      nextBtn.disabled = activeIndex === documents.length - 1;

      renderHeaderLeft(doc, isDuplicateFamily);
      renderFooter(doc);
      setViewerWidth(isDuplicateFamily);

      if (isDuplicateFamily) {
        singleWrap.classList.add('d-none');
        dualWrap.classList.remove('d-none');
        leftViewer.load(sourceImageUrl(doc), doc.fileName, { fitOnLoad: true });
        // No real image-serving path exists for PHILARIS-RD (target) documents - the right-hand
        // pane keeps the placeholder.
        var rightName = doc.existingFileName || doc.fileName;
        rightViewer.load(window.dctPlaceholderImage(rightName), rightName, { fitOnLoad: true });
      } else {
        dualWrap.classList.add('d-none');
        singleWrap.classList.remove('d-none');
        singleViewer.load(sourceImageUrl(doc), doc.fileName, { fitOnLoad: true });
      }

      if (listEl) {
        Array.prototype.forEach.call(listEl.querySelectorAll('[data-doc-index]'), function (el) {
          var isActive = Number(el.getAttribute('data-doc-index')) === activeIndex;
          el.classList.toggle('table-active', isActive);
        });
      }
    }

    // --- Supporting Documents table (JS-rendered, like Manual Validation's, so Save/Insert as New
    // Image/Confirm Duplicate responses can update it in place without a page reload) ---
    function renderDocumentList() {
      if (!listEl) return;
      listEl.innerHTML = '';
      var renderErrors = [];
      documents.forEach(function (doc, index) {
        try {
          renderDocumentRow(doc, index);
        } catch (err) {
          renderErrors.push({ index: index, docId: doc && doc.id, message: err && err.message });
          if (window.console) console.error('Migration Details: failed to render Supporting Documents row for document', doc, err);
        }
      });
      if (renderErrors.length > 0) {
        var warnRow = document.createElement('tr');
        var warnTd = document.createElement('td');
        warnTd.colSpan = 8;
        warnTd.className = 'small text-danger';
        warnTd.textContent = renderErrors.length + ' of ' + documents.length + ' supporting document row(s) failed to render - open the browser console for details.';
        warnRow.appendChild(warnTd);
        listEl.appendChild(warnRow);
      }

      function renderDocumentRow(doc, index) {
        var row = document.createElement('tr');
        row.setAttribute('role', 'button');
        row.setAttribute('data-doc-index', String(index));
        row.style.cursor = 'pointer';
        row.classList.toggle('table-active', index === activeIndex);

        var idxTd = document.createElement('td');
        idxTd.textContent = String(index + 1);

        var typeTd = document.createElement('td');
        typeTd.className = 'small';
        if (doc.canChangeDocumentType) {
          var wrapper = document.createElement('div');
          wrapper.className = 'd-flex align-items-center gap-1 flex-wrap';

          var select = document.createElement('select');
          select.className = 'form-select form-select-sm d-none';
          select.setAttribute('data-doc-type-select', '');
          select.setAttribute('data-doc-index', String(index));
          var placeholderOpt = document.createElement('option');
          placeholderOpt.value = '';
          placeholderOpt.textContent = 'Select';
          select.appendChild(placeholderOpt);
          documentTypes.forEach(function (t) {
            var opt = document.createElement('option');
            opt.value = t.code;
            opt.textContent = t.name;
            opt.setAttribute('data-name', t.name);
            select.appendChild(opt);
          });

          var pending = pendingDocumentChanges[doc.id];
          select.value = pending ? pending.code : '';
          wrapper.appendChild(window.DctSearchableCombobox.create(select, { minWidth: '180px', maxWidth: '240px' }));
          wrapper.appendChild(select);
          typeTd.appendChild(wrapper);
        } else {
          typeTd.textContent = doc.documentType || '—';
        }

        var fileTd = document.createElement('td');
        fileTd.className = 'small text-muted';
        fileTd.textContent = effectiveFileName(doc);

        var sourceTd = document.createElement('td');
        sourceTd.className = 'small text-muted cell-wrap';
        sourceTd.textContent = doc.sourceImagePath || '—';

        var targetTd = document.createElement('td');
        targetTd.className = 'small text-muted cell-wrap';
        targetTd.textContent = doc.destinationPath || '—';

        var validationTd = document.createElement('td');
        validationTd.className = 'small';
        validationTd.textContent = doc.targetSystemValidation || '—';

        var statusTd = document.createElement('td');
        // <status-badge> is a server-side Razor TagHelper (StatusBadgeTagHelper) - it only
        // transforms markup at render time, so a client-created element must build the
        // badge/span itself. Mirrors StatusDisplay.MigrationDocStatusMap +
        // StatusBadgeTagHelper.Variants for exactly the statuses this table can show.
        var STATUS_DISPLAY = {
          Migrated: 'Migrated', DuplicateSd: 'Duplicate SD', Overwritten: 'Overwritten',
          InsertedAsNew: 'Inserted', Failed: 'Failed Migration', ConfirmedDuplicate: 'Confirmed Duplicate',
          Pending: 'Pending',
        };
        var STATUS_VARIANT = {
          Migrated: 'ok', 'Duplicate SD': 'err', Overwritten: 'info', Inserted: 'info',
          'Failed Migration': 'err', 'Confirmed Duplicate': 'neutral', Pending: 'neutral',
        };
        var statusText = STATUS_DISPLAY[doc.status] || doc.status;
        var badge = document.createElement('span');
        badge.className = 'badge badge-' + (STATUS_VARIANT[statusText] || 'neutral');
        badge.textContent = statusText;
        statusTd.appendChild(badge);

        var reasonTd = document.createElement('td');
        reasonTd.className = 'small cell-wrap';
        reasonTd.textContent = doc.reasonForFailing || '—';

        row.appendChild(idxTd);
        row.appendChild(typeTd);
        row.appendChild(fileTd);
        row.appendChild(sourceTd);
        row.appendChild(targetTd);
        row.appendChild(validationTd);
        row.appendChild(statusTd);
        row.appendChild(reasonTd);
        listEl.appendChild(row);
      }
    }

    function selectDocument(index) {
      if (index < 0 || index >= documents.length) return;
      activeIndex = index;
      render();
    }

    if (listEl) {
      listEl.addEventListener('click', function (e) {
        if (e.target.closest('[data-doc-type-select]') || e.target.closest('.dropdown-menu') || e.target.tagName === 'INPUT') return;
        var item = e.target.closest('[data-doc-index]');
        if (!item) return;
        selectDocument(Number(item.getAttribute('data-doc-index')));
      });

      listEl.addEventListener('change', function (e) {
        var select = e.target.closest('[data-doc-type-select]');
        if (!select) return;
        var index = Number(select.getAttribute('data-doc-index'));
        var doc = documents[index];
        var code = select.value;
        if (!code) {
          delete pendingDocumentChanges[doc.id];
        } else {
          var opt = select.options[select.selectedIndex];
          pendingDocumentChanges[doc.id] = { code: code, name: opt.getAttribute('data-name') || opt.textContent };
        }
        syncDocumentCorrectionsField();
        renderDocumentList();
      });
    }

    prevBtn.addEventListener('click', function () { selectDocument(activeIndex - 1); });
    nextBtn.addEventListener('click', function () { selectDocument(activeIndex + 1); });

    renderDocumentList();
    render();

    wireSaveCloseReadyForMigration();

    // --- Save / Close / Ready for Migration ---
    function wireSaveCloseReadyForMigration() {
      function doSave(silent) {
        if (!isDirty()) {
          if (!silent) toast('No changes detected.');
          return Promise.resolve(true);
        }

        var formData = new FormData(detailsForm);
        return fetch('/Migrations/Save/' + migrationId, { method: 'POST', body: formData, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
          .then(function (r) {
            if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
            if (!r.ok) throw new Error('Save request failed with status ' + r.status);
            return r.json();
          })
          .then(function (data) {
            if (!data) return false;
            if (!data.success) {
              toast(data.message || 'Unable to save changes.', 'error');
              return false;
            }
            pendingDocumentChanges = {};
            if (documentCorrectionsField) documentCorrectionsField.value = '';
            if (rdCodeField) rdCodeField.value = data.rdCode || rdCodeField.value;
            var rdNameText = document.getElementById('migrationRdNameText');
            if (rdNameText) rdNameText.textContent = data.rdName || '—';
            if (!data.isTargetRdNotIdentified) {
              var rdCodeHostEl = document.getElementById('migrationRdCodeComboboxHost');
              if (rdCodeHostEl) {
                rdCodeHostEl.parentElement.innerHTML = '<div class="field-label">RD Code</div><div class="mt-1 fw-medium cell-wrap">' + (data.rdCode || '—') + '</div>';
              }
            }
            var readyBtn = document.getElementById('migrationReadyForMigrationBtn');
            if (readyBtn) readyBtn.classList.toggle('d-none', !data.canMarkReadyForMigration);
            targetRdNotIdentified = !!data.isTargetRdNotIdentified;
            if (data.documents) {
              documents = data.documents;
              if (listEl) { renderDocumentList(); render(); }
            }
            updateReadyForMigrationButtonState();
            refreshActionHistory();
            snapshot = currentValues();
            if (!silent) toast('Saved Successfully.', 'success');
            return true;
          })
          .catch(function (err) {
            toast('Unable to save changes. Please try again.', 'error');
            if (window.console) console.error('Migration Details Save failed', err);
            return false;
          });
      }

      var saveBtn = document.getElementById('migrationSaveBtn');
      if (saveBtn) saveBtn.addEventListener('click', function () { doSave(false); });

      // --- Ready for Migration (spec section 5) ---
      var readyForMigrationBtn = document.getElementById('migrationReadyForMigrationBtn');
      if (readyForMigrationBtn) {
        var rfmModalEl = document.getElementById('confirmDialog');
        var rfmModal = rfmModalEl && window.bootstrap ? bootstrap.Modal.getOrCreateInstance(rfmModalEl) : null;
        var rfmConfirmBtn = document.getElementById('confirmDialogConfirmBtn');
        var rfmCancelBtn = document.getElementById('confirmDialogCancelBtn');
        var rfmPendingHandler = null;

        function clearPendingRfmConfirm() {
          if (rfmPendingHandler) {
            rfmConfirmBtn.removeEventListener('click', rfmPendingHandler);
            rfmPendingHandler = null;
          }
        }
        if (rfmModalEl) rfmModalEl.addEventListener('hidden.bs.modal', clearPendingRfmConfirm);

        readyForMigrationBtn.addEventListener('click', function () {
          if (!rfmModal || !rfmConfirmBtn || readyForMigrationBtn.disabled) return;
          document.getElementById('confirmDialogMessage').textContent =
            'Are you sure you want to mark this Entry Record as Ready for Migration? This action will place the Entry Record in the migration queue.';
          rfmConfirmBtn.className = 'btn btn-navy';
          rfmConfirmBtn.textContent = 'Confirm';
          if (rfmCancelBtn) rfmCancelBtn.textContent = 'Cancel';
          clearPendingRfmConfirm();
          rfmPendingHandler = function () {
            if (rfmConfirmBtn.disabled) return;
            rfmConfirmBtn.disabled = true;
            readyForMigrationBtn.disabled = true;
            rfmModal.hide();
            doSave(true).then(function (saved) {
              if (!saved) { rfmConfirmBtn.disabled = false; readyForMigrationBtn.disabled = false; return; }
              var body = new URLSearchParams({ __RequestVerificationToken: token });
              fetch('/Migrations/ReadyForMigration/' + migrationId, { method: 'POST', body: body, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (r) {
                  if (window.handleUnauthenticatedAjax && window.handleUnauthenticatedAjax(r)) return null;
                  return r.json();
                })
                .then(function (data) {
                  if (!data) return;
                  if (!data.success) {
                    toast(data.message || 'Unable to mark this Entry Record as Ready for Migration.', 'error');
                    rfmConfirmBtn.disabled = false;
                    readyForMigrationBtn.disabled = false;
                    return;
                  }
                  toast(data.message, 'success');
                  setTimeout(function () { window.location.href = '/Migrations'; }, 900);
                })
                .catch(function () {
                  toast('Unable to mark this Entry Record as Ready for Migration. Please try again.', 'error');
                  rfmConfirmBtn.disabled = false;
                  readyForMigrationBtn.disabled = false;
                });
            });
          };
          rfmConfirmBtn.addEventListener('click', rfmPendingHandler);
          rfmModal.show();
        });
      }

      // --- Close / unsaved-changes (spec section 12) ---
      var unsavedModalEl = document.getElementById('migrationUnsavedModal');
      var unsavedModal = unsavedModalEl && window.bootstrap ? new bootstrap.Modal(unsavedModalEl) : null;

      function closePage() { window.location.href = '/Migrations'; }

      var closeBtn = document.getElementById('migrationCloseBtn');
      if (closeBtn) {
        closeBtn.addEventListener('click', function () {
          if (isDirty() && unsavedModal) {
            unsavedModal.show();
            return;
          }
          closePage();
        });
      }

      var saveAndCloseBtn = document.getElementById('migrationSaveAndCloseBtn');
      if (saveAndCloseBtn) {
        saveAndCloseBtn.addEventListener('click', function () {
          if (unsavedModal) unsavedModal.hide();
          doSave(true).then(function (saved) { if (saved) closePage(); });
        });
      }

      var discardAndCloseBtn = document.getElementById('migrationDiscardAndCloseBtn');
      if (discardAndCloseBtn) {
        discardAndCloseBtn.addEventListener('click', function () {
          if (unsavedModal) unsavedModal.hide();
          closePage();
        });
      }
    }
  });
})();
