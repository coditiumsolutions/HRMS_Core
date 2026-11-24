namespace HRMBT.Web.Models.ViewModels
{
    public class DepartmentEmployeeCount
    {
        public string Department { get; set; } = "Unassigned";
        public int Count { get; set; }
    }

    /// <summary>One pie card on the dashboard: the top slices, already grouped.</summary>
    public class HeadcountPieVm
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-pie-chart";
        public string Summary { get; set; } = string.Empty;
        public int Total { get; set; }
        public List<(string Name, int Count)> Slices { get; set; } = new();
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

        /// <summary>Headcount per project (the <c>Department</c> field holds the project name).</summary>
        public List<DepartmentEmployeeCount> ByProject { get; set; } = new();
    }
}
