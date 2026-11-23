(function () {
    'use strict';

    var root = document.getElementById('employeeDetailsView');
    var tabList = document.getElementById('employeeDetailsTabs');
    if (!root || !tabList) return;

    var storageKey = root.getAttribute('data-tab-storage-key') || 'hrms.employeeDetails.activeTab';

    // Personal Info, Official Info and Additional Info were merged into Employee Info.
    var legacyKeys = { personal: 'employee', official: 'employee', additional: 'employee' };

    function getTabButton(key) {
        if (!key) return null;
        key = legacyKeys[key] || key;
        return tabList.querySelector('[data-tab-key="' + key + '"]');
    }

    function showTab(key) {
        var btn = getTabButton(key);
        if (!btn || !window.bootstrap) return;
        window.bootstrap.Tab.getOrCreateInstance(btn).show();
    }

    tabList.addEventListener('shown.bs.tab', function (e) {
        var key = e.target.getAttribute('data-tab-key');
        if (!key) return;
        try {
            sessionStorage.setItem(storageKey, key);
        } catch (err) { /* ignore */ }
    });

    try {
        var key = null;
        var params = new URLSearchParams(window.location.search);
        if (params.get('tab')) {
            key = params.get('tab');
        } else {
            key = sessionStorage.getItem(storageKey);
        }
        if (key) showTab(key);
    } catch (err) { /* ignore */ }
})();
