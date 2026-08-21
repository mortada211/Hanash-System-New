using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace HanashGamingCafe
{
    [Table("floor_items")]
    public class FloorItem : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("type")]
        public string Type { get; set; }

        [Column("status")]
        public string Status { get; set; }

        [Column("hourly_rate")]
        public decimal HourlyRate { get; set; }

        [Column("game_rate")]
        public decimal GameRate { get; set; }

        [Column("current_session_start")]
        public DateTime? CurrentSessionStart { get; set; }
    }
}