using System;
using System.ComponentModel.DataAnnotations;

namespace HRMBT.Web.Models
{
    /// <summary>Maps to dbo.FuelPrices: the unit price of fuel from a date (decision T9). Payroll uses the price in force on the 1st of the payslip month.</summary>
    public class FuelPrice
    {
        [Key]
        public int Id { get; set; }

        public DateTime EffectiveFrom { get; set; }

        public decimal UnitPrice { get; set; }

        public DateTime CreatedAt { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }
}
