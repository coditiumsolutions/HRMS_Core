(function () {
    function initHrmsGridSelection(grid) {
        if (!grid || grid.dataset.hrmsGridInit === '1') return;
        grid.dataset.hrmsGridInit = '1';

        var editBase = grid.dataset.editBase || '';
        var deleteBase = grid.dataset.deleteBase || '';
        var detailsBase = grid.dataset.detailsBase || '';
        var editBtn = document.getElementById(grid.dataset.editBtnId || 'btnEditSelected');
        var deleteBtn = document.getElementById(grid.dataset.deleteBtnId || 'btnDeleteSelected');
        var radioClass = grid.dataset.radioClass || 'hrms-row-select';
        var radioSel = '.' + radioClass;

        var radios = grid.querySelectorAll(radioSel);
        if (!radios.length) return;

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

        function syncSelection() {
            var selected = grid.querySelector(radioSel + ':checked');
            grid.querySelectorAll('tbody tr').forEach(function (row) {
                row.classList.toggle('is-selected', !!(selected && row.contains(selected)));
            });
            if (!selected) {
                setActionEnabled(editBtn, false);
                setActionEnabled(deleteBtn, false);
                return;
            }
            var row = selected.closest('tr');
            var id = selected.value || (row ? rowId(row) : null);
            if (!id) {
                setActionEnabled(editBtn, false);
                setActionEnabled(deleteBtn, false);
                return;
            }
            var canEdit = row ? rowCanMutate(row) : true;
            setActionEnabled(editBtn, canEdit && !!editBase, editBase + '/' + encodeURIComponent(id));
            setActionEnabled(deleteBtn, canEdit && !!deleteBase, deleteBase + '/' + encodeURIComponent(id));
        }

        function openRead(row) {
            var id = rowId(row);
            if (!id) return;
            if (detailsBase) {
                window.location.href = detailsBase + '/' + encodeURIComponent(id);
                return;
            }
            if (editBase) {
                window.location.href = editBase + '/' + encodeURIComponent(id);
            }
        }

        radios.forEach(function (radio) {
            radio.addEventListener('change', syncSelection);
        });

        grid.querySelectorAll('tbody tr').forEach(function (row) {
            row.addEventListener('click', function (e) {
                if (e.target.closest('a, button, input, label')) return;
                var radio = row.querySelector(radioSel);
                if (!radio) return;
                radio.checked = true;
                radio.dispatchEvent(new Event('change', { bubbles: true }));
            });

            row.addEventListener('dblclick', function (e) {
                if (e.target.closest('a, button, input, label')) return;
                openRead(row);
            });
        });

        syncSelection();
    }

    document.querySelectorAll('.hrms-selectable-grid').forEach(initHrmsGridSelection);
})();
