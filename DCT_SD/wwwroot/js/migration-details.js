// Wires the Migration Details page's document list, Document Image Viewer, and
// Overwrite/Insert-as-New actions on top of the generic doc-viewer.js widget. For
// documents in the duplicate family (DuplicateSd/Overwritten/InsertedAsNew), the DCT-SD and
// PHILARIS-RD images are shown side-by-side (two independent doc-viewer instances); all other
// documents show a single DCT-SD viewer.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    if (window.bootstrap) {
      document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
        new window.bootstrap.Tooltip(el);
      });
    }

    var card = document.querySelector('[data-compare-url-base]');
    var dataEl = document.getElementById('migrationDocumentsData');
    if (!card || !dataEl) return;

    var documents = JSON.parse(dataEl.textContent || '[]');
    if (documents.length === 0) return;

    var DUPLICATE_FAMILY = ['DuplicateSd', 'Overwritten', 'InsertedAsNew'];
    var compareUrlBase = card.getAttribute('data-compare-url-base');

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

    function renderFooter(doc) {
      footerEl.innerHTML = '';

      if (doc.status === 'DuplicateSd') {
        var wrap = document.createElement('div');
        wrap.className = 'border-top pt-3 d-flex justify-content-center gap-3';

        var overwriteBtn = document.createElement('button');
        overwriteBtn.type = 'button';
        overwriteBtn.className = 'btn btn-navy px-4';
        overwriteBtn.textContent = 'Overwrite Existing Image';
        overwriteBtn.addEventListener('click', function () {
          document.getElementById('overwriteDocumentId').value = doc.id;
          document.getElementById('overwriteForm').requestSubmit();
        });

        var insertBtn = document.createElement('button');
        insertBtn.type = 'button';
        insertBtn.className = 'btn btn-outline-secondary px-4';
        insertBtn.textContent = 'Insert as New Image';
        insertBtn.addEventListener('click', function () {
          document.getElementById('insertAsNewDocumentId').value = doc.id;
          document.getElementById('insertAsNewForm').requestSubmit();
        });

        wrap.appendChild(overwriteBtn);
        wrap.appendChild(insertBtn);
        footerEl.appendChild(wrap);
      } else if (doc.status === 'Overwritten' || doc.status === 'InsertedAsNew') {
        var info = document.createElement('div');
        info.className = 'text-center small text-muted mt-2';
        var badge = doc.status === 'Overwritten' ? 'Overwritten' : 'Inserted as New';
        info.innerHTML =
          '<div class="mb-2"><span class="badge badge-info">' + badge + '</span></div>' +
          (doc.performedBy ? '<div><strong>Performed By:</strong> ' + doc.performedBy + '</div>' : '') +
          (doc.actionDate ? '<div><strong>Action Date:</strong> ' + new Date(doc.actionDate).toLocaleString() + '</div>' : '');
        footerEl.appendChild(info);
      }
    }

    function render() {
      var doc = documents[activeIndex];
      var isDuplicateFamily = DUPLICATE_FAMILY.indexOf(doc.status) !== -1;

      if (headerRightEl) headerRightEl.textContent = 'Image ' + (activeIndex + 1) + ' of ' + documents.length;
      prevBtn.disabled = activeIndex === 0;
      nextBtn.disabled = activeIndex === documents.length - 1;

      renderHeaderLeft(doc, isDuplicateFamily);
      renderFooter(doc);

      if (isDuplicateFamily) {
        singleWrap.classList.add('d-none');
        dualWrap.classList.remove('d-none');
        leftViewer.load(window.dctPlaceholderImage(doc.fileName), doc.fileName, { fitOnLoad: true });
        var rightName = doc.existingFileName || doc.fileName;
        rightViewer.load(window.dctPlaceholderImage(rightName), rightName, { fitOnLoad: true });
      } else {
        dualWrap.classList.add('d-none');
        singleWrap.classList.remove('d-none');
        singleViewer.load(window.dctPlaceholderImage(doc.fileName), doc.fileName, { fitOnLoad: true });
      }

      if (listEl) {
        Array.prototype.forEach.call(listEl.querySelectorAll('[data-doc-index]'), function (el) {
          var isActive = Number(el.getAttribute('data-doc-index')) === activeIndex;
          el.classList.toggle('table-active', isActive);
        });
      }
    }

    function selectDocument(index) {
      if (index < 0 || index >= documents.length) return;
      activeIndex = index;
      render();
    }

    if (listEl) {
      listEl.addEventListener('click', function (e) {
        var item = e.target.closest('[data-doc-index]');
        if (!item) return;
        selectDocument(Number(item.getAttribute('data-doc-index')));
      });
    }

    prevBtn.addEventListener('click', function () { selectDocument(activeIndex - 1); });
    nextBtn.addEventListener('click', function () { selectDocument(activeIndex + 1); });

    render();
  });
})();
