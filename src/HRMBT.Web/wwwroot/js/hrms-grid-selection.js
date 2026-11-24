(function () {
    // Selectable grid.
    //  - Clicking a row highlights it (.is-selected); double-click opens it (details, else edit).
    //  - Per-row Edit/Delete icons are plain links in an actions column, no script needed.
    //  - Legacy: a grid that still has radio buttons (data-radio-class) and header Edit/Delete buttons
    //    keeps working; the radio just mirrors the highlighted row.
    function initHrmsGridSelection(grid) {
        if (!grid || grid.dataset.hrmsGridInit === '1') return;
        grid.dataset.hrmsGridInit = '1';

        var editBase = grid.dataset.editBase || '';
        var deleteBase = grid.dataset.deleteBase || '';
        var detailsBase = grid.dataset.detailsBase || '';
        // Bases are URL templates containing __id__ (e.g. /employees/__id__/edit); a plain base still gets /id appended.
        function urlFor(base, id) {
            var enc = encodeURIComponent(id);
            return base.indexOf('__id__') >= 0 ? base.replace('__id__', enc) : base + '/' + enc;
        }
        var editBtn = document.getElementById(grid.dataset.editBtnId || 'btnEditSelected');
        var deleteBtn = document.getElementById(grid.dataset.deleteBtnId || 'btnDeleteSelected');
        var radioClass = grid.dataset.radioClass || 'hrms-row-select';
        var radioSel = '.' + radioClass;
        var rows = grid.querySelectorAll('tbody tr');

        function rowId(row) {
            return row.getAttribute('data-record-id')
                || row.getAttribute('data-employee-id')
                || row.getAttribute('data-row-id');
        }

        function rowCanMutate(row) {
            var flag = row.getAttribute('data-can-edit');
            if (flag === '0' || flag === 'false') return false;
            return true;
        }

        function setActionEnabled(btn, enabled, href) {
            if (!btn) return;
            if (enabled) {
                btn.classList.remove('disabled');
                btn.removeAttribute('aria-disabled');
                btn.removeAttribute('tabindex');
                btn.setAttribute('href', href);
            } else {
                btn.classList.add('disabled');
                btn.setAttribute('aria-disabled', 'true');
                btn.setAttribute('tabindex', '-1');
                btn.setAttribute('href', '#');
            }
        }

        function highlight(selectedRow) {
            rows.forEach(function (row) { row.classList.toggle('is-selected', row === selectedRow); });
            var radio = selectedRow && selectedRow.querySelector(radioSel);
            if (radio) radio.checked = true;
            var id = selectedRow ? rowId(selectedRow) : null;
            var canEdit = selectedRow ? rowCanMutate(selectedRow) : false;
            setActionEnabled(editBtn, !!id && canEdit && !!editBase, id ? urlFor(editBase, id) : '#');
            setActionEnabled(deleteBtn, !!id && canEdit && !!deleteBase, id ? urlFor(deleteBase, id) : '#');
        }

        function openRead(row) {
            var id = rowId(row);
            if (!id) return;
            if (detailsBase) { window.location.href = urlFor(detailsBase, id); return; }
            if (editBase) { window.location.href = urlFor(editBase, id); }
        }

        rows.forEach(function (row) {
            row.addEventListener('click', function (e) {
                if (e.target.closest('a, button, input, label')) return;
                highlight(row);
            });
            row.addEventListener('dblclick', function (e) {
                if (e.target.closest('a, button, input, label')) return;
                openRead(row);
            });
            var radio = row.querySelector(radioSel);
            if (radio) radio.addEventListener('change', function () { highlight(row); });
        });

        var checked = grid.querySelector(radioSel + ':checked');
        highlight(checked ? checked.closest('tr') : null);
    }

    document.querySelectorAll('.hrms-selectable-grid').forEach(initHrmsGridSelection);
})();
