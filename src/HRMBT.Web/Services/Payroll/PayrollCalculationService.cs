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

        /// <summary>Computed monetary amount for one allowance row (percentage of basic vs fixed).</summary>
        public static decimal AllowanceComputedAmount(Allowance a, decimal basicSalary) =>
            a.IsPercentage
                ? basicSalary * (a.PercentageValue ?? 0m) / 100m
                : a.Amount;

        /// <summary>Percentage of gross when <see cref="Deduction.CalculationMethod"/> is Percentage; otherwise fixed PKR from <see cref="Deduction.PercentageValue"/> (dbo has no Amount column — see db.txt).</summary>
        public static bool IsPercentageDeduction(Deduction d) =>
            string.Equals(d.CalculationMethod, "Percentage", StringComparison.OrdinalIgnoreCase);

        public static decimal DeductionComputedAmount(Deduction d, decimal grossSalary) =>
            IsPercentageDeduction(d)
                ? grossSalary * (d.PercentageValue ?? 0m) / 100m
                : (d.PercentageValue ?? 0m);

        /// <summary>Active allowance total using the same rules as payslip gross (percentage of basic vs fixed amount).</summary>
        public static decimal SumAllowancesForBasic(decimal basic, IEnumerable<Allowance> allowances) =>
            allowances.Sum(a => AllowanceComputedAmount(a, basic));

        /// <summary>Preview gross (basic + active allowances) per employee — matches <see cref="GeneratePayslip"/> gross before deductions.</summary>
        public Dictionary<int, decimal> ComputeGrossPreviewForEmployees(IEnumerable<Employee> employees)
        {
            var list = employees?.ToList() ?? new List<Employee>();
            var result = new Dictionary<int, decimal>();
            if (list.Count == 0) return result;

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
                result[emp.uid] = basic + SumAllowancesForBasic(basic, allowances);
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

                var allowances = _context.Allowances
                    .Where(a => a.EmployeeId == employeeId && a.IsActive)
                    .ToList();

                var deductions = _context.Deductions
                    .Where(d => d.EmployeeId == employeeId && d.IsActive)
                    .ToList();

                decimal totalAllowances = SumAllowancesForBasic(basic, allowances);

                decimal gross = basic + totalAllowances;

                decimal totalPayrollDeductions = deductions.Sum(d => DeductionComputedAmount(d, gross));

                // Tax is calculated on BasicSalary as requested.
                var taxResult = CalculateTax(employee, basic);

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
                        netSalary)
                };

                int sort = 1;
                foreach (var a in allowances)
                {
                    var lineAmount = AllowanceComputedAmount(a, basic);
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
                    var lineAmount = DeductionComputedAmount(d, gross);
                    payslip.PayslipDetails.Add(new PayslipDetail
                    {
                        ItemType = "Deduction",
                        ItemName = d.DeductionName,
                        ItemCategory = d.DeductionType,
                        Amount = lineAmount,
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
        /// ApplyTax must be affirmative (yes / y / true / 1); blank/null does not deduct tax — set the flag on the employee row.
        /// Chooses the slab with the greatest MinSalary whose band still contains the salary (proper bracket resolution).
        /// MaxSalary unset or zero is treated as no upper ceiling.
        /// </summary>
        private (decimal TaxPercentage, decimal TaxAmount) CalculateTax(Employee employee, decimal basicSalary)
        {
            if (!ShouldApplyIncomeTax(employee.ApplyTax))
                return (0m, 0m);

            var applicableRule = FindApplicableTaxRule(basicSalary);
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
        /// Loads rules once; picks the slab with largest MinSalary bracket containing basicSalary.
        /// </summary>
        private TaxRule? FindApplicableTaxRule(decimal basicSalary)
        {
            var rules = _context.TaxRules.AsNoTracking().ToList();

            return rules
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
            decimal netSalary)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Basic Salary: {basicSalary:N2}");
            sb.AppendLine();
            sb.AppendLine("Allowances:");
            foreach (var a in allowances)
            {
                var amt = AllowanceComputedAmount(a, basicSalary);
                var pctNote = a.IsPercentage ? $" ({a.PercentageValue:N2}% of basic)" : string.Empty;
                sb.AppendLine($"{a.Name}: {amt:N2}{pctNote}");
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
