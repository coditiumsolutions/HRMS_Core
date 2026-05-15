using HRMBT.Web.Infrastructure;

namespace HRMBT.Web.Models.ViewModels
{
    public class GeneratePayslipsVM
    {
        public string Month { get; set; } = PayrollMonthHelper.CurrentMonthName();
        public int Year { get; set; }
        public string? Department { get; set; }
    }
}
