namespace HRMBT.Web.Models.ViewModels
{
    public class DepartmentEmployeeCount
    {
        public string Department { get; set; } = "Unassigned";
        public int Count { get; set; }
    }

    public class EmployeeDashboardVM
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }
        public int OnLeaveEmployees { get; set; }
        public int DepartmentCount { get; set; }
        public List<DepartmentEmployeeCount> ByDepartment { get; set; } = new();
        public DepartmentEmployeeCount? TopDepartment { get; set; }
    }
}
