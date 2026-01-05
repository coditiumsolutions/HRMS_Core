using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models.ViewModels
{
    public class PayrollStatusVM
    {
        public string Year { get; set; } = "Current Year";
        
        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
        public int? Month { get; set; }
        
        public string? Department { get; set; }
        
        public string PayrollStatus { get; set; } = "Payroll Not Generated";
    }
}


