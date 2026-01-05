using HRMBT.Web.Data;
using HRMBT.Web.Models;
using Microsoft.EntityFrameworkCore;
using System;
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
            if (_context.Payslips.Any(p => p.EmployeeId == employeeId && p.Month == month && p.Year == year))
                throw new Exception("Payslip already exists.");

            var employee = _context.Employees.FirstOrDefault(e => e.uid == employeeId);
            if (employee == null)
                throw new Exception("Employee not found.");

            decimal basic = employee.BasicSalary ?? 0;

            var allowances = _context.Allowances
                .Where(a => a.EmployeeId == employeeId && a.IsActive)
                .ToList();

            var deductions = _context.Deductions
                .Where(d => d.EmployeeId == employeeId && d.IsActive)
                .ToList();

            decimal totalAllowances = allowances.Sum(a =>
                a.IsPercentage
                    ? basic * (a.PercentageValue ?? 0) / 100
                    : a.Amount);

            decimal gross = basic + totalAllowances;

            decimal totalDeductions = deductions.Sum(d =>
                d.PercentageValue.HasValue
                    ? gross * (d.PercentageValue.Value / 100)
                    : d.Amount ?? 0);

            decimal tax = CalculateTax(employee, gross);

            var payslip = new Payslip
            {
                EmployeeId = employeeId,
                Month = month,
                Year = year,
                BasicSalary = basic,
                GrossSalary = gross,
                TotalDeductions = totalDeductions + tax,
                NetSalary = gross - totalDeductions - tax,
                GeneratedDate = DateTime.Now,
                GeneratedBy = generatedBy,
                IsLocked = false,
                LeaveBalance = "",
                Notes = "",
                CalculationDetails = BuildDetails(allowances, deductions, tax)
            };

            _context.Payslips.Add(payslip);
            _context.SaveChanges();

            return payslip;
        }

        private decimal CalculateTax(Employee employee, decimal gross)
        {
            // FR-003: Tax MUST integrate with Payroll using TaxRule
            if (employee.ApplyTax != "1" && employee.ApplyTax != "Yes") return 0;

            // Get tax rules from database, ordered by MinSalary ascending
            var taxRules = _context.TaxRules
                .OrderBy(t => t.MinSalary)
                .ToList();

            if (!taxRules.Any())
            {
                // Fallback to hardcoded rules if no tax rules exist
                if (gross <= 50000) return 0;
                if (gross <= 100000) return gross * 0.05m;
                return gross * 0.10m;
            }

            // Find applicable tax rule
            TaxRule? applicableRule = null;
            foreach (var rule in taxRules)
            {
                if (gross >= rule.MinSalary)
                {
                    if (rule.MaxSalary == null || gross <= rule.MaxSalary)
                    {
                        applicableRule = rule;
                        break;
                    }
                }
            }

            // If no rule found, use the highest bracket (last rule with no MaxSalary or highest MaxSalary)
            if (applicableRule == null)
            {
                applicableRule = taxRules
                    .Where(r => r.MaxSalary == null || gross > r.MaxSalary)
                    .OrderByDescending(r => r.MinSalary)
                    .FirstOrDefault() 
                    ?? taxRules.OrderByDescending(r => r.MinSalary).First();
            }

            // Calculate tax based on percentage
            return gross * (applicableRule.TaxPercentage / 100);
        }

        private string BuildDetails(
            System.Collections.Generic.List<Allowance> allowances,
            System.Collections.Generic.List<Deduction> deductions,
            decimal tax)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Allowances:");
            foreach (var a in allowances)
                sb.AppendLine($"{a.Name}: {a.Amount}");

            sb.AppendLine("Deductions:");
            foreach (var d in deductions)
                sb.AppendLine($"{d.Name}: {d.Amount}");

            sb.AppendLine($"Tax: {tax}");

            return sb.ToString();
        }
    }
}
