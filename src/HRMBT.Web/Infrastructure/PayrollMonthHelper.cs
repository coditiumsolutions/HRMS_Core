using System.Globalization;
using HRMBT.Web.Models;

namespace HRMBT.Web.Infrastructure;

/// <summary>
/// Month names stored in dbo.Payslips.[Month] (nvarchar), e.g. January, February.
/// </summary>
public static class PayrollMonthHelper
{
    public static readonly string[] AllMonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    public static string CurrentMonthName() =>
        AllMonthNames[DateTime.Now.Month - 1];

    public static bool IsValid(string? month)
    {
        if (string.IsNullOrWhiteSpace(month)) return false;
        return AllMonthNames.Any(m => m.Equals(month.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Maps legacy numeric month (1–12) or name to canonical English month name.</summary>
    public static string Normalize(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
            return CurrentMonthName();

        var trimmed = month.Trim();
        if (int.TryParse(trimmed, out int n) && n >= 1 && n <= 12)
            return AllMonthNames[n - 1];

        var match = AllMonthNames.FirstOrDefault(m =>
            m.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        return match ?? trimmed;
    }

    public static string Display(string? month) => Normalize(month);

    public static int OrderIndex(string? month)
    {
        var normalized = Normalize(month);
        for (int i = 0; i < AllMonthNames.Length; i++)
        {
            if (AllMonthNames[i].Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }
        return 0;
    }

    public static IQueryable<Payslip> OrderByPeriodDescending(this IQueryable<Payslip> query) =>
        query
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p =>
                p.Month == "December" ? 12 :
                p.Month == "November" ? 11 :
                p.Month == "October" ? 10 :
                p.Month == "September" ? 9 :
                p.Month == "August" ? 8 :
                p.Month == "July" ? 7 :
                p.Month == "June" ? 6 :
                p.Month == "May" ? 5 :
                p.Month == "April" ? 4 :
                p.Month == "March" ? 3 :
                p.Month == "February" ? 2 :
                p.Month == "January" ? 1 : 0)
            .ThenBy(p => p.Employee != null ? p.Employee.EmployeeName : string.Empty);

    public static string FormatPeriod(string? month, int year) =>
        $"{Display(month)} {year}";
}
