namespace HRMBT.Web.Models.ViewModels
{
    public class EmployeeMonthlySummary
    {
        public string EmployeeID { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public int TotalLate { get; set; }
        public int TotalHoliday { get; set; }
    }
}

