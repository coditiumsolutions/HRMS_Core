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

        /// <summary>Percentage of gross when <see cref="Deduction.CalculationMethod"/> is Percentage; otherwise fixed PKR from <see cref="Deduction.PercentageValue"/> (dbo has no Amount column — see db.txt).</summary>
        public static bool IsPercentageDeduction(Deduction d) =>
            string.Equals(d.CalculationMethod, "Percentage", StringComparison.OrdinalIgnoreCase);

        public static decimal DeductionComputedAmount(Deduction d, decimal grossSalary) =>
            IsPercentageDeduction(d)
                ? grossSalary * (d.PercentageValue ?? 0m) / 100m
                : (d.PercentageValue ?? 0m);

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
        public Dictionary<int, decimal> ComputeGrossPreviewForEmployees(IEnumerable<Employee> employees)
        {
            var list = employees?.ToList() ?? new List<Employee>();
            var result = new Dictionary<int, decimal>();
            if (list.Count == 0) return result;

            var fuelPrice = GetFuelPriceFromConfiguration();
            var ids = list.Select(e => e.uid).ToList();
            var byEmpId = _context.Allowances.AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.IsActive)
                .ToList()
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

                var allowances = _context.Allowances
                    .Where(a => a.EmployeeId == employeeId && a.IsActive)
                    .ToList();

                var monthNum = PayrollMonthHelper.OrderIndex(monthName);
                if (monthNum < 1 || monthNum > 12)
                    monthNum = DateTime.Now.Month;
                var periodStart = new DateTime(year, monthNum, 1);
                var periodEnd = periodStart.AddMonths(1).AddDays(-1);

                // Every active deduction that applies in this payroll month
                var deductions = _context.Deductions
                    .Where(d => d.EmployeeId == employeeId && d.IsActive
                        && d.EffectiveDate <= periodEnd
                        && (d.EndDate == null || d.EndDate >= periodStart))
                    .OrderBy(d => d.DeductionType)
                    .ThenBy(d => d.DeductionName)
                    .ToList();

                decimal totalAllowances = SumAllowancesForBasic(basic, allowances, fuelPrice);

                decimal gross = basic + totalAllowances;

                decimal totalPayrollDeductions = deductions.Sum(d => DeductionComputedAmount(d, gross));
                totalPayrollDeductions = Math.Round(totalPayrollDeductions, 2, MidpointRounding.AwayFromZero);

                // Tax is calculated on BasicSalary as requested.
                var taxResult = CalculateTax(employee, basic, year);

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
                        ItemCategory = $"{taxResult.TaxPercentage:N2}% of Basic",
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
        /// Matches <see cref="TaxRule"/> to basic salary only: TaxAmount = BasicSalary × TaxPercentage / 100.
        /// Rules are filtered by <see cref="TaxRule.TaxYear"/> when it matches the payroll year (canonical: <c>year.ToString(CultureInfo.InvariantCulture)</c>).
        /// If no rows match that year (legacy databases), falls back to all rules.
        /// ApplyTax must be affirmative (yes / y / true / 1); blank/null does not deduct tax — set the flag on the employee row.
        /// Chooses the slab with the greatest MinSalary whose band still contains the salary (proper bracket resolution).
        /// MaxSalary unset or zero is treated as no upper ceiling.
        /// </summary>
        private (decimal TaxPercentage, decimal TaxAmount) CalculateTax(Employee employee, decimal basicSalary, int payslipYear)
        {
            if (!ShouldApplyIncomeTax(employee.ApplyTax))
                return (0m, 0m);

            var applicableRule = FindApplicableTaxRule(basicSalary, payslipYear);
            if (applicableRule == null)
                return (0m, 0m);

            decimal percentage = applicableRule.TaxPercentage;
            decimal taxAmount = Math.Round((basicSalary * percentage) / 100m, 2, MidpointRounding.AwayFromZero);
            return (percentage, taxAmount);
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

        /// <summary>
        /// MaxSalary NULL or non-positive values are treated as no upper bound (common data-entry pattern).
        /// </summary>
        private static decimal EffectiveMaxInclusive(decimal? maxSalary)
        {
            if (!maxSalary.HasValue || maxSalary.Value <= 0m)
                return decimal.MaxValue;

            return maxSalary.Value;
        }

        /// <summary>
        /// Loads rules; prefers rows whose <see cref="TaxRule.TaxYear"/> matches the payslip year. If none match (legacy DB), uses all rules.
        /// Picks the slab with largest MinSalary bracket containing basicSalary.
        /// </summary>
        private TaxRule? FindApplicableTaxRule(decimal basicSalary, int payslipYear)
        {
            var rules = _context.TaxRules.AsNoTracking().ToList();
            var yearKey = payslipYear.ToString(CultureInfo.InvariantCulture);

            var labeled = rules.Where(r => !string.IsNullOrWhiteSpace(r.TaxYear)).ToList();
            var forYear = labeled
                .Where(r => string.Equals(r.TaxYear.Trim(), yearKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            List<TaxRule> pool;
            if (forYear.Count > 0)
                pool = forYear;
            else
            {
                var wildcards = rules.Where(r => string.IsNullOrWhiteSpace(r.TaxYear)).ToList();
                pool = wildcards.Count > 0 ? wildcards : rules;
            }

            return pool
                .Where(r => basicSalary >= r.MinSalary && basicSalary <= EffectiveMaxInclusive(r.MaxSalary))
                .OrderByDescending(r => r.MinSalary)
                .FirstOrDefault();
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
            sb.AppendLine($"Tax ({taxPercentage:N2}% of BasicSalary): {taxAmount:N2}");
            sb.AppendLine($"Total withheld (payroll + tax): {totalWithheld:N2}");
            sb.AppendLine($"Net Salary: {netSalary:N2}");

            return sb.ToString();
        }
    }
}
