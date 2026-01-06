using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models;

public class Department
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Display(Name = "Department ID")]
    public int DepartmentID { get; set; }

    [Display(Name = "Department Name")]
    [StringLength(100)]
    public string? DepartmentName { get; set; }
}

