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

    initStartFetching();
  });

  // Drives "Start Fetching": confirms, then POSTs /RdConfig/StartFetchStream and reads the
  // response body as a live Server-Sent Events stream (fetch() + a ReadableStream reader, since
  // the browser's native EventSource can only ever issue a GET, not this POST). Each SSE
  // message is parsed independently as it arrives and reflected in the UI immediately - nothing
  // here waits for the run to finish, and the page is never reloaded. There's no separate
  // progress panel: live progress is shown as one extra row prepended to the existing Fetch
  // History table itself, which the final refreshFetchHistory() call then replaces with the
  // real persisted row once the run ends.
  function initStartFetching() {
    var startBtn = document.getElementById('startFetchBtn');
    if (!startBtn) return;

    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';
    var currentUsername = startBtn.getAttribute('data-current-username') || '';

    var confirmModalEl = document.getElementById('confirmDialog');
    var confirmModal = confirmModalEl && window.bootstrap ? bootstrap.Modal.getOrCreateInstance(confirmModalEl) : null;

    // progressTotal/progressDone drive the Progress cell directly from real SSE data
    // (folder_inventory's to_process, then one increment per folder_result) - never a timer.
    var progressTotal = null;
    var progressDone = 0;
    var stopStream = false;
    var liveRow = null;

    function pick(obj, keys) {
      for (var i = 0; i < keys.length; i++) {
        var v = obj[keys[i]];
        if (v !== undefined && v !== null) return v;
      }
      return undefined;
    }

    function formatNow(date) {
      var hours = date.getHours();
      var minutes = String(date.getMinutes()).padStart(2, '0');
      var ampm = hours >= 12 ? 'PM' : 'AM';
      hours = hours % 12 || 12;
      return (date.getMonth() + 1) + '/' + String(date.getDate()).padStart(2, '0') + '/' + date.getFullYear() +
        ' ' + hours + ':' + minutes + ' ' + ampm;
    }

    function getHistoryTbody() {
      var container = document.getElementById('fetch-history-results');
      return container ? container.querySelector('tbody') : null;
    }

    // Prepends one live row - matching _FetchHistoryResults.cshtml's 8 columns exactly - to the
    // existing Fetch History table, removing its "No fetch history found." empty state if
    // present. Cell references are grabbed once here rather than re-queried by id, since this
    // row (unlike the rest of the table) is never re-rendered by the server while the fetch runs.
    function createLiveRow() {
      var tbody = getHistoryTbody();
      if (!tbody) return null;

      var emptyRow = tbody.querySelector('tr td[colspan]');
      if (emptyRow) emptyRow.closest('tr').remove();

      var row = document.createElement('tr');
      row.innerHTML =
        '<td class="font-monospace small"></td>' +
        '<td class="font-monospace small">—</td>' +
        '<td>—</td>' +
        '<td data-cell="progress">0 / —</td>' +
        '<td data-cell="status"><span class="badge badge-info">Starting…</span></td>' +
        '<td><span class="chip-mono"></span></td>' +
        '<td class="font-monospace small"></td>' +
        '<td></td>';

      var cells = row.querySelectorAll('td');
      cells[0].textContent = formatNow(new Date());
      cells[5].querySelector('.chip-mono').textContent = currentUsername;
      cells[6].textContent = (document.getElementById('rootPathField') || {}).value || '';

      tbody.insertBefore(row, tbody.firstChild);
      return { root: row, progress: cells[3], status: cells[4] };
    }

    function setStatus(html) {
      if (liveRow) liveRow.status.innerHTML = html;
    }

    function setProgress(text) {
      if (liveRow) liveRow.progress.textContent = text;
    }

    function updateProgressCell(done, total) {
      setProgress(done + ' / ' + (total === null ? '—' : total));
    }

    function markRunFailed(message) {
      setStatus('<span class="badge badge-err">Failed</span>' +
        (message ? '<div class="text-muted" style="font-size: 11px;">' + escapeHtml(message) + '</div>' : ''));
      stopStream = true;
    }

    function escapeHtml(text) {
      var div = document.createElement('div');
      div.textContent = text;
      return div.innerHTML;
    }

    function resetLiveState() {
      progressTotal = null;
      progressDone = 0;
      stopStream = false;
      liveRow = createLiveRow();
    }

    function handleConnectivityCheck(payload) {
      var ok = pick(payload, ['ok']);
      var message = pick(payload, ['message', 'status', 'detail']);

      if (ok === false) {
        // Per the integration contract: a failed connectivity check is the only event sent - no
        // run is actually started - so this must be shown as a stopped/failed attempt, not left
        // looking like a run that's still in progress.
        if (message === undefined) {
          var problems = ['database', 'llm', 'root_path'].filter(function (key) { return payload[key] === false; });
          message = problems.length
            ? 'Connectivity issue: ' + problems.join(', ') + ' not reachable.'
            : 'Connectivity issue detected.';
        }
        markRunFailed(message);
        return;
      }

      setStatus('<span class="badge badge-info">Ongoing Fetching</span>');
    }

    function handleFolderInventory(payload) {
      var toProcess = pick(payload, ['to_process', 'toProcess', 'folders_to_process', 'foldersToProcess']);
      progressTotal = Number(toProcess) || 0;
      progressDone = 0;
      updateProgressCell(0, progressTotal);
    }

    function handleFolderResult() {
      // Live, one increment per folder_result - never wait for run_complete to move this. Per-
      // folder detail (path/RD/failure reason) isn't shown live here; it's recorded server-side
      // into Failed Extraction regardless, and viewable afterward via the row's own "View" button.
      progressDone++;
      updateProgressCell(progressDone, progressTotal);
    }

    function handleSystemError(payload) {
      // "message" is the human-readable one in the real payload (e.g. "Stopping fetch run - llm
      // became unavailable"); "reason"/"system" are shorter machine-ish fields - prefer message.
      var message = pick(payload, ['message', 'reason', 'detail']) || 'The fetch service reported a system error.';
      // The server is stopping the run - show it as Failed now rather than waiting for a
      // run_complete that may never arrive, while preserving whatever progress was already made.
      markRunFailed(message);
    }

    function handleRunComplete(payload) {
      var status = String(pick(payload, ['status']) || '');
      var isFailed = status.toLowerCase().indexOf('fail') !== -1;
      var processedCount = pick(payload, ['processed_count', 'processedCount']);
      var totalCount = pick(payload, ['total_count', 'totalCount']);

      // run_complete's own counts are authoritative for the final state, superseding the
      // running tally kept from individual folder_result events.
      updateProgressCell(processedCount !== undefined ? processedCount : progressDone, totalCount !== undefined ? totalCount : progressTotal);
      setStatus('<span class="badge badge-' + (isFailed ? 'err' : 'ok') + '">' + (isFailed ? 'Failed' : 'Completed') + '</span>');

      stopStream = true;
    }

    function refreshFetchHistory() {
      var form = document.querySelector('[data-list-page-form="fetch-history-results"]');
      if (form && form.requestSubmit) form.requestSubmit();
    }

    function dispatchEvent(eventName, payload) {
      switch (eventName) {
        case 'connectivity_check': handleConnectivityCheck(payload); break;
        case 'folder_inventory': handleFolderInventory(payload); break;
        case 'folder_result': handleFolderResult(payload); break;
        case 'system_error': handleSystemError(payload); break;
        case 'run_complete': handleRunComplete(payload); break;
        default: break; // Unknown/future event kind - ignore rather than fail the whole stream.
      }
    }

    // Splits accumulated text on the SSE record separator (a blank line) and parses each
    // record's "event:"/"data:" lines independently, per the integration requirement. The
    // event's kind is read from the "event:" line when present, falling back to a type/event
    // field inside the JSON body itself since the exact wire format isn't documented beyond the
    // five named events.
    function processBuffer(buffer, isFinal) {
      var records = buffer.split(/\r?\n\r?\n/);
      var remainder = isFinal ? '' : records.pop();

      records.forEach(function (record) {
        if (!record.trim()) return;

        var eventName = null;
        var dataLines = [];
        record.split(/\r?\n/).forEach(function (line) {
          if (line.indexOf('event:') === 0) {
            eventName = line.slice('event:'.length).trim();
          } else if (line.indexOf('data:') === 0) {
            dataLines.push(line.slice('data:'.length).trim());
          }
        });

        var raw = dataLines.join('\n');
        if (!raw) return;

        var payload;
        try {
          payload = JSON.parse(raw);
        } catch (err) {
          return; // Not JSON (or a partial record) - skip it rather than throw.
        }

        if (!eventName) {
          eventName = pick(payload, ['event', 'type', 'event_type', 'kind']);
        }
        if (eventName) dispatchEvent(eventName, payload);
      });

      return remainder;
    }

    function beginFetch() {
      startBtn.disabled = true;
      resetLiveState();

      fetch('/RdConfig/StartFetchStream', {
        method: 'POST',
        body: new URLSearchParams({ __RequestVerificationToken: token }),
      }).then(function (response) {
        if (!response.ok) {
          return response.json().catch(function () { return {}; }).then(function (data) {
            handleSystemError({ message: data.message || 'Unable to start the fetching process.' });
            startBtn.disabled = false;
            throw new Error('start-fetch-rejected');
          });
        }

        var reader = response.body.getReader();
        var decoder = new TextDecoder();
        var buffer = '';
        var buttonReenabled = false;

        function pump() {
          return reader.read().then(function (result) {
            if (result.done) {
              processBuffer(buffer, true);
              startBtn.disabled = false;
              // The server only reconciles the local Fetch History row (CompleteFetchRunAsync/
              // FailFetchRunAsync) after ITS OWN read loop sees this same stream close - which is
              // exactly what "result.done" here means. Refreshing at that point (rather than the
              // instant run_complete/system_error is parsed) avoids a race where this table
              // refresh lands before the server has actually saved the final status, which would
              // otherwise show a stale "Ongoing" until the next unrelated refresh.
              refreshFetchHistory();
              return;
            }

            buffer += decoder.decode(result.value, { stream: true });
            buffer = processBuffer(buffer, false);

            if (stopStream && !buttonReenabled) {
              // run_complete arrived, or the run was stopped early (a failed connectivity_check
              // or a system_error) - re-enable the button right away for responsiveness, but keep
              // reading (rather than reader.cancel()) so the Fetch History refresh above still
              // waits for the connection's real end, per the ordering note there.
              startBtn.disabled = false;
              buttonReenabled = true;
            }

            return pump();
          });
        }

        return pump();
      }).catch(function () {
        startBtn.disabled = false;
      });
    }

    var pendingConfirmHandler = null;
    var confirmBtn = document.getElementById('confirmDialogConfirmBtn');

    function clearPendingConfirm() {
      if (pendingConfirmHandler) {
        confirmBtn.removeEventListener('click', pendingConfirmHandler);
        pendingConfirmHandler = null;
      }
    }

    // A cancel/backdrop-dismiss/Esc never fires the Confirm click, so the listener added below
    // would otherwise accumulate across repeated open-then-cancel attempts and fire beginFetch()
    // multiple times on a later confirm. Clearing it whenever the shared dialog closes covers
    // every dismissal path, not just an explicit Cancel click.
    if (confirmModalEl) {
      confirmModalEl.addEventListener('hidden.bs.modal', clearPendingConfirm);
    }

    startBtn.addEventListener('click', function () {
      if (startBtn.disabled) return;

      if (confirmModal && confirmBtn) {
        document.getElementById('confirmDialogMessage').textContent = 'Are you sure you want to start the fetching process?';
        confirmBtn.className = 'btn btn-navy';
        confirmBtn.textContent = 'Start Fetching';
        clearPendingConfirm();
        pendingConfirmHandler = function () {
          confirmModal.hide();
          beginFetch();
        };
        confirmBtn.addEventListener('click', pendingConfirmHandler);
        confirmModal.show();
      } else {
        beginFetch();
      }
    });
  }
})();
