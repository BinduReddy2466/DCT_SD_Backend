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

    var fieldIds = ['mvRdCode', 'mvEntry', 'mvTitle', 'mvTitleType', 'mvPlan', 'mvBlock', 'mvLot', 'mvTitleSeq', 'mvRdName'];
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
    var retrieveBtn = document.getElementById('mvRetrieveTitleSeqBtn');
    if (retrieveBtn) {
      retrieveBtn.addEventListener('click', function () {
        var title = fieldEls.mvTitle.value.trim();
        var titleType = fieldEls.mvTitleType.value.trim();
        var plan = fieldEls.mvPlan.value.trim();
        var block = fieldEls.mvBlock.value.trim();
        var lot = fieldEls.mvLot.value.trim();

        if (!title || !titleType || !plan || !block || !lot) {
          fieldEls.mvTitleSeq.value = '';
          toast('No matching title sequence found for the title record.');
          return;
        }

        var body = new URLSearchParams({ Title: title, TitleType: titleType, Plan: plan, Block: block, Lot: lot, __RequestVerificationToken: token });
        fetch('/ManualValidation/RetrieveTitleSequence', { method: 'POST', body: body })
          .then(function (r) { return r.json(); })
          .then(function (data) {
            if (data.success) {
              fieldEls.mvTitleSeq.value = data.sequence;
              toast('Title Sequence retrieved successfully.', 'success');
            } else {
              fieldEls.mvTitleSeq.value = '';
              toast('No matching title sequence found for the title record.');
            }
          });
      });
    }

    // --- Document viewer ---
    if (documents.length > 0) {
      var container = document.getElementById('mvViewer');
      var viewer = window.DctDocViewer.create(container);
      var headerRightEl = container.querySelector('[data-viewer-header-right]');
      var listEl = document.getElementById('mvDocumentList');
      var activeIndex = 0;

      function renderDoc() {
        var doc = documents[activeIndex];
        viewer.load(window.dctPlaceholderImage(doc.fileName), doc.fileName, { fitOnLoad: false });
        viewer.setNavDisabled(activeIndex === 0, activeIndex === documents.length - 1);
        if (headerRightEl) headerRightEl.textContent = 'Image ' + (activeIndex + 1) + ' of ' + documents.length;
        if (listEl) {
          Array.prototype.forEach.call(listEl.querySelectorAll('[data-doc-index]'), function (el) {
            var isActive = Number(el.getAttribute('data-doc-index')) === activeIndex;
            el.style.background = isActive ? '#EEF2F8' : '';
          });
        }
      }

      function selectDoc(index) {
        if (index < 0 || index >= documents.length) return;
        activeIndex = index;
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
            return false;
          }
          fieldEls.mvRdName.value = data.rdName || '';
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
      closeRemarksText.value = '';
      closeRemarksError.classList.add('d-none');
      closeModal.show();
    });
  });
})();
