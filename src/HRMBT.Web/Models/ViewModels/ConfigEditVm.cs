using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models.ViewModels;

public class ConfigEditVm
{
    public int UID { get; set; }

    [Display(Name = "Config key")]
    [StringLength(50)]
    public string? ConfigKey { get; set; }

    [Display(Name = "Config value")]
    [DataType(DataType.MultilineText)]
    public string? ConfigValue { get; set; }
}
