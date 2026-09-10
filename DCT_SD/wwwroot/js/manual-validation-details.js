// Wires the Manual Validation Details page: document viewer navigation, the RD Code -> RD
// Name live lookup, Retrieve Title Sequence, and the Save / Close (with remarks) / Migrate
// flows with unsaved-changes tracking. Mirrors the React frontend's
// ManualValidationDetailsPage.tsx behavior on top of the new server-rendered form.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    var form = document.getElementById('mvDetailsForm');
    if (!form) return;

    var recordId = document.getElementById('mvRecordId').value;
    var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';

    var registryOffices = JSON.parse((document.getElementById('mvRegistryOfficesData') || {}).textContent || '[]');
    var documents = JSON.parse((document.getElementById('mvDocumentsData') || {}).textContent || '[]');
    var documentTypes = JSON.parse((document.getElementById('mvDocumentTypesData') || {}).textContent || '[]');

    var fieldIds = ['mvRdCode', 'mvEntry', 'mvTitle', 'mvTitleType', 'mvPlan', 'mvBlock', 'mvLot', 'mvTitleSeq', 'mvRdName',
      'mvDocumentChangeIndex', 'mvDocumentChangeCode', 'mvDocumentChangeName'];
    var fieldEls = {};
    fieldIds.forEach(function (id) {
      fieldEls[id] = document.getElementById(id);
    });

    function currentValues() {
      var v = {};
      fieldIds.forEach(function (id) {
        v[id] = fieldEls[id].value || '';
      });
      return v;
    }

    var snapshot = currentValues();

    // Assigned inside the "Document viewer" block below (only when there are documents to show);
    // declared here as plain vars - not `function` declarations - so they stay reachable from
    // doSave() and the discard-without-saving handler further down, which sit outside that block.
    var applySavedDocuments = null;
    var revertPendingDocumentChange = null;

    function isDirty() {
      var current = currentValues();
      return fieldIds.some(function (id) {
        return current[id] !== snapshot[id];
      });
    }

    function toast(message, variant) {
      if (window.showToast) window.showToast(message, variant);
    }

    // --- RD Code -> RD Name live lookup ---
    if (fieldEls.mvRdCode) {
      fieldEls.mvRdCode.addEventListener('input', function () {
        var match = registryOffices.find(function (o) {
          return o.code === fieldEls.mvRdCode.value.trim();
        });
        fieldEls.mvRdName.value = match ? match.name : '';
      });
    }

    // Editing Title/TitleType/Plan/Block/Lot invalidates any previously retrieved sequence.
    ['mvTitle', 'mvTitleType', 'mvPlan', 'mvBlock', 'mvLot'].forEach(function (id) {
      var el = fieldEls[id];
      if (!el) return;
      el.addEventListener('input', function () {
        fieldEls.mvTitleSeq.value = '';
      });
      el.addEventListener('change', function () {
        fieldEls.mvTitleSeq.value = '';
      });
    });

    // --- Retrieve Title Sequence ---
    // Matching is staged server-side: RD Code + Title Number + Title Type first; if that's
    // still ambiguous, Plan/Block/Lot narrow it further; if it's *still* ambiguous (a genuinely
    // Repeating Title Number), the server returns every remaining candidate instead of guessing,
    // and this shows them in the shared #ajaxModal for manual selection.
    var retrieveBtn = document.getElementById('mvRetrieveTitleSeqBtn');
    if (retrieveBtn) {
      var rtnModalEl = document.getElementById('ajaxModal');
      var rtnModalContentEl = document.getElementById('ajaxModalContent');
      var rtnModal = rtnModalEl && window.bootstrap ? bootstrap.Modal.getOrCreateInstance(rtnModalEl) : null;

      function showRepeatingTitleNumberModal(candidates) {
        if (!rtnModalContentEl || !rtnModal) return;

        rtnModalContentEl.innerHTML =
          '<div class="modal-header">' +
          '<h5 class="modal-title">Repeating Title Number</h5>' +
          '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>' +
          '</div>' +
          '<div class="modal-body">' +
          '<div class="table-responsive"><table class="table table-hover align-middle">' +
          '<thead><tr><th>RD Code</th><th>Title Number</th><th>Title Type</th><th>Plan Number</th><th>Block Number</th><th>Lot Number</th><th>Title Sequence Number</th><th>Action</th></tr></thead>' +
          '<tbody id="mvRtnCandidatesBody"></tbody>' +
          '</table></div>' +
          '</div>' +
          '<div class="modal-footer"><button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Close</button></div>';

        var tbody = document.getElementById('mvRtnCandidatesBody');
        candidates.forEach(function (c) {
          var row = document.createElement('tr');
          [c.rdCode, c.title, c.titleType, c.plan, c.block, c.lot, c.sequence].forEach(function (value) {
            var td = document.createElement('td');
            td.textContent = value || '—';
            row.appendChild(td);
          });
          var actionTd = document.createElement('td');
          var selectBtn = document.createElement('button');
          selectBtn.type = 'button';
          selectBtn.className = 'btn btn-outline-secondary btn-sm';
          selectBtn.textContent = 'Select';
          selectBtn.addEventListener('click', function () {
            fieldEls.mvTitleSeq.value = c.sequence;
            rtnModal.hide();
            toast('Title Sequence retrieved successfully.', 'success');
          });
          actionTd.appendChild(selectBtn);
          row.appendChild(actionTd);
          tbody.appendChild(row);
        });

        rtnModal.show();
      }

      retrieveBtn.addEventListener('click', function () {
        var rdCode = fieldEls.mvRdCode.value.trim();
        var title = fieldEls.mvTitle.value.trim();
        var titleType = fieldEls.mvTitleType.value.trim();
        var plan = fieldEls.mvPlan.value.trim();
        var block = fieldEls.mvBlock.value.trim();
        var lot = fieldEls.mvLot.value.trim();

        if (!title || !titleType) {
          fieldEls.mvTitleSeq.value = '';
          toast('No Title Sequence record was found.');
          return;
        }

        var body = new URLSearchParams({ RdCode: rdCode, Title: title, TitleType: titleType, Plan: plan, Block: block, Lot: lot, __RequestVerificationToken: token });
        fetch('/ManualValidation/RetrieveTitleSequence', { method: 'POST', body: body })
          .then(function (r) { return r.json(); })
          .then(function (data) {
            if (data.success) {
              fieldEls.mvTitleSeq.value = data.sequence;
              toast('Title Sequence retrieved successfully.', 'success');
            } else if (data.ambiguous) {
              fieldEls.mvTitleSeq.value = '';
              showRepeatingTitleNumberModal(data.candidates || []);
            } else {
              fieldEls.mvTitleSeq.value = '';
              toast(data.message || 'No Title Sequence record was found.');
            }
          });
      });
    }

    // --- Document viewer ---
    if (documents.length > 0) {
      var container = document.getElementById('mvViewer');
      var viewer = window.DctDocViewer.create(container);
      var headerRightEl = container.querySelector('[data-viewer-header-right]');
      var filenameEl = container.querySelector('[data-viewer-filename]');
      var listEl = document.getElementById('mvDocumentList');
      var docTypeText = document.getElementById('mvDocTypeText');
      var docTypeSelect = document.getElementById('mvDocTypeSelect');
      var activeIndex = 0;

      // A pending "Others" -> real Document Type correction for at most one document at a time
      // (matching what Save accepts) - { index: doc.id, code, name } or null. Never sent to the
      // server except as part of Save itself; cleared on a successful save or on discard.
      var pendingChange = null;

      if (docTypeSelect) {
        documentTypes.forEach(function (t) {
          var opt = document.createElement('option');
          opt.value = t.code;
          opt.textContent = t.name;
          opt.setAttribute('data-name', t.name);
          docTypeSelect.appendChild(opt);
        });
      }

      function effectiveDocumentId(doc) {
        return pendingChange && pendingChange.index === doc.id ? pendingChange.code : doc.documentId;
      }

      function effectiveDocumentName(doc) {
        return pendingChange && pendingChange.index === doc.id ? pendingChange.name : doc.documentName;
      }

      // Mirrors the server's ApplyDocumentTypeChange naming rule (<DocumentID>_<DocumentName><ext>)
      // so the Image File Name a pending correction will produce is visible immediately on
      // selection, per the acceptance criteria - this is only a preview: the actual rename (with
      // its collision-suffix fallback, which depends on what's already on disk) happens on Save.
      function generatedFileName(doc, code, name) {
        var match = /\.[^.]+$/.exec(doc.renamedFileName || '');
        var ext = match ? match[0] : '';
        return code + '_' + name + ext;
      }

      function effectiveFileName(doc) {
        return pendingChange && pendingChange.index === doc.id
          ? generatedFileName(doc, pendingChange.code, pendingChange.name)
          : doc.renamedFileName;
      }

      // Rebuilds the Supporting Documents table (never raw HTML from doc data - built via
      // textContent) so a pending correction's preview name, and any Save-time reshuffle from
      // renaming (the list is sorted by Document Name), are both reflected without a page reload.
      function renderDocumentList() {
        if (!listEl) return;
        listEl.innerHTML = '';
        documents.forEach(function (doc, index) {
          var row = document.createElement('tr');
          row.setAttribute('role', 'button');
          row.setAttribute('data-doc-index', String(index));
          row.style.cursor = 'pointer';
          row.classList.toggle('table-active', index === activeIndex);

          var idxTd = document.createElement('td');
          idxTd.className = 'small';
          idxTd.textContent = String(index + 1);

          var fileTd = document.createElement('td');
          fileTd.className = 'small font-monospace';
          fileTd.textContent = effectiveFileName(doc);

          var nameTd = document.createElement('td');
          nameTd.className = 'small fw-medium';
          nameTd.textContent = effectiveDocumentName(doc);

          row.appendChild(idxTd);
          row.appendChild(fileTd);
          row.appendChild(nameTd);
          listEl.appendChild(row);
        });
      }

      // Shows the read-only Document Type text for anything already classified, or the
      // CodeLookups-backed dropdown when the current (or currently pending) Document Type is
      // "Others" - per the acceptance criteria, only Others documents are ever correctable.
      function updateDocTypeControls() {
        var doc = documents[activeIndex];
        var docId = effectiveDocumentId(doc);
        var isOthers = (docId || '').toUpperCase() === 'OTHERS';

        if (docTypeText) {
          docTypeText.textContent = effectiveDocumentName(doc);
          docTypeText.classList.toggle('d-none', isOthers);
        }
        if (docTypeSelect) {
          docTypeSelect.classList.toggle('d-none', !isOthers);
          docTypeSelect.value = pendingChange && pendingChange.index === doc.id ? pendingChange.code : '';
        }
      }

      function setPendingChangeFields() {
        document.getElementById('mvDocumentChangeIndex').value = pendingChange ? pendingChange.index : '';
        document.getElementById('mvDocumentChangeCode').value = pendingChange ? pendingChange.code : '';
        document.getElementById('mvDocumentChangeName').value = pendingChange ? pendingChange.name : '';
      }

      if (docTypeSelect) {
        docTypeSelect.addEventListener('change', function () {
          var doc = documents[activeIndex];
          var code = docTypeSelect.value;
          if (!code) {
            pendingChange = null;
          } else {
            var opt = docTypeSelect.options[docTypeSelect.selectedIndex];
            pendingChange = { index: doc.id, code: code, name: opt.getAttribute('data-name') || opt.textContent };
          }
          setPendingChangeFields();
          renderDocumentList();
          updateDocTypeControls();
          // Update just the filename label - not a full viewer.load(), which would reset
          // zoom/rotation/pan for no reason since the image itself hasn't changed.
          if (filenameEl) filenameEl.textContent = effectiveFileName(documents[activeIndex]);
        });
      }

      function renderDoc() {
        var doc = documents[activeIndex];
        // doc.id is the 1-based position synthesized server-side (ManualValidationDocumentDto.Id);
        // the actual image file is always looked up server-side from that document's imagePath
        // in DocumentsJson - never a client-constructed path. The image itself only actually
        // changes once Save succeeds, so this always loads the ORIGINAL file - but the filename
        // label shown alongside it previews the pending correction's generated Image File Name,
        // per the acceptance criteria ("system shall automatically generate the corresponding
        // Image File Name" on selection).
        viewer.load('/ManualValidation/DocumentImage?id=' + recordId + '&documentId=' + doc.id, effectiveFileName(doc), { fitOnLoad: false });
        viewer.setNavDisabled(activeIndex === 0, activeIndex === documents.length - 1);
        if (headerRightEl) headerRightEl.textContent = 'Image ' + (activeIndex + 1) + ' of ' + documents.length;
        updateDocTypeControls();
        if (listEl) {
          Array.prototype.forEach.call(listEl.querySelectorAll('[data-doc-index]'), function (el) {
            var isActive = Number(el.getAttribute('data-doc-index')) === activeIndex;
            el.classList.toggle('table-active', isActive);
          });
        }
      }

      function selectDoc(index) {
        if (index < 0 || index >= documents.length) return;
        activeIndex = index;
        renderDoc();
      }

      // Drops any pending Document Type correction and restores the original documents/UI state
      // - used on discard (Close Without Saving) and is also safe to call after a failed Save.
      revertPendingDocumentChange = function () {
        pendingChange = null;
        setPendingChangeFields();
        renderDocumentList();
        updateDocTypeControls();
      };

      // Replaces the local documents array with the server's freshly re-sorted list after a
      // successful save (a rename changes Document Name, which the list/viewer are sorted by),
      // clears the pending-change state, and keeps showing the same document the user was on.
      applySavedDocuments = function (newDocuments) {
        if (!newDocuments || !newDocuments.length) return;
        var current = documents[activeIndex];
        documents = newDocuments;
        pendingChange = null;
        setPendingChangeFields();

        var matchIndex = documents.findIndex(function (d) {
          return current && (d.renamedFileName === current.renamedFileName || d.id === current.id);
        });
        activeIndex = matchIndex >= 0 ? matchIndex : Math.min(activeIndex, documents.length - 1);

        renderDocumentList();
        renderDoc();
      }

      if (listEl) {
        listEl.addEventListener('click', function (e) {
          var item = e.target.closest('[data-doc-index]');
          if (!item) return;
          selectDoc(Number(item.getAttribute('data-doc-index')));
        });
      }

      container.addEventListener('docviewer:prev', function () { selectDoc(activeIndex - 1); });
      container.addEventListener('docviewer:next', function () { selectDoc(activeIndex + 1); });

      renderDoc();
    }

    // --- Remarks history pagination ---
    var remarksContainer = document.getElementById('mvRemarksHistory');

    function loadRemarks(pageNumber) {
      fetch('/ManualValidation/RemarksHistoryInline?id=' + recordId + '&pageNumber=' + pageNumber, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (r) { return r.text(); })
        .then(function (html) { remarksContainer.innerHTML = html; });
    }

    if (remarksContainer) {
      remarksContainer.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-remarks-page]');
        if (!btn) return;
        loadRemarks(btn.getAttribute('data-remarks-page'));
      });
    }

    // --- Save ---
    function doSave(silent) {
      if (!isDirty()) {
        if (!silent) toast('No changes detected.');
        return Promise.resolve(true);
      }

      var formData = new FormData(form);
      return fetch('/ManualValidation/Save/' + recordId, { method: 'POST', body: formData })
        .then(function (r) { return r.json(); })
        .then(function (data) {
          if (!data.success) {
            toast(data.message || 'Unable to save changes.', 'error');
            // Per the acceptance criteria, a failed save must leave the physical file, DocumentsJson
            // and UI exactly as they were - so drop the pending Document Type correction rather than
            // leaving a selection in the UI that implies something was persisted.
            if (typeof revertPendingDocumentChange === 'function') revertPendingDocumentChange();
            return false;
          }
          fieldEls.mvRdName.value = data.rdName || '';
          if (typeof applySavedDocuments === 'function') applySavedDocuments(data.documents);
          snapshot = currentValues();
          applyMissingFields(data.missingFields || []);
          if (!silent) toast('Saved Successfully.', 'success');
          loadRemarks(1);
          return true;
        });
    }

    function applyMissingFields(missing) {
      var map = { rdCode: 'mvRdCode', rdName: 'mvRdName', entry: 'mvEntry', title: 'mvTitle', titleType: 'mvTitleType', plan: 'mvPlan', block: 'mvBlock', lot: 'mvLot' };
      var missingSet = {};
      missing.forEach(function (k) { missingSet[k] = true; });
      Object.keys(map).forEach(function (key) {
        var el = fieldEls[map[key]];
        if (!el) return;
        var wrapper = el.closest('.mb-3');
        if (wrapper) wrapper.classList.toggle('missing-field', !!missingSet[key]);
      });
      var titleSeqWrapper = fieldEls.mvTitleSeq.closest('.mb-3');
      if (titleSeqWrapper) titleSeqWrapper.classList.toggle('missing-field', !!(missingSet.titleSequence || missingSet.titleSeq));
    }

    document.getElementById('mvSaveBtn').addEventListener('click', function () {
      doSave(false);
    });

    // --- Migrate ---
    document.getElementById('mvMigrateBtn').addEventListener('click', function () {
      doSave(true).then(function (saved) {
        if (!saved) return;
        var body = new URLSearchParams({ __RequestVerificationToken: token });
        fetch('/ManualValidation/Migrate/' + recordId, { method: 'POST', body: body })
          .then(function (r) { return r.json(); })
          .then(function (data) {
            if (!data.success) {
              toast(data.message || 'Please complete all mandatory fields before proceeding with migration.', 'error');
              return;
            }
            toast(data.message, 'success');
            setTimeout(function () { window.location.href = '/ManualValidation'; }, 900);
          });
      });
    });

    // --- Close (with remarks) / unsaved-changes flow ---
    var closeModalEl = document.getElementById('mvCloseModal');
    var unsavedModalEl = document.getElementById('mvUnsavedModal');
    var closeModal = window.bootstrap ? new bootstrap.Modal(closeModalEl) : null;
    var unsavedModal = window.bootstrap ? new bootstrap.Modal(unsavedModalEl) : null;
    var closeRemarksText = document.getElementById('mvCloseRemarksText');
    var closeRemarksError = document.getElementById('mvCloseRemarksError');

    function openCloseFlow() {
      if (isDirty()) {
        unsavedModal.show();
        return;
      }
      closeRemarksText.value = '';
      closeRemarksError.classList.add('d-none');
      closeModal.show();
    }

    document.getElementById('mvCloseBtn').addEventListener('click', openCloseFlow);

    document.getElementById('mvCloseConfirmBtn').addEventListener('click', function () {
      var val = closeRemarksText.value.trim();
      if (!val) {
        closeRemarksError.classList.remove('d-none');
        return;
      }
      var body = new URLSearchParams({ remarks: val, __RequestVerificationToken: token });
      fetch('/ManualValidation/Close/' + recordId, { method: 'POST', body: body })
        .then(function (r) { return r.json(); })
        .then(function (data) {
          if (!data.success) {
            toast(data.message || 'Unable to close this record.', 'error');
            return;
          }
          closeModal.hide();
          toast(data.message, 'success');
          setTimeout(function () { window.location.href = '/ManualValidation'; }, 700);
        });
    });

    document.getElementById('mvSaveAndCloseBtn').addEventListener('click', function () {
      unsavedModal.hide();
      doSave(true).then(function (saved) {
        if (saved) openCloseFlow();
      });
    });

    document.getElementById('mvDiscardAndCloseBtn').addEventListener('click', function () {
      unsavedModal.hide();
      fieldIds.forEach(function (id) {
        fieldEls[id].value = snapshot[id];
      });
      if (typeof revertPendingDocumentChange === 'function') revertPendingDocumentChange();
      closeRemarksText.value = '';
      closeRemarksError.classList.add('d-none');
      closeModal.show();
    });
  });
})();
