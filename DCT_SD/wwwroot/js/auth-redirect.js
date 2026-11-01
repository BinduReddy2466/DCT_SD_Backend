// Shared helper for every AJAX fetch() call in the app (list-page.js, modal-loader.js,
// reports.js, manual-validation-details.js). The JWT auth challenge (Program.cs OnChallenge/
// OnForbidden) returns a plain 401/403 - instead of a redirect - whenever the request carries the
// X-Requested-With: XMLHttpRequest header every one of those callers already sends, specifically
// so this check can catch it here and send the whole browser to the login page, rather than the
// caller swapping an error response into a small results/modal container.
//
// Usage: `if (window.handleUnauthenticatedAjax(response)) return;` right after a fetch() resolves,
// before reading/using its body.
window.handleUnauthenticatedAjax = function (response) {
  if (response.status !== 401 && response.status !== 403) {
    return false;
  }

  var returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
  window.location.href = response.status === 401
    ? '/Account/Login?returnUrl=' + returnUrl
    : '/Account/AccessDenied';
  return true;
};
