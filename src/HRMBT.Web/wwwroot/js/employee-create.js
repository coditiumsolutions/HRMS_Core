(function () {
    'use strict';

    var form = document.getElementById('employeeCreateForm');
    var TAB_STORAGE_KEY = (form && form.getAttribute('data-tab-storage-key'))
        || 'hrms.employeeCreate.activeTab';
    var tabList = document.getElementById('employeeCreateTabs');
    var fileInput = document.getElementById('employeePhotoFile');
    var browseBtn = document.getElementById('employeePhotoBrowse');
    var preview = document.getElementById('employeePhotoPreview');
    var placeholderSrc = preview
        ? (preview.getAttribute('data-placeholder-src') || preview.getAttribute('src') || '')
        : '';
    var submitBtn = document.getElementById('employeeFormSubmit');
    var spinner = document.getElementById('employeeSaveSpinner');
    var icon = document.getElementById('employeeSaveIcon');

    function getTabButton(key) {
        if (!tabList || !key) return null;
        return tabList.querySelector('[data-tab-key="' + key + '"]');
    }

    function showTab(key) {
        var btn = getTabButton(key);
        if (!btn || !window.bootstrap) return;
        window.bootstrap.Tab.getOrCreateInstance(btn).show();
    }

    function saveActiveTab(key) {
        try {
            sessionStorage.setItem(TAB_STORAGE_KEY, key);
        } catch (e) { /* ignore */ }
    }

    function restoreActiveTab() {
        var key = null;
        try {
            var params = new URLSearchParams(window.location.search);
            if (params.get('tab')) {
                key = params.get('tab');
            } else {
                key = sessionStorage.getItem(TAB_STORAGE_KEY);
            }
            if (key) showTab(key);
        } catch (e) { /* ignore */ }
    }

    function tabKeyForField(field) {
        var pane = field.closest('[data-tab-panel]');
        return pane ? pane.getAttribute('data-tab-panel') : null;
    }

    function focusFirstInvalidTab() {
        if (!form) return;
        var invalid = form.querySelector('.input-validation-error, :invalid');
        if (!invalid) return;
        var key = tabKeyForField(invalid);
        if (key) showTab(key);
        invalid.focus({ preventScroll: false });
    }

    if (tabList) {
        tabList.addEventListener('shown.bs.tab', function (e) {
            var key = e.target.getAttribute('data-tab-key');
            if (key) saveActiveTab(key);
        });
        restoreActiveTab();
    }

    if (browseBtn && fileInput && preview) {
        browseBtn.addEventListener('click', function () { fileInput.click(); });
        fileInput.addEventListener('change', function () {
            var f = fileInput.files && fileInput.files[0];
            if (!f || !f.type.match(/^image\//)) return;
            var reader = new FileReader();
            reader.onload = function (e) { preview.src = e.target.result; };
            reader.readAsDataURL(f);
        });
    }

    var resetBtn = document.getElementById('employeeFormReset');
    if (resetBtn && preview) {
        resetBtn.addEventListener('click', function () {
            setTimeout(function () {
                preview.src = placeholderSrc;
                if (fileInput) fileInput.value = '';
                try { sessionStorage.removeItem(TAB_STORAGE_KEY); } catch (e) { /* ignore */ }
                showTab('personal');
            }, 0);
        });
    }

    if (form && window.jQuery) {
        window.jQuery(function ($) {
            var $form = $(form);
            var validator = $form.data('validator');

            if (validator) {
                var previousInvalidHandler = validator.settings.invalidHandler;
                validator.settings.invalidHandler = function (event, v) {
                    if (typeof previousInvalidHandler === 'function') {
                        previousInvalidHandler.call(this, event, v);
                    }
                    if (v.errorList && v.errorList.length && v.errorList[0].element) {
                        var key = tabKeyForField(v.errorList[0].element);
                        if (key) showTab(key);
                        v.errorList[0].element.focus({ preventScroll: false });
                    }
                };
            }

            $form.on('submit', function () {
                if (!$form.valid()) {
                    focusFirstInvalidTab();
                    return;
                }
                if (submitBtn && spinner && icon) {
                    spinner.classList.remove('d-none');
                    icon.classList.add('d-none');
                    submitBtn.disabled = true;
                }
            });
        });
    }
})();
