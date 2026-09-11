// Vanilla-JS port of the React frontend's useZoomPan.ts + DocumentViewer.tsx: zoom in/out,
// actual size (100%), fit-to-screen, rotate left/right, pan-mode drag, and cursor-anchored
// wheel zoom. This module only owns the image viewport (zoom/rotate/pan/fit + toolbar
// buttons); which document is currently loaded is a page-level concern - callers call
// viewer.load(src, fileName, { fitOnLoad }) whenever the active document changes, and listen
// for the 'docviewer:prev' / 'docviewer:next' events this module dispatches on the container.
(function () {
  'use strict';

  function createViewer(container) {
    var enableWheelZoom = container.dataset.enableWheelZoom === 'true';
    var minZoom = 0.2;
    var maxZoom = 5;
    var buttonZoomStep = 0.2;
    var wheelZoomStep = 0.1;

    var frame = container.querySelector('[data-viewer-frame]');
    var img = container.querySelector('[data-viewer-image]');
    var filenameEl = container.querySelector('[data-viewer-filename]');

    var state = { zoom: 1, rotation: 0, panX: 0, panY: 0, panMode: false };
    var drag = { isDragging: false, startX: 0, startY: 0 };
    var pendingFitOnLoad = false;

    // Rendered image size accounting for zoom and 90/270deg rotation swapping width/height -
    // used to keep panning from ever dragging the image fully out of the frame.
    function getRenderedSize() {
      var iw = img.naturalWidth || img.width || 0;
      var ih = img.naturalHeight || img.height || 0;
      var swapped = Math.abs(state.rotation % 180) === 90;
      return {
        w: (swapped ? ih : iw) * state.zoom,
        h: (swapped ? iw : ih) * state.zoom,
      };
    }

    function clampPan() {
      var size = getRenderedSize();
      if (!size.w || !size.h || !frame) return;
      var fw = frame.clientWidth;
      var fh = frame.clientHeight;
      var minVisible = 40;
      var maxPanX = Math.max(0, size.w / 2 + fw / 2 - minVisible);
      var maxPanY = Math.max(0, size.h / 2 + fh / 2 - minVisible);
      state.panX = Math.min(maxPanX, Math.max(-maxPanX, state.panX));
      state.panY = Math.min(maxPanY, Math.max(-maxPanY, state.panY));
    }

    function applyTransform() {
      img.style.transform =
        'translate(' + state.panX + 'px, ' + state.panY + 'px) scale(' + state.zoom + ') rotate(' + state.rotation + 'deg)';
      img.style.transformOrigin = 'center center';
      if (!drag.isDragging) {
        img.style.cursor = state.panMode || state.zoom > 1 ? 'grab' : 'default';
      }
    }

    function setPanButtonState() {
      var panBtn = container.querySelector('[data-viewer-action="togglePan"]');
      if (panBtn) panBtn.classList.toggle('btn-navy', state.panMode);
    }

    function zoomIn() {
      state.zoom = Math.min(maxZoom, state.zoom + buttonZoomStep);
      clampPan();
      applyTransform();
    }

    function zoomOut() {
      state.zoom = Math.max(minZoom, state.zoom - buttonZoomStep);
      clampPan();
      applyTransform();
    }

    function actualSize() {
      state.zoom = 1;
      state.rotation = 0;
      state.panX = 0;
      state.panY = 0;
      applyTransform();
    }

    function rotateLeft() {
      state.rotation -= 90;
      clampPan();
      applyTransform();
    }

    function rotateRight() {
      state.rotation += 90;
      clampPan();
      applyTransform();
    }

    function togglePan() {
      state.panMode = !state.panMode;
      setPanButtonState();
      applyTransform();
    }

    function fitToScreen() {
      var cw = frame.clientWidth;
      var ch = frame.clientHeight;
      var iw = img.naturalWidth || img.width;
      var ih = img.naturalHeight || img.height;
      if (!iw || !ih) return;
      state.panX = 0;
      state.panY = 0;
      state.zoom = Math.min(1, Math.min(cw / iw, ch / ih));
      applyTransform();
    }

    if (enableWheelZoom) {
      frame.addEventListener(
        'wheel',
        function (e) {
          e.preventDefault();
          e.stopPropagation();
          var rect = frame.getBoundingClientRect();
          var mouseX = e.clientX - rect.left;
          var mouseY = e.clientY - rect.top;
          var oldZoom = state.zoom;
          var worldX = (mouseX - state.panX) / oldZoom;
          var worldY = (mouseY - state.panY) / oldZoom;
          var newZoom = Math.min(maxZoom, Math.max(minZoom, oldZoom + (e.deltaY < 0 ? wheelZoomStep : -wheelZoomStep)));
          if (newZoom !== oldZoom) {
            state.panX = mouseX - worldX * newZoom;
            state.panY = mouseY - worldY * newZoom;
            state.zoom = newZoom;
            clampPan();
            applyTransform();
          }
        },
        { passive: false }
      );
    }

    // Click-and-drag works whenever Pan/Move is toggled on (existing behavior, unchanged) OR
    // whenever the image is zoomed past 100% (an image that big is worth dragging even without
    // explicitly enabling pan mode first) - purely additive, on top of the existing controls.
    img.addEventListener('mousedown', function (e) {
      if (!state.panMode && state.zoom <= 1) return;
      e.preventDefault();
      drag.isDragging = true;
      img.style.cursor = 'grabbing';
      drag.startX = e.clientX - state.panX;
      drag.startY = e.clientY - state.panY;

      function onMove(ev) {
        if (!drag.isDragging) return;
        state.panX = ev.clientX - drag.startX;
        state.panY = ev.clientY - drag.startY;
        clampPan();
        applyTransform();
      }
      function onUp() {
        drag.isDragging = false;
        applyTransform();
        window.removeEventListener('mousemove', onMove);
        window.removeEventListener('mouseup', onUp);
      }
      window.addEventListener('mousemove', onMove);
      window.addEventListener('mouseup', onUp);
    });

    img.addEventListener('load', function () {
      if (pendingFitOnLoad) {
        pendingFitOnLoad = false;
        fitToScreen();
      }
    });

    container.querySelectorAll('[data-viewer-action]').forEach(function (btn) {
      var action = btn.getAttribute('data-viewer-action');
      btn.addEventListener('click', function () {
        if (action === 'zoomIn') zoomIn();
        else if (action === 'zoomOut') zoomOut();
        else if (action === 'actualSize') actualSize();
        else if (action === 'fit') fitToScreen();
        else if (action === 'rotateLeft') rotateLeft();
        else if (action === 'rotateRight') rotateRight();
        else if (action === 'togglePan') togglePan();
        else if (action === 'reset') {
          // Same zoom=1/rotation=0/pan=0 restore as "100%" (actualSize), plus also turning off
          // Pan/Move if it was on - a full Reset clears every user-applied view transformation.
          actualSize();
          if (state.panMode) togglePan();
        }
        else if (action === 'prev') container.dispatchEvent(new CustomEvent('docviewer:prev'));
        else if (action === 'next') container.dispatchEvent(new CustomEvent('docviewer:next'));
      });
    });

    return {
      load: function (src, fileName, opts) {
        opts = opts || {};
        state.zoom = 1;
        state.rotation = 0;
        state.panX = 0;
        state.panY = 0;
        state.panMode = false;
        setPanButtonState();
        pendingFitOnLoad = !!opts.fitOnLoad;
        img.src = src;
        img.alt = fileName || '';
        if (filenameEl) filenameEl.textContent = fileName || '';
        applyTransform();
      },
      setNavDisabled: function (prevDisabled, nextDisabled) {
        var prevBtn = container.querySelector('[data-viewer-action="prev"]');
        var nextBtn = container.querySelector('[data-viewer-action="next"]');
        if (prevBtn) prevBtn.disabled = !!prevDisabled;
        if (nextBtn) nextBtn.disabled = !!nextDisabled;
      },
    };
  }

  window.DctDocViewer = { create: createViewer };
})();
