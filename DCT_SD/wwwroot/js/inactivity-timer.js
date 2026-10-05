// Session-inactivity handling: after 10 minutes with no mouse click / keyboard input, shows a
// Yes/No prompt; if nobody answers within another 5 minutes (15 total), the session is force-
// ended - same as a real Logout (revokes the refresh token, releases any Manual Validation lock
// the user holds) - and the browser is sent back to Login with the inactivity message. Only
// loaded on authenticated pages (see _Layout.cshtml) and no-ops if its own markup isn't present,
// matching app-shell.js's own per-element existence-check style.
//
// A page that has unsaved work can register a hook (see registerSaveHook below) that this file
// calls - and awaits - right before the forced logout, so in-progress edits aren't silently
// lost. This file has no Manual-Validation-specific knowledge itself; manual-validation-details.js
// is what actually registers/clears the hook.
(function () {
  'use strict';

  var WARNING_MS = 10 * 60 * 1000;
  var GRACE_MS = 5 * 60 * 1000;

  var modalEl = document.getElementById('inactivityWarningModal');
  if (!modalEl || !window.bootstrap) return;

  var bsModal = new bootstrap.Modal(modalEl);
  var yesBtn = document.getElementById('inactivityYesBtn');
  var noBtn = document.getElementById('inactivityNoBtn');

  var warningTimer = null;
  var graceTimer = null;
  var promptShown = false;
  var saveHook = null;

  window.dctInactivity = {
    registerSaveHook: function (fn) { saveHook = fn; },
    clearSaveHook: function () { saveHook = null; },
  };

  function getAntiForgeryToken() {
    var input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : null;
  }

  function endSession(redirectReason) {
    var token = getAntiForgeryToken();
    var headers = {};
    var body = null;
    if (token) {
      body = new URLSearchParams();
      body.append('__RequestVerificationToken', token);
    }

    fetch('/Account/Logout', {
      method: 'POST',
      headers: headers,
      body: body,
      credentials: 'same-origin',
    }).catch(function () {
      // Network failure ending the session server-side doesn't stop the browser from still
      // redirecting to Login below - the cookies may or may not have been cleared, but Login
      // itself will simply prompt for credentials again either way.
    }).then(function () {
      var url = '/Account/Login';
      if (redirectReason) url += '?reason=' + encodeURIComponent(redirectReason);
      window.location.href = url;
    });
  }

  function forceExpire() {
    var maybePromise = saveHook ? saveHook() : null;
    if (maybePromise && typeof maybePromise.then === 'function') {
      maybePromise.then(function () { endSession('inactivity'); }, function () { endSession('inactivity'); });
    } else {
      endSession('inactivity');
    }
  }

  function showPrompt() {
    promptShown = true;
    bsModal.show();
    graceTimer = setTimeout(forceExpire, GRACE_MS);
  }

  function resetWarningTimer() {
    if (warningTimer) clearTimeout(warningTimer);
    warningTimer = setTimeout(showPrompt, WARNING_MS);
  }

  function onActivity() {
    // While the prompt is up, ordinary page activity elsewhere is deliberately ignored - only
    // an explicit Yes/No answer in the prompt itself should count, per the acceptance criteria's
    // "the inactivity timer must reset when the user performs valid activity" paired with a
    // dedicated Yes/No confirmation step.
    if (promptShown) return;
    resetWarningTimer();
  }

  yesBtn.addEventListener('click', function () {
    if (graceTimer) clearTimeout(graceTimer);
    promptShown = false;
    bsModal.hide();
    resetWarningTimer();
  });

  noBtn.addEventListener('click', function () {
    if (graceTimer) clearTimeout(graceTimer);
    bsModal.hide();
    endSession(null);
  });

  document.addEventListener('mousedown', onActivity);
  document.addEventListener('keydown', onActivity);

  resetWarningTimer();
})();
