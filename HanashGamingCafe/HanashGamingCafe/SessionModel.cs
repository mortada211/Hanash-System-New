using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace HanashGamingCafe
{
    [Table("sessions")]
    public class SessionModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } // ليتوافق مع UUID

        [Column("device_name")]
        public string DeviceName { get; set; }

        [Column("device_type")]
        public string DeviceType { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime? EndTime { get; set; }

        [Column("hourly_rate")]
        public decimal HourlyRate { get; set; }

        [Column("rounds_count")]
        public int RoundsCount { get; set; } = 1;

        [Column("status")]
        public string Status { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("payment_method")]
        public string PaymentMethod { get; set; } = "كاش";

        // 🟢 خاصية ItemId مربوطة بعمود item_id في قاعدة البيانات
        [Column("item_id")]
        public string ItemId { get; set; }

        // 🟢 خاصية IsActive لمعرفة حالة الجلسة
        [Column("is_active")]
        public bool IsActive { get; set; }

        // 🟢 خاصية CashierName عادية للعرض فقط (بدون Column attribute حتى لا تتعارض مع قاعدة البيانات)
        public string CashierName { get; set; }

    }
}