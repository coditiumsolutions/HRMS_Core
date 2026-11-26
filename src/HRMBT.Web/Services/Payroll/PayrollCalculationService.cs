using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRMBT.Web.Services.Payroll
{
    public class PayrollCalculationService
    {
        private readonly ApplicationDbContext _context;

        public PayrollCalculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>True when allowance name is Fuel Allowance (also accepts legacy "Fuel").</summary>
        public static bool IsFuelAllowanceName(string? allowanceName) =>
            string.Equals((allowanceName ?? "").Trim(), "Fuel Allowance", StringComparison.OrdinalIgnoreCase)
            || string.Equals((allowanceName ?? "").Trim(), "Fuel", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when type is Fixed Amount.</summary>
        public static bool IsFuelFixedAmountType(string? allowanceType) =>
            string.Equals((allowanceType ?? "").Trim(), "Fixed Amount", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when type is Quantity.</summary>
        public static bool IsFuelQuantityType(string? allowanceType) =>
            string.Equals((allowanceType ?? "").Trim(), "Quantity", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Legacy: AllowanceType itself was Fuel Allowance / Fuel.
        /// </summary>
        public static bool IsFuelAllowanceType(string? allowanceType) =>
            string.Equals((allowanceType ?? "").Trim(), "Fuel Allowance", StringComparison.OrdinalIgnoreCase)
            || string.Equals((allowanceType ?? "").Trim(), "Fuel", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Computed monetary amount for one allowance row.
        /// Fuel Allowance + Fixed Amount → Amount.
        /// Fuel Allowance + Quantity → fuel price for the month × Quantity.
        /// Legacy Fuel type: Amount if &gt; 0, else FuelPrice × Quantity.
        /// Other fixed rows: Amount × Quantity. Percentage: % of basic.
        /// </summary>
        public static decimal AllowanceComputedAmount(Allowance a, decimal basicSalary, decimal fuelPrice = 0m)
        {
            var qty = a.Quantity < 0 ? 0 : a.Quantity;

            if (a.IsPercentage)
                return basicSalary * (a.PercentageValue ?? 0m) / 100m;

            var isFuel = IsFuelAllowanceName(a.Name) || IsFuelAllowanceType(a.AllowanceType);
            if (isFuel)
            {
                if (IsFuelFixedAmountType(a.AllowanceType))
                    return a.Amount;

                if (IsFuelQuantityType(a.AllowanceType))
                    return fuelPrice * qty;

                // Legacy Fuel Allowance type: prefer Amount when set
                if (a.Amount > 0m)
                    return a.Amount;

                return fuelPrice * qty;
            }

            return a.Amount * qty;
        }

        /// <summary>Percentage of gross when <see cref="Deduction.CalculationMethod"/> is Percentage; otherwise fixed PKR from <see cref="Deduction.PercentageValue"/> (dbo has no Amount column — see db.md).</summary>
        public static bool IsPercentageDeduction(Deduction d) =>
            string.Equals(d.CalculationMethod, "Percentage", StringComparison.OrdinalIgnoreCase);

        public static decimal DeductionComputedAmount(Deduction d, decimal grossSalary) =>
            IsPercentageDeduction(d)
                ? grossSalary * (d.PercentageValue ?? 0m) / 100m
                : (d.PercentageValue ?? 0m);

        /// <summary>
        /// True when an allowance or deduction applies in the payroll period [periodStart, periodEndExclusive).
        /// It must have started (EffectiveDate before the period ends) and not have ended (EndDate on or after the period start).
        /// Frequency: Once = only in the month of EffectiveDate; Annual = in the same calendar month every year from EffectiveDate;
        /// Monthly (and anything else, including Weekly and Bi-Weekly, whose meaning HR has not defined) = every month.
        /// </summary>
        public static bool AppliesInPeriod(string? frequency, DateTime effectiveDate, DateTime? endDate, DateTime periodStart, DateTime periodEndExclusive)
        {
            if (effectiveDate >= periodEndExclusive)
                return false;
            if (endDate.HasValue && endDate.Value.Date < periodStart.Date)
                return false;

            var f = (frequency ?? string.Empty).Trim();
            if (f.Equals("Once", StringComparison.OrdinalIgnoreCase))
                return effectiveDate >= periodStart;
            if (f.Equals("Annual", StringComparison.OrdinalIgnoreCase))
                return effectiveDate.Month == periodStart.Month;
            return true;
        }

        /// <summary>Active allowance total using the same rules as payslip gross.</summary>
        public static decimal SumAllowancesForBasic(decimal basic, IEnumerable<Allowance> allowances, decimal fuelPrice = 0m) =>
            allowances.Sum(a => AllowanceComputedAmount(a, basic, fuelPrice));

        /// <summary>The fuel price in force on <paramref name="date"/> (dbo.FuelPrices), or 0 when none is set. Payroll passes the 1st of the payslip month (T9).</summary>
        public decimal GetFuelPrice(DateTime date) =>
            _context.FuelPrices.AsNoTracking()
                .Where(f => f.EffectiveFrom <= date)
                .OrderByDescending(f => f.EffectiveFrom)
                .Select(f => (decimal?)f.UnitPrice)
                .FirstOrDefault() ?? 0m;

        /// <summary>True when the row's amount is the fuel price times its quantity (so it needs a fuel price).</summary>
        private static bool UsesFuelPrice(Allowance a)
        {
            if (a.IsPercentage || !(IsFuelAllowanceName(a.Name) || IsFuelAllowanceType(a.AllowanceType)))
                return false;
            if (IsFuelFixedAmountType(a.AllowanceType)) return false;
            return IsFuelQuantityType(a.AllowanceType) || a.Amount <= 0m;
        }

        public Payslip GeneratePayslip(int employeeId, string month, int year, string generatedBy)
        {
            Payslip? payslip = null;
            try
            {
                var monthName = PayrollMonthHelper.Normalize(month);
                if (_context.Payslips.Any(p => p.EmployeeId == employeeId && p.Month == monthName && p.Year == year))
                    throw new InvalidOperationException("Payslip already exists.");

                var employee = _context.Employees.FirstOrDefault(e => e.uid == employeeId);
                if (employee == null)
                    throw new InvalidOperationException("Employee not found.");

                payslip = new Payslip
                {
                    EmployeeId = employeeId,
                    Month = monthName,
                    Year = year,
                    IsLocked = false,
                    LeaveBalance = "",
                    Notes = ""
                };
                Fill(payslip, ComputeFor(employee, monthName, year), generatedBy);

                _context.Payslips.Add(payslip);
                _context.SaveChanges();

                return payslip;
            }
            catch (Exception ex)
            {
                // A failed save leaves the new rows queued in the context, where they would make every later save fail too
                // (a bulk run uses one context for all employees).
                if (payslip != null)
                {
                    foreach (var line in payslip.PayslipDetails.ToList())
                        _context.Entry(line).State = EntityState.Detached;
                    _context.Entry(payslip).State = EntityState.Detached;
                }
                throw new Exception($"Payroll generation failed for employee {employeeId}: {ex.Message}", ex);
            }
        }

        /// <summary>A payslip typed in by hand has no lines and no working.</summary>
        public static bool IsManual(Payslip p) =>
            string.IsNullOrWhiteSpace(p.CalculationDetails) && p.PayslipDetails.Count == 0;

        /// <summary>
        /// Works an unlocked payslip out again from the current salary, allowances, deductions, fuel price and tax slabs (decision T4):
        /// in one transaction the payslip row is updated, its old lines are deleted and fresh ones inserted. Notes and attendance figures are kept.
        /// <paramref name="snapshot"/> is taken before the change and <paramref name="afterSave"/> runs inside the same transaction (the audit row).
        /// Returns a message when the payslip may not be regenerated; nothing is changed in that case.
        /// </summary>
        public async Task<string?> RegeneratePayslipAsync(int payslipId, string generatedBy,
            Func<Payslip, object> snapshot, Func<Payslip, object, Task> afterSave)
        {
            string? refusal = null;
            await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                refusal = null;
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync();

                var payslip = await _context.Payslips.Include(p => p.PayslipDetails).FirstOrDefaultAsync(p => p.Id == payslipId);
                if (payslip == null) { refusal = "Payslip not found."; return; }
                if (payslip.IsLocked) { refusal = "This payslip is locked and cannot be regenerated."; return; }
                if (IsManual(payslip)) { refusal = "This payslip was typed in by hand, so there is nothing to work out again."; return; }

                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.uid == payslip.EmployeeId);
                if (employee == null) { refusal = "Employee not found."; return; }
                var block = CheckEmployee(employee, payslip.Month, payslip.Year, slipExists: false).FirstOrDefault(i => i.Blocks);
                if (block != null) { refusal = block.Text; return; }

                var before = snapshot(payslip);
                var calc = ComputeFor(employee, payslip.Month, payslip.Year);

                // Delete first and save: the unique index on the lines would otherwise see old and new rows together.
                _context.PayslipDetails.RemoveRange(payslip.PayslipDetails.ToList());
                await _context.SaveChangesAsync();
                payslip.PayslipDetails.Clear();

                Fill(payslip, calc, generatedBy);
                await _context.SaveChangesAsync();
                await afterSave(payslip, before);
                await tx.CommitAsync();
            });
            return refusal;
        }

        private Calc ComputeFor(Employee employee, string monthName, int year)
        {
            var monthNum = PayrollMonthHelper.OrderIndex(monthName);
            if (monthNum < 1 || monthNum > 12)
                monthNum = DateTime.Now.Month;

            var uid = employee.uid;
            return Compute(
                employee,
                GetFuelPrice(new DateTime(year, monthNum, 1)),
                _context.Allowances.AsNoTracking().Where(a => a.EmployeeId == uid && a.IsActive).ToList(),
                _context.Deductions.AsNoTracking().Where(d => d.EmployeeId == uid && d.IsActive).ToList(),
                monthNum, year, TaxSlabsFromDatabase);
        }

        /// <summary>Puts the calculated figures and lines on a payslip that has no lines.</summary>
        private void Fill(Payslip payslip, Calc calc, string generatedBy)
        {
            payslip.BasicSalary = calc.Basic;
            payslip.TotalAllowances = calc.TotalAllowances;
            payslip.GrossSalary = calc.Gross;
            payslip.TaxPercentage = calc.Tax.TaxPercentage;
            payslip.TaxAmount = calc.Tax.TaxAmount;
            payslip.TotalDeductions = calc.TotalDeductions;
            payslip.NetSalary = calc.Net;
            payslip.GeneratedDate = DateTime.Now;
            payslip.GeneratedBy = generatedBy;
            payslip.CalculationDetails = BuildDetails(
                calc.Basic, calc.TotalAllowances, calc.Gross, calc.Allowances, calc.Deductions, calc.TotalDeductions,
                calc.Tax.TaxPercentage, calc.Tax.TaxAmount, calc.Tax.Text, calc.TotalWithheld, calc.Net, calc.FuelPrice);
            foreach (var line in BuildLines(calc))
                payslip.PayslipDetails.Add(line);
        }

        private static List<PayslipDetail> BuildLines(Calc calc)
        {
            var lines = new List<PayslipDetail>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string type, string name, string category, decimal amount, decimal? quantity = null, decimal? unit = null, string? note = null) =>
                lines.Add(new PayslipDetail
                {
                    ItemType = type,
                    ItemName = UniqueName(used, type, name),
                    ItemCategory = category,
                    Amount = amount,
                    Quantity = quantity,
                    UnitAmount = unit,
                    Note = note,
                    SortOrder = lines.Count + 1
                });

            foreach (var a in calc.Allowances)
            {
                var amount = AllowanceComputedAmount(a, calc.Basic, calc.FuelPrice);
                var qty = (decimal)(a.Quantity < 0 ? 0 : a.Quantity);
                if (a.IsPercentage)
                    Add("Allowance", a.Name, a.AllowanceType, amount, note: $"{Fmt.Percent(a.PercentageValue ?? 0m)} of basic");
                else if (UsesFuelPrice(a))
                    Add("Allowance", a.Name, a.AllowanceType, amount, qty, calc.FuelPrice);
                else if (IsFuelAllowanceName(a.Name) || IsFuelAllowanceType(a.AllowanceType))
                    Add("Allowance", a.Name, a.AllowanceType, amount); // a fixed fuel amount has no unit rate
                else
                    Add("Allowance", a.Name, a.AllowanceType, amount, qty, a.Amount);
            }

            foreach (var d in calc.Deductions)
            {
                var amount = Math.Round(DeductionComputedAmount(d, calc.Gross), 2, MidpointRounding.AwayFromZero);
                Add("Deduction", string.IsNullOrWhiteSpace(d.DeductionName) ? d.DeductionType : d.DeductionName, d.DeductionType, amount,
                    note: IsPercentageDeduction(d) ? $"{Fmt.Percent(d.PercentageValue ?? 0m)} of gross" : null);
            }

            if (calc.Tax.TaxAmount > 0)
                Add("Tax", "Income Tax", calc.Tax.Category, calc.Tax.TaxAmount);

            return lines;
        }

        /// <summary>
        /// A payslip may not hold two lines with the same type and name (UX_PayslipDetails_Payslip_Item), but an employee can have
        /// two rows of the same allowance or deduction and both are paid, so the second line is named "... (2)".
        /// </summary>
        private static string UniqueName(HashSet<string> used, string type, string name)
        {
            const int max = 200;
            var baseName = (name ?? string.Empty).Trim();
            if (baseName.Length > max - 6) baseName = baseName[..(max - 6)];
            var candidate = baseName;
            for (var n = 2; !used.Add(type + "|" + candidate); n++)
                candidate = $"{baseName} ({n})";
            return candidate;
        }

        /// <summary>
        /// Income tax by the annual slab method (see <see cref="IncomeTaxCalculator"/> and docs/reference/pk-income-tax.md):
        /// annual taxable income = BasicSalary x 12 (the tax base is still open, decision B17), tax = slab fixed amount + slab rate % of the part above the slab start,
        /// monthly tax = annual / 12 rounded to the rupee. The tax year is July-June (T6): July-December 2026 uses 2026-2027, January-June 2026 uses 2025-2026.
        /// ApplyTax must be affirmative (yes / y / true / 1). With no slabs for that tax year no tax is charged and the payslip working says so.
        /// TaxPercentage on the payslip is the slab rate (marginal), TaxAmount the monthly tax.
        /// </summary>
        private List<TaxSlab> TaxSlabsFromDatabase(string label) =>
            _context.TaxRules.AsNoTracking()
                .Where(r => r.TaxYear == label)
                .ToList()
                .Select(r => new TaxSlab(r.MinSalary, r.MaxSalary.HasValue && r.MaxSalary.Value > 0m ? r.MaxSalary : null, r.FixedAmount, r.TaxPercentage))
                .ToList();

        private sealed record Calc(
            decimal Basic, decimal FuelPrice, List<Allowance> Allowances, List<Deduction> Deductions,
            decimal TotalAllowances, decimal Gross, decimal TotalDeductions,
            (decimal TaxPercentage, decimal TaxAmount, string Category, string Text) Tax,
            decimal TotalWithheld, decimal Net);

        /// <summary>
        /// The one calculation used by both <see cref="GeneratePayslip"/> and <see cref="Preview"/>.
        /// Takes the employee's active allowance and deduction rows and keeps those that apply in the month.
        /// </summary>
        private Calc Compute(Employee employee, decimal fuelPrice, List<Allowance> activeAllowances, List<Deduction> activeDeductions,
            int monthNum, int year, Func<string, List<TaxSlab>> slabsFor)
        {
            var basic = employee.BasicSalary ?? 0m;
            var periodStart = new DateTime(year, monthNum, 1);
            var periodEndExclusive = periodStart.AddMonths(1);

            var allowances = activeAllowances
                .Where(a => AppliesInPeriod(a.Frequency, a.EffectiveDate, a.EndDate, periodStart, periodEndExclusive))
                .ToList();
            var deductions = activeDeductions
                .OrderBy(d => d.DeductionType)
                .ThenBy(d => d.DeductionName)
                .Where(d => AppliesInPeriod(d.Frequency, d.EffectiveDate, d.EndDate, periodStart, periodEndExclusive))
                .ToList();

            var totalAllowances = SumAllowancesForBasic(basic, allowances, fuelPrice);
            var gross = basic + totalAllowances;
            var totalDeductions = Math.Round(deductions.Sum(d => DeductionComputedAmount(d, gross)), 2, MidpointRounding.AwayFromZero);

            // Income tax: annual slab method on BasicSalary x 12 (B17 decides the base).
            var tax = CalculateTax(employee, basic, year, monthNum, slabsFor);
            var withheld = totalDeductions + tax.TaxAmount;
            return new Calc(basic, fuelPrice, allowances, deductions, totalAllowances, gross, totalDeductions, tax, withheld, gross - withheld);
        }

        /// <summary>A reason shown before payroll is generated. <see cref="Blocks"/> means no payslip is created.</summary>
        public sealed record PayrollIssue(bool Blocks, string Text);

        /// <summary>One employee in a payroll preview: what would happen and the figures it would produce.</summary>
        public sealed record PayrollPreviewRow(Employee Employee, bool WillCreate, decimal Gross, decimal Deductions, decimal Tax, decimal Net, List<PayrollIssue> Issues);

        /// <summary>
        /// The rules every payroll route applies before creating a payslip, so the routes cannot disagree.
        /// Blocks: employee not Active, payslip already exists. Warnings: no basic salary, month in the future,
        /// employee joined after the month or left before it, tax flag set but no slabs for that tax year, negative net.
        /// </summary>
        public static List<PayrollIssue> CheckEmployee(Employee employee, string monthName, int year, bool slipExists)
        {
            var issues = new List<PayrollIssue>();
            if (!string.Equals(employee.EmployeeStatus, "Active", StringComparison.OrdinalIgnoreCase))
                issues.Add(new PayrollIssue(true, $"Not active (status: {employee.EmployeeStatus})."));
            if (slipExists)
                issues.Add(new PayrollIssue(true, "A payslip already exists for this month."));

            if ((employee.BasicSalary ?? 0m) <= 0m)
                issues.Add(new PayrollIssue(false, "No basic salary set; the payslip would be 0.00."));

            var monthNum = PayrollMonthHelper.OrderIndex(monthName);
            if (monthNum >= 1 && monthNum <= 12)
            {
                var start = new DateTime(year, monthNum, 1);
                var end = start.AddMonths(1);
                var today = DateTime.Today;
                if (start > new DateTime(today.Year, today.Month, 1))
                    issues.Add(new PayrollIssue(false, "This month is in the future."));
                if (employee.DateOfJoining.HasValue && employee.DateOfJoining.Value.Date >= end)
                    issues.Add(new PayrollIssue(false, $"Joined {employee.DateOfJoining.Value:dd/MM/yyyy}, after this month; a full month would still be paid."));
                if (employee.DateOfLeaving.HasValue && employee.DateOfLeaving.Value.Date < start)
                    issues.Add(new PayrollIssue(false, $"Left {employee.DateOfLeaving.Value:dd/MM/yyyy}, before this month."));
            }
            return issues;
        }

        /// <summary>
        /// Dry run for a whole group of employees: nothing is saved. Uses the same calculation as generation,
        /// with the allowance, deduction, slip and tax-slab lookups done once for the group.
        /// </summary>
        public List<PayrollPreviewRow> Preview(IEnumerable<Employee> employees, string month, int year)
        {
            var list = employees.ToList();
            var rows = new List<PayrollPreviewRow>();
            if (list.Count == 0) return rows;

            var monthName = PayrollMonthHelper.Normalize(month);
            var monthNum = PayrollMonthHelper.OrderIndex(monthName);
            var ids = list.Select(e => e.uid).ToList();
            var fuelPrice = GetFuelPrice(new DateTime(year, monthNum, 1));
            var allowances = _context.Allowances.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.IsActive).ToList()
                .GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            var deductions = _context.Deductions.AsNoTracking().Where(d => ids.Contains(d.EmployeeId) && d.IsActive).ToList()
                .GroupBy(d => d.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            var existing = _context.Payslips.AsNoTracking()
                .Where(p => p.Month == monthName && p.Year == year && ids.Contains(p.EmployeeId))
                .Select(p => p.EmployeeId).ToHashSet();
            var slabCache = new Dictionary<string, List<TaxSlab>>();
            List<TaxSlab> SlabsFor(string label)
            {
                if (!slabCache.TryGetValue(label, out var s))
                    slabCache[label] = s = TaxSlabsFromDatabase(label);
                return s;
            }

            foreach (var e in list)
            {
                var issues = CheckEmployee(e, monthName, year, existing.Contains(e.uid));
                var calc = Compute(e, fuelPrice,
                    allowances.GetValueOrDefault(e.uid) ?? new List<Allowance>(),
                    deductions.GetValueOrDefault(e.uid) ?? new List<Deduction>(),
                    monthNum, year, SlabsFor);
                if (calc.FuelPrice <= 0m && calc.Allowances.Any(UsesFuelPrice))
                    issues.Add(new PayrollIssue(false, "No fuel price is set for this month; fuel by quantity would be 0.00."));
                if (calc.Net < 0m)
                    issues.Add(new PayrollIssue(false, "Net pay would be negative (deductions and tax are more than gross)."));
                if (ShouldApplyIncomeTax(e.ApplyTax) && calc.Tax.Text.Contains("no slabs are set up"))
                    issues.Add(new PayrollIssue(false, "Tax is switched on for this employee but the tax year has no slabs."));
                rows.Add(new PayrollPreviewRow(e, !issues.Any(i => i.Blocks), calc.Gross, calc.TotalDeductions, calc.Tax.TaxAmount, calc.Net, issues));
            }
            return rows;
        }

        private (decimal TaxPercentage, decimal TaxAmount, string Category, string Text) CalculateTax(Employee employee, decimal basicSalary, int payslipYear, int payslipMonth, Func<string, List<TaxSlab>> slabsFor)
        {
            var taxYearStart = payslipMonth >= 7 ? payslipYear : payslipYear - 1;
            var label = $"{taxYearStart}-{taxYearStart + 1}";

            if (!ShouldApplyIncomeTax(employee.ApplyTax))
                return (0m, 0m, "Income tax", "Income tax: not applied (employee is not marked for tax).");

            var slabs = slabsFor(label);

            if (slabs.Count == 0)
                return (0m, 0m, "Income tax", $"Income tax: no slabs are set up for tax year {label}, so no tax was charged.");

            var result = IncomeTaxCalculator.Calculate(basicSalary * IncomeTaxCalculator.MonthsPerYear, slabs);
            return (result.Slab?.Rate ?? 0m, result.MonthlyTax, IncomeTaxCalculator.Category(result), IncomeTaxCalculator.Describe(label, result));
        }

        private static bool ShouldApplyIncomeTax(string? applyTax)
        {
            if (string.IsNullOrWhiteSpace(applyTax))
                return false;

            var t = applyTax.Trim();

            return t.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || t.Equals("y", StringComparison.OrdinalIgnoreCase)
                   || t.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || t == "1";
        }

        private string BuildDetails(
            decimal basicSalary,
            decimal totalAllowancesSum,
            decimal grossSalary,
            List<Allowance> allowances,
            List<Deduction> deductions,
            decimal totalPayrollDeductions,
            decimal taxPercentage,
            decimal taxAmount,
            string taxText,
            decimal totalWithheld,
            decimal netSalary,
            decimal fuelPrice = 0m)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Basic Salary: {basicSalary:N2}");
            sb.AppendLine();
            sb.AppendLine("Allowances:");
            foreach (var a in allowances)
            {
                var amt = AllowanceComputedAmount(a, basicSalary, fuelPrice);
                string note;
                if (a.IsPercentage)
                    note = $" ({a.PercentageValue:N2}% of basic)";
                else if (IsFuelAllowanceName(a.Name) && IsFuelQuantityType(a.AllowanceType))
                    note = $" (FuelPrice {fuelPrice:N2} × Qty {a.Quantity})";
                else if (IsFuelAllowanceName(a.Name) && IsFuelFixedAmountType(a.AllowanceType))
                    note = " (fixed Fuel Allowance amount)";
                else if (IsFuelAllowanceType(a.AllowanceType) && a.Amount <= 0m)
                    note = $" (FuelPrice {fuelPrice:N2} × Qty {a.Quantity})";
                else if (IsFuelAllowanceType(a.AllowanceType))
                    note = " (fixed Fuel Allowance amount)";
                else
                    note = string.Empty;
                sb.AppendLine($"{a.Name}: {amt:N2}{note}");
            }

            sb.AppendLine($"Total Allowances: {totalAllowancesSum:N2}");
            sb.AppendLine($"Gross Salary: {grossSalary:N2}");
            sb.AppendLine();
            sb.AppendLine("Deductions (from Deductions table):");
            foreach (var d in deductions)
            {
                var amt = DeductionComputedAmount(d, grossSalary);
                var note = IsPercentageDeduction(d) ? $" ({d.PercentageValue:N2}% of gross)" : " (fixed)";
                sb.AppendLine($"{d.DeductionName}: {amt:N2}{note}");
            }

            sb.AppendLine($"Total payroll deductions (Payslips.TotalDeductions): {totalPayrollDeductions:N2}");
            sb.AppendLine(taxText);
            sb.AppendLine($"Tax withheld (slab rate {taxPercentage:N2}%): {taxAmount:N2}");
            sb.AppendLine($"Total withheld (payroll + tax): {totalWithheld:N2}");
            sb.AppendLine($"Net Salary: {netSalary:N2}");

            return sb.ToString();
        }
    }
}
