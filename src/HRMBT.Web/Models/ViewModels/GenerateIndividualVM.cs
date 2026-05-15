using HRMBT.Web.Infrastructure;

namespace HRMBT.Web.Models.ViewModels
{
    public class GenerateIndividualVM
    {
        public string EmployeeID { get; set; } = string.Empty;
        public string Month { get; set; } = PayrollMonthHelper.CurrentMonthName();
        public int Year { get; set; }
    }
}


