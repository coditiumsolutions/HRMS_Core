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

        public Payslip GeneratePayslip(int employeeId, int month, int year, string user)
        {
            if (_context.Payslips.Any(p => p.EmployeeId == employeeId && p.Month == month && p.Year == year))
                throw new Exception("Payslip already exists.");

            var employee = _context.Employees.Find(employeeId);
            if (employee == null)
                throw new Exception("Employee not found.");

            var allowances = _context.Allowances
                .Where(a => a.EmployeeId == employeeId && a.IsActive)
                .ToList();

            var deductions = _context.Deductions
                .Where(d => d.EmployeeId == employeeId && d.IsActive)
                .ToList();

            decimal basic = employee.BasicSalary ?? 0;
            decimal totalAllowances = allowances.Sum(a =>
                a.IsPercentage ? basic * (a.PercentageValue ?? 0) / 100 : a.Amount);

            decimal totalDeductions = deductions.Sum(d =>
                d.PercentageValue.HasValue ? basic * d.PercentageValue.Value / 100 : d.Amount ?? 0);

            var payslip = new Payslip
            {
                EmployeeId = employeeId,
                Month = month,
                Year = year,
                BasicSalary = basic,
                GrossSalary = basic + totalAllowances,
                TotalDeductions = totalDeductions,
                NetSalary = (basic + totalAllowances) - totalDeductions,
                // These columns are NOT NULL in the current DB migration
                LeaveBalance = string.Empty,
                GeneratedDate = DateTime.Now,
                GeneratedBy = user,
                IsLocked = false,
                Notes = string.Empty,
                CalculationDetails = BuildCalculationDetails(allowances, deductions)
            };

            _context.Payslips.Add(payslip);
            _context.SaveChanges();

            return payslip;
        }

        private string BuildCalculationDetails(
            System.Collections.Generic.List<Allowance> allowances,
            System.Collections.Generic.List<Deduction> deductions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Allowances:");
            allowances.ForEach(a => sb.AppendLine($"{a.Name}: {a.Amount}"));
            sb.AppendLine("Deductions:");
            deductions.ForEach(d => sb.AppendLine($"{d.Name}: {d.Amount}"));
            return sb.ToString();
        }
    }
}
