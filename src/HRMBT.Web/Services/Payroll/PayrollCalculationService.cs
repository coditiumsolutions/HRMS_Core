using HRMBT.Web.Data;
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

        public Payslip GeneratePayslip(int employeeId, int month, int year, string generatedBy)
        {
            try
            {
                if (_context.Payslips.Any(p => p.EmployeeId == employeeId && p.Month == month && p.Year == year))
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

                decimal totalAllowances = allowances.Sum(a =>
                    a.IsPercentage
                        ? basic * (a.PercentageValue ?? 0m) / 100m
                        : a.Amount);

                decimal gross = basic + totalAllowances;

                decimal payrollDeductions = deductions.Sum(d =>
                    d.PercentageValue.HasValue
                        ? gross * (d.PercentageValue.Value / 100m)
                        : d.Amount ?? 0m);

                // Tax is calculated on BasicSalary as requested.
                var taxResult = CalculateTax(employee, basic);

                decimal totalDeductions = payrollDeductions + taxResult.TaxAmount;
                decimal netSalary = gross - totalDeductions;

                var payslip = new Payslip
                {
                    EmployeeId = employeeId,
                    Month = month,
                    Year = year,
                    BasicSalary = basic,
                    GrossSalary = gross,
                    TaxPercentage = taxResult.TaxPercentage,
                    TaxAmount = taxResult.TaxAmount,
                    TotalDeductions = totalDeductions,
                    NetSalary = netSalary,
                    GeneratedDate = DateTime.Now,
                    GeneratedBy = generatedBy,
                    IsLocked = false,
                    LeaveBalance = "",
                    Notes = "",
                    CalculationDetails = BuildDetails(
                        basic,
                        gross,
                        allowances,
                        deductions,
                        payrollDeductions,
                        taxResult.TaxPercentage,
                        taxResult.TaxAmount,
                        totalDeductions,
                        netSalary)
                };

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
            decimal grossSalary,
            List<Allowance> allowances,
            List<Deduction> deductions,
            decimal payrollDeductions,
            decimal taxPercentage,
            decimal taxAmount,
            decimal totalDeductions,
            decimal netSalary)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Basic Salary: {basicSalary:N2}");
            sb.AppendLine();
            sb.AppendLine("Allowances:");
            foreach (var a in allowances)
                sb.AppendLine($"{a.Name}: {a.Amount}");

            sb.AppendLine($"Gross Salary: {grossSalary:N2}");
            sb.AppendLine();
            sb.AppendLine("Deductions:");
            foreach (var d in deductions)
                sb.AppendLine($"{d.Name}: {d.Amount}");

            sb.AppendLine($"Payroll Deductions Total: {payrollDeductions:N2}");
            sb.AppendLine($"Tax ({taxPercentage:N2}% of BasicSalary): {taxAmount:N2}");
            sb.AppendLine($"Total Deductions: {totalDeductions:N2}");
            sb.AppendLine($"Net Salary: {netSalary:N2}");

            return sb.ToString();
        }
    }
}
