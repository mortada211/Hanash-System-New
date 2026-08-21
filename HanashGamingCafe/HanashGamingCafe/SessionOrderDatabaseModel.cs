using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace HanashGamingCafe
{
    [Table("session_orders")]
    public class SessionOrderDatabaseModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("session_id")]
        public string SessionId { get; set; }

        [Column("item_name")]
        public string ItemName { get; set; }

        [Column("quantity")]
        public double Qty { get; set; }

        [Column("unit_price")]
        public double Price { get; set; }

        [Column("total_price")]
        public double TotalPrice { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}