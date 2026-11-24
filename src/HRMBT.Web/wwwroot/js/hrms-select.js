/*
 * hrms-select.js - searchable dropdowns for long lists.
 *
 * Short selects stay native (best on mobile and for screen readers; themed in hrms-theme.css).
 * A select with LONG_LIST or more options (departments, designations, projects, employees...) is
 * upgraded to a type-to-filter box with Tom Select. The original <select> stays in the page and keeps
 * its value, so forms, validation, onchange="this.form.submit()" and scripts that read $('#x').val() work unchanged.
 *
 * Opt out with data-native on a select; force it on a short one with data-searchable.
 * After a script rewrites a select's options, the box refreshes by itself; after a script only sets
 * select.value, call hrmsSelectRefresh(select).
 */
(function () {
    'use strict';
    var LONG_LIST = 15;
    if (typeof TomSelect === 'undefined') return;

    function wanted(sel) {
        if (sel.multiple || sel.size > 1 || sel.hasAttribute('data-native') || sel.tomselect) return false;
        if (sel.closest('template, .no-searchable')) return false;
        return sel.hasAttribute('data-searchable') || sel.options.length >= LONG_LIST;
    }

    function signature(sel) {
        return Array.prototype.map.call(sel.options, function (o) { return o.value + '|' + o.text; }).join('\n');
    }

    function placeholderOf(sel) {
        var first = sel.options[0];
        if (sel.getAttribute('data-placeholder')) return sel.getAttribute('data-placeholder');
        if (first && first.value === '' && first.text.trim()) return first.text.trim();
        return 'Select\u2026';
    }

    function enhance(sel) {
        if (!wanted(sel)) return;
        var hasEmpty = Array.prototype.some.call(sel.options, function (o) { return o.value === ''; });
        var ts = new TomSelect(sel, {
            allowEmptyOption: hasEmpty,
            create: false,
            maxOptions: null,
            openOnFocus: true,
            selectOnTab: true,
            placeholder: placeholderOf(sel),
            dropdownParent: 'body',            // never clipped by a card, table or sticky bar
            controlInput: '<input type="text" autocomplete="off" size="1" />',
            render: {
                no_results: function () { return '<div class="no-results">No matches</div>'; }
            },
            onDropdownOpen: function (dd) {
                // keep the list inside the viewport: flip above the control when there is no room below
                var r = this.control.getBoundingClientRect();
                var below = window.innerHeight - r.bottom;
                dd.classList.toggle('ts-flip', below < 260 && r.top > below);
            }
        });
        var last = signature(sel);
        var busy = false;
        new MutationObserver(function () {
            if (busy) return;
            var now = signature(sel);
            if (now === last) return;
            busy = true;
            last = now;
            var v = sel.value;
            ts.clear(true); ts.clearOptions(); ts.sync(); ts.setValue(v, true);
            setTimeout(function () { busy = false; last = signature(sel); }, 0);
        }).observe(sel, { childList: true });
    }

    window.hrmsSelectRefresh = function (sel) { if (sel && sel.tomselect) sel.tomselect.setValue(sel.value, true); };

    function run() { Array.prototype.forEach.call(document.querySelectorAll('select'), enhance); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
})();
