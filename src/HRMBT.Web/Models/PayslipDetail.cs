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
        /// <summary>Quantity and unit amount behind <see cref="Amount"/>, when the line was worked out that way (T9).</summary>
        public decimal? Quantity { get; set; }
        public decimal? UnitAmount { get; set; }
        /// <summary>Short working, for example "5.00% of gross".</summary>
        public string? Note { get; set; }
        public int? SortOrder { get; set; }

        [ForeignKey("PayslipId")]
        public Payslip Payslip { get; set; } = null!;
    }
}
