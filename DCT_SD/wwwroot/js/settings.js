// Wires the Settings screen's three tabs: Branding (upload/remove login background image),
// Email Templates (select/edit/save/preview/restore-default), and Session (timeout + action).
(function () {
  'use strict';

  document.addEventListener('DOMContentLoaded', function () {
    function toast(message, variant) {
      if (window.showToast) window.showToast(message, variant);
    }

    function tokenFrom(form) {
      var input = form.querySelector('input[name="__RequestVerificationToken"]');
      return input ? input.value : '';
    }

    // --- Branding ---
    var brandingPreview = document.getElementById('brandingPreview');
    var brandingFileInput = document.getElementById('brandingFileInput');
    var brandingRemoveBtn = document.getElementById('brandingRemoveBtn');
    var brandingUploadForm = document.getElementById('brandingUploadForm');

    function setBrandingPreview(imageUrl) {
      if (imageUrl) {
        brandingPreview.style.backgroundImage = "url('" + imageUrl + "')";
        brandingPreview.textContent = '';
        brandingRemoveBtn.style.display = 'inline-block';
      } else {
        brandingPreview.style.backgroundImage = '';
        brandingPreview.textContent = 'No image uploaded';
        brandingRemoveBtn.style.display = 'none';
      }
    }

    if (brandingFileInput) {
      brandingFileInput.addEventListener('change', function () {
        var file = brandingFileInput.files && brandingFileInput.files[0];
        if (!file) return;

        var formData = new FormData();
        formData.append('file', file);
        formData.append('__RequestVerificationToken', tokenFrom(brandingUploadForm));

        fetch('/Settings/UploadBrandingImage', { method: 'POST', body: formData })
          .then(function (r) { return r.json(); })
          .then(function (data) {
            if (!data.success) {
              toast(data.message || 'Unable to upload this image.', 'error');
              return;
            }
            setBrandingPreview(data.imageUrl);
            toast(data.message, 'success');
          })
          .finally(function () {
            brandingFileInput.value = '';
          });
      });
    }

    if (brandingRemoveBtn) {
      brandingRemoveBtn.addEventListener('click', function () {
        var body = new URLSearchParams({ __RequestVerificationToken: tokenFrom(brandingUploadForm) });
        fetch('/Settings/RemoveBrandingImage', { method: 'POST', body: body })
          .then(function (r) { return r.json(); })
          .then(function (data) {
            setBrandingPreview(null);
            toast(data.message, 'success');
          });
      });
    }

    // --- Email Templates ---
    var emailTemplates = JSON.parse((document.getElementById('emailTemplatesData') || {}).textContent || '[]');
    var emailTemplateForm = document.getElementById('emailTemplateForm');
    var emailTemplateTabsEl = document.getElementById('emailTemplateTabs');
    var recipientsEl = document.getElementById('emailTplRecipients');
    var subjectEl = document.getElementById('emailTplSubject');
    var bodyEl = document.getElementById('emailTplBody');
    var activeKey = emailTemplates.length > 0 ? emailTemplates[0].key : null;

    function findTemplate(key) {
      return emailTemplates.find(function (t) { return t.key === key; });
    }

    function renderEmailTemplate(key) {
      var template = findTemplate(key);
      if (!template) return;
      activeKey = key;
      recipientsEl.value = template.recipients;
      subjectEl.value = template.subject;
      bodyEl.value = template.body;

      if (emailTemplateTabsEl) {
        Array.prototype.forEach.call(emailTemplateTabsEl.querySelectorAll('[data-email-template-key]'), function (btn) {
          var isActive = btn.getAttribute('data-email-template-key') === key;
          btn.classList.toggle('btn-navy', isActive);
          btn.classList.toggle('btn-outline-secondary', !isActive);
        });
      }
    }

    if (emailTemplateTabsEl) {
      emailTemplateTabsEl.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-email-template-key]');
        if (!btn) return;
        renderEmailTemplate(btn.getAttribute('data-email-template-key'));
      });
    }

    if (activeKey) renderEmailTemplate(activeKey);

    function fillEmailPlaceholders(text) {
      var now = new Date();
      var sample = {
        '{{FirstName}}': 'Jane',
        '{{LastName}}': 'Doe',
        '{{Email}}': 'jane@gmail.com',
        '{{TemporaryPassword}}': 'TempPass@123',
        '{{ResetPasswordLink}}': 'https://lares.example.com/reset-password?token=demo',
        '{{ChangePasswordLink}}': 'https://lares.example.com/change-password?token=demo',
        '{{CurrentDate}}': String(now.getMonth() + 1).padStart(2, '0') + '-' + String(now.getDate()).padStart(2, '0') + '-' + now.getFullYear(),
      };
      var out = text;
      Object.keys(sample).forEach(function (key) {
        out = out.split(key).join(sample[key]);
      });
      return out;
    }

    var saveBtn = document.getElementById('emailTplSaveBtn');
    if (saveBtn) {
      saveBtn.addEventListener('click', function () {
        var body = new URLSearchParams({
          key: activeKey,
          Recipients: recipientsEl.value,
          Subject: subjectEl.value,
          Body: bodyEl.value,
          __RequestVerificationToken: tokenFrom(emailTemplateForm),
        });
        fetch('/Settings/SaveEmailTemplate', { method: 'POST', body: body })
          .then(function (r) { return r.json(); })
          .then(function (data) {
            if (!data.success) {
              toast(data.message || 'Unable to save this template.', 'error');
              return;
            }
            var template = findTemplate(activeKey);
            if (template) {
              template.recipients = data.template.recipients;
              template.subject = data.template.subject;
              template.body = data.template.body;
            }
            toast(data.message, 'success');
          });
      });
    }

    var restoreBtn = document.getElementById('emailTplRestoreBtn');
    if (restoreBtn) {
      restoreBtn.addEventListener('click', function () {
        var body = new URLSearchParams({ key: activeKey, __RequestVerificationToken: tokenFrom(emailTemplateForm) });
        fetch('/Settings/RestoreEmailTemplateDefault', { method: 'POST', body: body })
          .then(function (r) { return r.json(); })
          .then(function (data) {
            if (!data.success) {
              toast(data.message || 'Unable to restore the default template.', 'error');
              return;
            }
            var template = findTemplate(activeKey);
            if (template) {
              template.recipients = data.template.recipients;
              template.subject = data.template.subject;
              template.body = data.template.body;
            }
            renderEmailTemplate(activeKey);
            toast(data.message, 'success');
          });
      });
    }

    var previewBtn = document.getElementById('emailTplPreviewBtn');
    if (previewBtn && window.bootstrap) {
      var previewModal = new bootstrap.Modal(document.getElementById('emailPreviewModal'));
      previewBtn.addEventListener('click', function () {
        document.getElementById('emailPreviewTo').textContent = fillEmailPlaceholders(recipientsEl.value);
        document.getElementById('emailPreviewSubject').textContent = fillEmailPlaceholders(subjectEl.value);
        document.getElementById('emailPreviewBody').textContent = fillEmailPlaceholders(bodyEl.value);
        previewModal.show();
      });
    }

    // --- Session ---
    var timeoutSelect = document.getElementById('sessionTimeoutSelect');
    var customWrap = document.getElementById('sessionCustomMinutesWrap');
    var customInput = document.getElementById('sessionCustomMinutes');
    var sessionForm = document.getElementById('sessionSettingsForm');

    if (timeoutSelect) {
      timeoutSelect.addEventListener('change', function () {
        customWrap.style.display = timeoutSelect.value === 'custom' ? '' : 'none';
      });
    }

    if (sessionForm) {
      sessionForm.addEventListener('submit', function (e) {
        var minutes = timeoutSelect.value === 'custom' ? parseInt(customInput.value, 10) : parseInt(timeoutSelect.value, 10);
        if (!minutes || minutes <= 0) {
          e.preventDefault();
          toast('Please enter a valid custom timeout value.', 'error');
          return;
        }
        var action = document.querySelector('input[name="sessionTimeoutAction"]:checked').value;
        document.getElementById('sessionTimeoutMinutesHidden').value = minutes;
        document.getElementById('sessionActionHidden').value = action;
      });
    }
  });
})();
