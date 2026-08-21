using Postgrest.Attributes;
using Postgrest.Models;

namespace HanashGamingCafe
{
    [Table("devices")]
    public class DeviceModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } // 👈 تم التغيير من int إلى string ليتوافق مع UUID

        [Column("name")]
        public string Name { get; set; }

        [Column("type")]
        public string Type { get; set; }

        [Column("hourly_rate")]
        public decimal HourlyRate { get; set; }

        [Column("status")]
        public string Status { get; set; }
        [Column("round_rate")]
        public decimal RoundRate { get; set; }
    }
}