// window.showToast(message, variant) - variant: 'success' | 'error' | 'default'.
window.showToast = function (message, variant) {
  var container = document.getElementById('toastContainer');
  if (!container) return;

  var bg = variant === 'success' ? 'bg-success' : variant === 'error' ? 'bg-danger' : 'bg-dark';
  var toastEl = document.createElement('div');
  toastEl.className = 'toast align-items-center text-white ' + bg + ' border-0 mb-2';
  toastEl.setAttribute('role', 'alert');
  toastEl.innerHTML =
    '<div class="d-flex"><div class="toast-body"></div>' +
    '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
  toastEl.querySelector('.toast-body').textContent = message;
  container.appendChild(toastEl);

  var bsToast = new bootstrap.Toast(toastEl, { delay: 3600 });
  bsToast.show();
  toastEl.addEventListener('hidden.bs.toast', function () {
    toastEl.remove();
  });
};

// Counterpart to modal-loader.js stashing a toast in sessionStorage before a client-side
// location.reload() - the reload lands here, so this is what actually shows it.
document.addEventListener('DOMContentLoaded', function () {
  var pending = sessionStorage.getItem('pendingToast');
  if (!pending) return;
  sessionStorage.removeItem('pendingToast');
  try {
    var toast = JSON.parse(pending);
    window.showToast(toast.message, toast.variant);
  } catch (e) { /* malformed value, nothing to show */ }
});
