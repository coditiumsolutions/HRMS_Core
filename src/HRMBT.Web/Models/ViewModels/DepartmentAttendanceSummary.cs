namespace HRMBT.Web.Models.ViewModels
{
    public class DepartmentAttendanceSummary
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int TotalEmployees { get; set; }
        public int Present { get; set; }
        public int Absent { get; set; }
        public int Late { get; set; }
        public int Holiday { get; set; }
        public int NotMarked { get; set; }
    }
}

