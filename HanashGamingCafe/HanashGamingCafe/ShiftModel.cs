using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace HanashGamingCafe
{
    [Table("shifts")]
    public class ShiftModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("cashier_name")]
        public string CashierName { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime? EndTime { get; set; }

        [Column("initial_cash")]
        public decimal InitialCash { get; set; }

        [Column("total_sales")]
        public decimal TotalSales { get; set; }

        [Column("total_expenses")]
        public decimal TotalExpenses { get; set; }

        [Column("expected_cash")]
        public decimal ExpectedCash { get; set; }

        [Column("actual_cash")]
        public decimal ActualCash { get; set; }

        [Column("difference")]
        public decimal Difference { get; set; }

        [Column("status")]
        public string Status { get; set; } = "open";

        [Column("notes")]
        public string Notes { get; set; }
        [Column("shift_id")]
        public long? ShiftId { get; set; }
    }
}