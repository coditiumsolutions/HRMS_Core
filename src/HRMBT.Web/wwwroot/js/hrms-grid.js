/* Grid alignment standard (docs/ui_standards.md):
   numeric columns right-aligned, dates/times/IDs no-wrap, status and badge columns left,
   header always follows its column. Runs once per data table; a column or table can opt
   out with data-align="none". Pure presentation: no data is changed. */
(function () {
  'use strict';
  var NUM = /^(PKR\s*)?[-+(]?[\d,]+(\.\d+)?\)?\s*(%|days?|hrs?|h)?$/i;
  var DATE = /^((\d{2}:\d{2}(:\d{2})?\s[AP]M\s)?\d{2}\/\d{2}\/\d{4}|\d{2}:\d{2}(:\d{2})?\s[AP]M|[A-Z][a-z]+\s\d{4})$/;
  var EMPTY = /^[\s—–-]*$/;

  function text(td) { return (td.textContent || '').replace(/\s+/g, ' ').trim(); }

  function classify(rows, idx) {
    var num = 0, date = 0, total = 0;
    for (var i = 0; i < rows.length; i++) {
      var td = rows[i].cells[idx];
      if (!td) continue;
      var t = text(td);
      if (EMPTY.test(t)) continue;
      if (td.querySelector('input, select, textarea, .badge, .status-pill, a.btn, button')) return null;
      total++;
      if (NUM.test(t)) num++;
      else if (DATE.test(t)) date++;
    }
    if (!total) return null;
    if (num === total) return 'num';
    if (date === total) return 'date';
    return null;
  }

  function apply(table) {
    if (table.dataset.align === 'none' || table.dataset.hrmsAligned) return;
    table.dataset.hrmsAligned = '1';
    var body = table.tBodies[0];
    var head = table.tHead && table.tHead.rows[table.tHead.rows.length - 1];
    if (!body || !head || body.rows.length === 0) return;
    if (table.querySelector('tbody [colspan], tbody [rowspan]')) return;   // irregular grid: leave alone
    var rows = Array.prototype.slice.call(body.rows);
    for (var c = 0; c < head.cells.length; c++) {
      var th = head.cells[c];
      if (th.dataset.align === 'none' || th.classList.contains('text-center') || th.classList.contains('text-start')) continue;
      var kind = classify(rows, c);
      if (!kind) continue;
      var cls = kind === 'num' ? 'text-end' : 'text-nowrap';
      th.classList.add(cls);
      if (kind === 'num') th.classList.add('hr-num');
      rows.forEach(function (r) {
        var td = r.cells[c];
        if (!td || td.classList.contains('text-center') || td.classList.contains('text-start')) return;
        td.classList.add(cls);
        if (kind === 'num') td.classList.add('hr-num');
      });
    }
  }

  function run() {
    var tables = document.querySelectorAll('main table.table, main table.payslip-grid, main table.employee-grid');
    for (var i = 0; i < tables.length; i++) apply(tables[i]);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
})();
