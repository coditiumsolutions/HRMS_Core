using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMBT.Web.Models
{
    public class PayslipDetail
    {
        [Key]
        public int Id { get; set; }
        public int PayslipId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemCategory { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? SortOrder { get; set; }

        [ForeignKey("PayslipId")]
        public Payslip Payslip { get; set; } = null!;
    }
}
