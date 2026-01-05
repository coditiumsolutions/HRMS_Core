namespace HRMBT.Web.Models.ViewModels
{
    public class PayrollDashboardVM
    {
        public int TotalEmployees { get; set; }
        public int EmployeesWithPayrollGenerated { get; set; }
        public int EmployeesWithoutPayroll { get; set; }
        public decimal TotalPayrollAmount { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
    }
}


