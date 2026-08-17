// The "Browse Folder" button opens the shared #ajaxModal via modal-loader.js's generic
// data-modal-url mechanism, loading RdConfigController.BrowseFolders (a server-side directory
// listing of this machine's fixed drives, drilling down like a native folder picker). This
// exists because browsers deliberately never expose a real filesystem path from the
// client-side File System Access API - only ever a folder *name* - which cannot work as an
// actual scan root for the backend's fetch process. Clicking a folder/the "Up" link re-fetches
// this same partial into the modal (event delegation on #ajaxModalContent, since content
// swapped in via innerHTML never re-runs inline scripts). "Select This Folder" copies the
// modal's current path into the Root Source Path field and closes the modal; Cancel/dismissing
// the modal leaves the field untouched.
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    // Root Source Path Update History: Date From/To mutually constrain each other so the user
    // can never pick an invalid (From > To) range in the first place.
    var rootDateFrom = document.getElementById('rootDateFrom');
    var rootDateTo = document.getElementById('rootDateTo');
    if (rootDateFrom && rootDateTo) {
      rootDateFrom.addEventListener('change', function () {
        rootDateTo.min = rootDateFrom.value || '';
      });
      rootDateTo.addEventListener('change', function () {
        rootDateFrom.max = rootDateTo.value || '';
      });
    }

    var contentEl = document.getElementById('ajaxModalContent');
    if (!contentEl) return;

    contentEl.addEventListener('click', function (e) {
      var selectBtn = e.target.closest('#browseFolderSelectBtn');
      if (!selectBtn || selectBtn.disabled) return;

      var currentPathEl = document.getElementById('browseFolderCurrentPath');
      var path = currentPathEl ? currentPathEl.getAttribute('data-current-path') : null;
      if (!path) return;

      document.getElementById('rootPathField').value = path;
      document.getElementById('rootPathHiddenInput').value = path;

      var modalEl = document.getElementById('ajaxModal');
      if (modalEl && window.bootstrap) {
        var modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
      }
    });
  });
})();
