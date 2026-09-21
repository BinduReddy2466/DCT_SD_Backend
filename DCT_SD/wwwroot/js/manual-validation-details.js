// Wires the Manual Validation Details page: document viewer navigation, the RD Code -> RD
// Name live lookup, Retrieve Title Sequence, and the Save / Close (with remarks) / Ready for
// Migration flows with unsaved-changes tracking. Mirrors the React frontend's
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

    // One or more Title Record rows (see Details.cshtml's Title Record(s) table) - every row
    // sharing the opened record's exact EntryNumbersCsv, combined server-side into this one
    // Details view. Always at least 1 (the opened record itself when it has no group siblings),
    // so a record with no grouping siblings behaves exactly as it always has.
    var titleRecordRows = Array.prototype.slice.call(document.querySelectorAll('[data-title-record-row]'));
    var titleRecordFieldSuffixes = ['Title', 'TitleType', 'Plan', 'Block', 'Lot', 'TitleSeq'];
    var titleRecordFieldIds = [];
    titleRecordRows.forEach(function (row, i) {
      titleRecordFieldSuffixes.forEach(function (suffix) {
        titleRecordFieldIds.push('mv' + suffix + '_' + i);
      });
    });

    var fieldIds = ['mvRdCode', 'mvEntry', 'mvRdName', 'mvDocumentChangesJson'].concat(titleRecordFieldIds);
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

    // Editing Title/TitleType/Plan/Block/Lot invalidates any previously retrieved sequence - for
    // its own Title Record row only, not the whole group.
    titleRecordRows.forEach(function (row, i) {
      var seqEl = document.getElementById('mvTitleSeq_' + i);
      if (!seqEl) return;
      ['Title', 'TitleType', 'Plan', 'Block', 'Lot'].forEach(function (suffix) {
        var el = document.getElementById('mv' + suffix + '_' + i);
        if (!el) return;
        el.addEventListener('input', function () { seqEl.value = ''; });
        el.addEventListener('change', function () { seqEl.value = ''; });
      });
    });

    // --- Retrieve Title Sequence ---
    // Matching is staged server-side: RD Code + Title Number + Title Type first; if that's
    // still ambiguous, Plan/Block/Lot narrow it further; if it's *still* ambiguous (a genuinely
    // Repeating Title Number), the server returns every remaining candidate instead of guessing,
    // and this shows them in the shared #ajaxModal for manual selection. One button per Title
    // Record row, all sharing the one modal (only one can be open at a time anyway) and the one
    // General Information RD Code field.
    if (titleRecordRows.length > 0) {
      var rtnModalEl = document.getElementById('ajaxModal');
      var rtnModalContentEl = document.getElementById('ajaxModalContent');
      var rtnModal = rtnModalEl && window.bootstrap ? bootstrap.Modal.getOrCreateInstance(rtnModalEl) : null;

      var showRepeatingTitleNumberModal = function (candidates, onSelect) {
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
            onSelect(c.sequence);
            rtnModal.hide();
            toast('Title Sequence retrieved successfully.', 'success');
          });
          actionTd.appendChild(selectBtn);
          row.appendChild(actionTd);
          tbody.appendChild(row);
        });

        rtnModal.show();
      };

      titleRecordRows.forEach(function (row, i) {
        var retrieveBtn = document.querySelector('[data-mv-retrieve-title-seq][data-title-record-index="' + i + '"]');
        if (!retrieveBtn) return;

        var titleEl = document.getElementById('mvTitle_' + i);
        var titleTypeEl = document.getElementById('mvTitleType_' + i);
        var planEl = document.getElementById('mvPlan_' + i);
        var blockEl = document.getElementById('mvBlock_' + i);
        var lotEl = document.getElementById('mvLot_' + i);
        var seqEl = document.getElementById('mvTitleSeq_' + i);

        retrieveBtn.addEventListener('click', function () {
          var rdCode = fieldEls.mvRdCode.value.trim();
          var title = titleEl.value.trim();
          var titleType = titleTypeEl.value.trim();
          var plan = planEl.value.trim();
          var block = blockEl.value.trim();
          var lot = lotEl.value.trim();

          if (!title || !titleType) {
            seqEl.value = '';
            toast('No Title Sequence record was found.');
            return;
          }

          var body = new URLSearchParams({ RdCode: rdCode, Title: title, TitleType: titleType, Plan: plan, Block: block, Lot: lot, __RequestVerificationToken: token });
          fetch('/ManualValidation/RetrieveTitleSequence', { method: 'POST', body: body })
            .then(function (r) { return r.json(); })
            .then(function (data) {
              if (data.success) {
                seqEl.value = data.sequence;
                toast('Title Sequence retrieved successfully.', 'success');
              } else if (data.ambiguous) {
                seqEl.value = '';
                showRepeatingTitleNumberModal(data.candidates || [], function (sequence) { seqEl.value = sequence; });
              } else {
                seqEl.value = '';
                toast(data.message || 'No Title Sequence record was found.');
              }
            });
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
      var activeIndex = 0;

      // Zero or more pending "Others" -> real Document Type corrections, keyed by doc.id (one at
      // most per document - selecting again for the same document just replaces its own pending
      // choice). Never sent to the server except as part of Save itself; cleared entirely on a
      // successful save or on discard. { [docId]: { code, name } }
      var pendingChanges = {};

      function effectiveDocumentName(doc) {
        var p = pendingChanges[doc.id];
        return p ? p.name : doc.documentName;
      }

      function escapeRegExp(s) {
        return (s || '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      }

      // Mirrors the server's SanitizeForFileName: a CodeLookups Document Name can contain
      // characters that aren't valid in a file name (e.g. a "/"), so the generated file name
      // replaces them with "_" - same substitution the OCR pipeline's own file names already use.
      function sanitizeForFileName(s) {
        return (s || '').replace(/[\/\\]/g, '_');
      }

      // Mirrors the server's NextDocumentSequenceNumber: the highest existing "_<N>" sequence
      // number already used by this record's documents that share this exact Document ID + Name
      // (matching "<DocumentID>_<DocumentName>_<N>.<ext>"), plus 1 - or 1 if none match. Looks at
      // ORIGINAL (persisted) documents first, then walks any OTHER pending changes targeting the
      // same type in a stable ascending-by-id order (matching the order Save submits and applies
      // them in) up to `excludeDocId`, so two documents pending the same type in the browser
      // preview as sequential numbers exactly like they will after Save - without needing to
      // recursively resolve each other's preview (which target the same type would deadlock).
      function nextDocumentSequenceNumber(excludeDocId, code, name) {
        var pattern = new RegExp('^' + escapeRegExp(sanitizeForFileName(code)) + '_' + escapeRegExp(sanitizeForFileName(name)) + '_(\\d+)\\.[^.]+$', 'i');
        var max = 0;
        documents.forEach(function (d) {
          if ((d.documentId || '').toLowerCase() !== code.toLowerCase()) return;
          if ((d.documentName || '').toLowerCase() !== name.toLowerCase()) return;
          var m = pattern.exec(d.renamedFileName || '');
          if (m) {
            var n = parseInt(m[1], 10);
            if (!isNaN(n) && n > max) max = n;
          }
        });

        var pendingIds = Object.keys(pendingChanges)
          .map(Number)
          .filter(function (id) {
            var p = pendingChanges[id];
            return p && p.code.toLowerCase() === code.toLowerCase() && p.name.toLowerCase() === name.toLowerCase();
          })
          .sort(function (a, b) { return a - b; });

        for (var i = 0; i < pendingIds.length; i++) {
          if (pendingIds[i] === excludeDocId) break;
          max++;
        }

        return max + 1;
      }

      // Mirrors the server's ApplyDocumentTypeChange naming rule
      // (<DocumentID>_<DocumentName>_<SequenceNumber><ext>) so the Image File Name a pending
      // correction will produce is visible immediately on selection, per the acceptance criteria
      // - this is only a preview: the actual rename happens on Save.
      function generatedFileName(doc, code, name) {
        var match = /\.[^.]+$/.exec(doc.renamedFileName || '');
        var ext = match ? match[0] : '';
        var seq = nextDocumentSequenceNumber(doc.id, code, name);
        return sanitizeForFileName(code) + '_' + sanitizeForFileName(name) + '_' + seq + ext;
      }

      function effectiveFileName(doc) {
        var p = pendingChanges[doc.id];
        return p ? generatedFileName(doc, p.code, p.name) : doc.renamedFileName;
      }

      // Builds the searchable Document Type combobox around one already-populated, already-valued
      // <select> (see renderDocumentList) - a visible text input the user can either click to
      // browse every option (native dropdown behavior) or type into to filter by name, plus the
      // filtered list itself. The <select> stays hidden but present and fully functional: every
      // selection - by mouse or keyboard - sets select.value and dispatches a real 'change' event
      // on it, so the existing delegated change handler (pendingChanges, Save, DocumentsJson,
      // filename sequencing, etc.) drives off the exact same element/event it always has, entirely
      // unaware this UI exists. Returns the wrapper element to place next to the <select> in the DOM.
      function wireDocTypeCombobox(select) {
        var container = document.createElement('div');
        container.className = 'position-relative';
        container.style.minWidth = '200px';
        container.style.maxWidth = '260px';

        var input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-control form-control-sm';
        input.setAttribute('autocomplete', 'off');
        input.placeholder = 'Select';

        var menu = document.createElement('div');
        menu.className = 'dropdown-menu p-0';
        menu.style.maxHeight = '220px';
        menu.style.overflowY = 'auto';
        menu.style.width = '100%';

        var activeIndex = -1;
        var currentMatches = [];

        function nameForCode(code) {
          if (!code) return '';
          var match = documentTypes.filter(function (t) { return t.code === code; })[0];
          return match ? match.name : '';
        }

        function closeMenu() {
          menu.classList.remove('show');
          menu.innerHTML = '';
          activeIndex = -1;
          currentMatches = [];
        }

        function highlight(index) {
          var items = menu.querySelectorAll('[data-doc-type-option]');
          Array.prototype.forEach.call(items, function (el, i) {
            el.classList.toggle('active', i === index);
          });
          if (items[index]) items[index].scrollIntoView({ block: 'nearest' });
        }

        function choose(type) {
          select.value = type.code;
          input.value = type.name;
          closeMenu();
          select.dispatchEvent(new Event('change', { bubbles: true }));
        }

        function openMenu(filterText) {
          var term = (filterText || '').trim().toLowerCase();
          currentMatches = documentTypes.filter(function (t) {
            return !term || t.name.toLowerCase().indexOf(term) !== -1;
          });

          menu.innerHTML = '';
          if (currentMatches.length === 0) {
            var empty = document.createElement('span');
            empty.className = 'dropdown-item-text text-muted small px-2 py-1 d-block';
            empty.textContent = 'No matching Document Type';
            menu.appendChild(empty);
          } else {
            currentMatches.forEach(function (t) {
              var item = document.createElement('button');
              item.type = 'button';
              item.className = 'dropdown-item small';
              item.textContent = t.name;
              item.setAttribute('data-doc-type-option', '');
              // mousedown (not click), with preventDefault, fires and completes before the
              // input's own blur handler - so choosing an option never races with (or gets
              // pre-empted by) the blur-triggered close/snap-back below.
              item.addEventListener('mousedown', function (e) {
                e.preventDefault();
                choose(t);
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

        // Leaving the field without picking an option (typed free text, or just clicked away)
        // never counts as a selection - snap the visible text back to whatever is actually
        // selected, exactly like a native <select> never changes value until something is
        // explicitly chosen.
        input.addEventListener('blur', function () {
          closeMenu();
          input.value = nameForCode(select.value);
        });

        input.value = nameForCode(select.value);
        container.appendChild(input);
        container.appendChild(menu);
        return container;
      }

      // Rebuilds the Supporting Documents table (never raw HTML from doc data - built via
      // textContent/DOM APIs) so a pending correction's preview name, and any Save-time reshuffle
      // from renaming (the list is sorted by Image File Name), are both reflected without a page
      // reload. The Document Type cell is the CodeLookups-backed dropdown itself, inline, when the
      // SERVER says this document can still have its Document Type changed
      // (canChangeDocumentType - true for a document that's still "Others" needing its first
      // correction, or one that was ever corrected from "Others" in the past, so a wrong pick can
      // still be fixed later, even after Save) - plain read-only text otherwise. The dropdown
      // defaults to this document's actual current type (doc.documentId) so its real value stays
      // visible at a glance, unless a pending change is already selected for it, or it's still
      // "OTHERS" and has never had a real type - both cases fall back to the "Select" placeholder.
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
          idxTd.className = 'small text-center';
          idxTd.style.whiteSpace = 'nowrap';
          idxTd.textContent = String(index + 1);

          var fileTd = document.createElement('td');
          fileTd.className = 'small font-monospace';
          fileTd.textContent = effectiveFileName(doc);

          var nameTd = document.createElement('td');
          nameTd.className = 'small fw-medium';
          if (doc.canChangeDocumentType) {
            var wrapper = document.createElement('div');
            wrapper.className = 'd-flex align-items-center gap-1 flex-wrap';

            // The real, hidden <select> stays the single source of truth for the value and the
            // 'change' event - the searchable text input built around it (see
            // wireDocTypeCombobox) only ever drives IT, then dispatches a native 'change' on it,
            // so the existing delegated change handler below (pendingChanges, syncPendingChangesField,
            // Save, etc.) needs no changes at all: it still just reads select.value/selectedIndex
            // exactly as before.
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

            var pending = pendingChanges[doc.id];
            var currentCode = doc.documentId && doc.documentId.toUpperCase() !== 'OTHERS' ? doc.documentId : '';
            select.value = pending ? pending.code : currentCode;
            wrapper.appendChild(wireDocTypeCombobox(select));
            wrapper.appendChild(select);

            // Marks WHICH row is the editable one, independent of what type name it currently
            // shows - without this, a document that was corrected from "Others" to a real type
            // that another, unrelated document already has natively is visually indistinguishable
            // from that unrelated document except for the dropdown itself, which reads as "the
            // dropdown is showing on the wrong row" rather than "this specific document is still
            // correctable because it was once Others."
            var badge = document.createElement('span');
            badge.className = 'badge rounded-pill text-bg-secondary';
            badge.style.fontSize = '0.65rem';
            badge.style.fontWeight = '500';
            badge.textContent = 'Was Others';
            badge.title = 'This document was originally classified as "Others". It can still have its Document Type corrected here, even after being reclassified - other documents of the same type are not affected.';
            wrapper.appendChild(badge);

            nameTd.appendChild(wrapper);
          } else {
            nameTd.textContent = effectiveDocumentName(doc);
          }

          row.appendChild(idxTd);
          row.appendChild(fileTd);
          row.appendChild(nameTd);
          listEl.appendChild(row);
        });
      }

      // The Image Viewer's own Document Type label just mirrors whichever document is currently
      // shown - always read-only there now that the editable control lives inline in the
      // Supporting Documents table (see renderDocumentList).
      function updateDocTypeControls() {
        var doc = documents[activeIndex];
        if (docTypeText) {
          docTypeText.textContent = effectiveDocumentName(doc);
        }
      }

      // Serializes every currently pending change into the one hidden field Save submits, in
      // ascending doc.id order - matching the order the server applies them in, so a document
      // that lands after another one targeting the same type gets the next sequence number.
      function syncPendingChangesField() {
        var ids = Object.keys(pendingChanges).map(Number).sort(function (a, b) { return a - b; });
        var arr = ids.map(function (id) {
          var p = pendingChanges[id];
          return { index: id, code: p.code, name: p.name };
        });
        document.getElementById('mvDocumentChangesJson').value = arr.length ? JSON.stringify(arr) : '';
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

      // Drops every pending Document Type correction and restores the original documents/UI
      // state - used on discard (Close Without Saving) and is also safe to call after a failed
      // Save.
      revertPendingDocumentChange = function () {
        pendingChanges = {};
        syncPendingChangesField();
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
        pendingChanges = {};
        syncPendingChangesField();

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

        // Inline Document Type dropdown, one per eligible row (see renderDocumentList) - handled
        // via delegation since the row/select elements are rebuilt on every render. The click
        // handler above still also fires for a click landing on the select (selecting that row's
        // document into the Image Viewer), which is harmless and lets the user see the image
        // they're reclassifying without an extra click.
        listEl.addEventListener('change', function (e) {
          var select = e.target.closest('[data-doc-type-select]');
          if (!select) return;

          var index = Number(select.getAttribute('data-doc-index'));
          var doc = documents[index];
          var code = select.value;
          if (!code) {
            delete pendingChanges[doc.id];
          } else {
            var opt = select.options[select.selectedIndex];
            pendingChanges[doc.id] = { code: code, name: opt.getAttribute('data-name') || opt.textContent };
          }
          syncPendingChangesField();
          renderDocumentList();
          if (index === activeIndex) {
            updateDocTypeControls();
            // Update just the filename label - not a full viewer.load(), which would reset
            // zoom/rotation/pan for no reason since the image itself hasn't changed.
            if (filenameEl) filenameEl.textContent = effectiveFileName(documents[activeIndex]);
          }
        });
      }

      container.addEventListener('docviewer:prev', function () { selectDoc(activeIndex - 1); });
      container.addEventListener('docviewer:next', function () { selectDoc(activeIndex + 1); });

      renderDocumentList();
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
        .then(function (r) {
          // The server only ever returns JSON from this endpoint for the outcomes it expects
          // (success, or a recognized business/file-system failure - see ManualValidationController.
          // Save); anything else (an unhandled exception, a network-level failure) falls back to
          // the app's generic HTML error page or an empty body, which r.json() can't parse - without
          // checking r.ok/content-type first, that parse failure used to reject this promise with no
          // .catch() anywhere in the chain, so Save would silently do nothing at all in the browser.
          if (!r.ok) {
            throw new Error('Save request failed with status ' + r.status);
          }
          return r.json();
        })
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
          applyTitleRecordMissingFields(data.titleRecords || []);
          if (!silent) toast('Saved Successfully.', 'success');
          loadRemarks(1);
          return true;
        })
        .catch(function (err) {
          toast('Unable to save changes. Please try again.', 'error');
          if (typeof revertPendingDocumentChange === 'function') revertPendingDocumentChange();
          if (window.console) console.error('Manual Validation Save failed', err);
          return false;
        });
    }

    // General Information fields only (rdCode/rdName/entry) - shown once, shared by the whole
    // group, so they're outside the per-Title-Record loop below.
    function applyMissingFields(missing) {
      var map = { rdCode: 'mvRdCode', rdName: 'mvRdName', entry: 'mvEntry' };
      var missingSet = {};
      missing.forEach(function (k) { missingSet[k] = true; });
      Object.keys(map).forEach(function (key) {
        var el = fieldEls[map[key]];
        if (!el) return;
        var wrapper = el.closest('.mb-3');
        if (wrapper) wrapper.classList.toggle('missing-field', !!missingSet[key]);
      });
    }

    // titleRecords is the server's freshly computed per-row missing-field list (same order as
    // the rendered rows - both are the group in ascending Id order), applied row by row so
    // Saving one Title Record's fields doesn't disturb another row's gold outline.
    function applyTitleRecordMissingFields(titleRecords) {
      var map = { title: 'mvTitle', titleType: 'mvTitleType', plan: 'mvPlan', block: 'mvBlock', lot: 'mvLot', titleSequence: 'mvTitleSeq' };
      titleRecords.forEach(function (tr, i) {
        var missingSet = {};
        (tr.missingFields || []).forEach(function (k) { missingSet[k] = true; });
        Object.keys(map).forEach(function (key) {
          var el = document.getElementById(map[key] + '_' + i);
          if (!el) return;
          var wrapper = el.closest('.mb-3');
          if (wrapper) wrapper.classList.toggle('missing-field', !!missingSet[key]);
        });
      });
    }

    document.getElementById('mvSaveBtn').addEventListener('click', function () {
      doSave(false);
    });

    // --- Ready for Migration ---
    // Replaces the old direct-click Migrate action with an explicit confirmation, reusing the
    // one shared #confirmDialog modal (same one rd-config.js's Start Fetching uses) rather than
    // adding a page-specific modal. Confirm still silently saves any pending edits first (doSave)
    // before marking the record ready, matching the old Migrate flow's behavior; Cancel performs
    // no server call at all - the record's Status is left exactly as it was.
    var readyForMigrationBtn = document.getElementById('mvReadyForMigrationBtn');
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

      if (rfmModalEl) {
        rfmModalEl.addEventListener('hidden.bs.modal', clearPendingRfmConfirm);
      }

      readyForMigrationBtn.addEventListener('click', function () {
        if (!rfmModal || !rfmConfirmBtn) return;

        document.getElementById('confirmDialogMessage').textContent = 'Are you sure you want to mark this Entry Record as Ready for Migration?';
        rfmConfirmBtn.className = 'btn btn-navy';
        rfmConfirmBtn.textContent = 'Confirm';
        if (rfmCancelBtn) rfmCancelBtn.textContent = 'Cancel';
        clearPendingRfmConfirm();
        rfmPendingHandler = function () {
          rfmModal.hide();
          doSave(true).then(function (saved) {
            if (!saved) return;
            var body = new URLSearchParams({ __RequestVerificationToken: token });
            fetch('/ManualValidation/ReadyForMigration/' + recordId, { method: 'POST', body: body })
              .then(function (r) { return r.json(); })
              .then(function (data) {
                if (!data.success) {
                  toast(data.message || 'Unable to mark this record as Ready for Migration.', 'error');
                  return;
                }
                toast(data.message, 'success');
                setTimeout(function () { window.location.href = '/ManualValidation'; }, 900);
              });
          });
        };
        rfmConfirmBtn.addEventListener('click', rfmPendingHandler);
        rfmModal.show();
      });
    }

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
