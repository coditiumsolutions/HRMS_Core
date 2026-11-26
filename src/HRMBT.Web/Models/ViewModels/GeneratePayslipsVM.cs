using HRMBT.Web.Infrastructure;

namespace HRMBT.Web.Models.ViewModels
{
    public class GeneratePayslipsVM
    {
        public string Month { get; set; } = PayrollMonthHelper.CurrentMonthName();
        public int Year { get; set; }
        public string? Department { get; set; }

        /// <summary>When filled, the run is for this one employee and the department is ignored.</summary>
        public string? EmployeeID { get; set; }

        /// <summary>Set by the preview: only the ticked employees (<see cref="SelectedIds"/>) are generated.</summary>
        public bool FromPreview { get; set; }

        /// <summary>Employee uids ticked on the preview, comma separated (one field, so a large run stays within the form limit).</summary>
        public string? SelectedIds { get; set; }

        public bool IsSingleEmployee => !string.IsNullOrWhiteSpace(EmployeeID);
    }
}
