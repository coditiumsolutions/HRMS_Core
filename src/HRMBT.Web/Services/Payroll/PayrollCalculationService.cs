using System.Globalization;
using HRMBT.Web.Data;
using HRMBT.Web.Infrastructure;
using HRMBT.Web.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// Fuel Allowance + Quantity → FuelPrice (config) × Quantity.
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

        /// <summary>Unit price from dbo.Configuration where ConfigKey = FuelPrice (0 if missing).</summary>
        public decimal GetFuelPriceFromConfiguration()
        {
            var rows = _context.HrConfigurations
                .AsNoTracking()
                .Where(c => c.ConfigKey != null && c.ConfigValue != null)
                .ToList();

            var raw = rows
                .FirstOrDefault(c => string.Equals((c.ConfigKey ?? "").Trim(), "FuelPrice", StringComparison.OrdinalIgnoreCase))
                ?.ConfigValue;

            if (string.IsNullOrWhiteSpace(raw))
                return 0m;

            var token = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? raw.Trim();

            return decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                ? price
                : 0m;
        }

        /// <summary>Preview gross (basic + active allowances) per employee — matches <see cref="GeneratePayslip"/> gross before deductions.</summary>
        public Dictionary<int, decimal> ComputeGrossPreviewForEmployees(IEnumerable<Employee> employees, DateTime? periodMonth = null)
        {
            var periodStart = new DateTime((periodMonth ?? DateTime.Today).Year, (periodMonth ?? DateTime.Today).Month, 1);
            var periodEndExclusive = periodStart.AddMonths(1);
            var list = employees?.ToList() ?? new List<Employee>();
            var result = new Dictionary<int, decimal>();
            if (list.Count == 0) return result;

            var fuelPrice = GetFuelPriceFromConfiguration();
            var ids = list.Select(e => e.uid).ToList();
            var byEmpId = _context.Allowances.AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.IsActive)
                .ToList()
                .Where(a => AppliesInPeriod(a.Frequency, a.EffectiveDate, a.EndDate, periodStart, periodEndExclusive))
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var emp in list)
            {
                var basic = emp.BasicSalary ?? 0m;
                byEmpId.TryGetValue(emp.uid, out var allowances);
                allowances ??= new List<Allowance>();
                result[emp.uid] = basic + SumAllowancesForBasic(basic, allowances, fuelPrice);
            }

            return result;
        }

        public Payslip GeneratePayslip(int employeeId, string month, int year, string generatedBy)
        {
            try
            {
                var monthName = PayrollMonthHelper.Normalize(month);
                if (_context.Payslips.Any(p => p.EmployeeId == employeeId && p.Month == monthName && p.Year == year))
                    throw new InvalidOperationException("Payslip already exists.");

                var employee = _context.Employees.FirstOrDefault(e => e.uid == employeeId);
                if (employee == null)
                    throw new InvalidOperationException("Employee not found.");

                decimal basic = employee.BasicSalary ?? 0m;
                decimal fuelPrice = GetFuelPriceFromConfiguration();

                var monthNum = PayrollMonthHelper.OrderIndex(monthName);
                if (monthNum < 1 || monthNum > 12)
                    monthNum = DateTime.Now.Month;
                var periodStart = new DateTime(year, monthNum, 1);
                var periodEndExclusive = periodStart.AddMonths(1);

                // Active allowances and deductions that apply in this payroll month (effective/end dates and frequency)
                var allowances = _context.Allowances
                    .Where(a => a.EmployeeId == employeeId && a.IsActive)
                    .ToList()
                    .Where(a => AppliesInPeriod(a.Frequency, a.EffectiveDate, a.EndDate, periodStart, periodEndExclusive))
                    .ToList();

                var deductions = _context.Deductions
                    .Where(d => d.EmployeeId == employeeId && d.IsActive)
                    .OrderBy(d => d.DeductionType)
                    .ThenBy(d => d.DeductionName)
                    .ToList()
                    .Where(d => AppliesInPeriod(d.Frequency, d.EffectiveDate, d.EndDate, periodStart, periodEndExclusive))
                    .ToList();

                decimal totalAllowances = SumAllowancesForBasic(basic, allowances, fuelPrice);

                decimal gross = basic + totalAllowances;

                decimal totalPayrollDeductions = deductions.Sum(d => DeductionComputedAmount(d, gross));
                totalPayrollDeductions = Math.Round(totalPayrollDeductions, 2, MidpointRounding.AwayFromZero);

                // Income tax: annual slab method on BasicSalary x 12 (B17 decides the base).
                var taxResult = CalculateTax(employee, basic, year, monthNum);

                decimal totalWithheld = totalPayrollDeductions + taxResult.TaxAmount;
                decimal netSalary = gross - totalWithheld;

                var payslip = new Payslip
                {
                    EmployeeId = employeeId,
                    Month = monthName,
                    Year = year,
                    BasicSalary = basic,
                    TotalAllowances = totalAllowances,
                    GrossSalary = gross,
                    TaxPercentage = taxResult.TaxPercentage,
                    TaxAmount = taxResult.TaxAmount,
                    TotalDeductions = totalPayrollDeductions,
                    NetSalary = netSalary,
                    GeneratedDate = DateTime.Now,
                    GeneratedBy = generatedBy,
                    IsLocked = false,
                    LeaveBalance = "",
                    Notes = "",
                    CalculationDetails = BuildDetails(
                        basic,
                        totalAllowances,
                        gross,
                        allowances,
                        deductions,
                        totalPayrollDeductions,
                        taxResult.TaxPercentage,
                        taxResult.TaxAmount,
                        taxResult.Text,
                        totalWithheld,
                        netSalary,
                        fuelPrice)
                };

                int sort = 1;
                foreach (var a in allowances)
                {
                    var lineAmount = AllowanceComputedAmount(a, basic, fuelPrice);
                    payslip.PayslipDetails.Add(new PayslipDetail
                    {
                        ItemType = "Allowance",
                        ItemName = a.Name,
                        ItemCategory = a.AllowanceType,
                        Amount = lineAmount,
                        SortOrder = sort++
                    });
                }

                foreach (var d in deductions)
                {
                    var lineAmount = Math.Round(DeductionComputedAmount(d, gross), 2, MidpointRounding.AwayFromZero);
                    payslip.PayslipDetails.Add(new PayslipDetail
                    {
                        ItemType = "Deduction",
                        ItemName = string.IsNullOrWhiteSpace(d.DeductionName) ? d.DeductionType : d.DeductionName,
                        ItemCategory = d.DeductionType,
                        Amount = lineAmount,
                        SortOrder = sort++
                    });
                }

                if (taxResult.TaxAmount > 0)
                {
                    payslip.PayslipDetails.Add(new PayslipDetail
                    {
                        ItemType = "Tax",
                        ItemName = "Income Tax",
                        ItemCategory = taxResult.Category,
                        Amount = taxResult.TaxAmount,
                        SortOrder = sort++
                    });
                }

                _context.Payslips.Add(payslip);
                _context.SaveChanges();

                return payslip;
            }
            catch (Exception ex)
            {
                throw new Exception($"Payroll generation failed for employee {employeeId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Income tax by the annual slab method (see <see cref="IncomeTaxCalculator"/> and docs/reference/pk-income-tax.md):
        /// annual taxable income = BasicSalary x 12 (the tax base is still open, decision B17), tax = slab fixed amount + slab rate % of the part above the slab start,
        /// monthly tax = annual / 12 rounded to the rupee. The tax year is July-June (T6): July-December 2026 uses 2026-2027, January-June 2026 uses 2025-2026.
        /// ApplyTax must be affirmative (yes / y / true / 1). With no slabs for that tax year no tax is charged and the payslip working says so.
        /// TaxPercentage on the payslip is the slab rate (marginal), TaxAmount the monthly tax.
        /// </summary>
        private (decimal TaxPercentage, decimal TaxAmount, string Category, string Text) CalculateTax(Employee employee, decimal basicSalary, int payslipYear, int payslipMonth)
        {
            var taxYearStart = payslipMonth >= 7 ? payslipYear : payslipYear - 1;
            var label = $"{taxYearStart}-{taxYearStart + 1}";

            if (!ShouldApplyIncomeTax(employee.ApplyTax))
                return (0m, 0m, "Income tax", "Income tax: not applied (employee is not marked for tax).");

            var slabs = _context.TaxRules.AsNoTracking()
                .Where(r => r.TaxYear == label)
                .ToList()
                .Select(r => new TaxSlab(r.MinSalary, r.MaxSalary.HasValue && r.MaxSalary.Value > 0m ? r.MaxSalary : null, r.FixedAmount, r.TaxPercentage))
                .ToList();

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
