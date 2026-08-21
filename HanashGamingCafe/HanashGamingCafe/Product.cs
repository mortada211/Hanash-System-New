using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace HanashGamingCafe
{
    [Table("products")]
    public class Product : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("barcode")]
        public string Barcode { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("category")]
        public string Category { get; set; }

        [Column("unit")]
        public string Unit { get; set; }

        [Column("cost_price")]
        public decimal CostPrice { get; set; }

        [Column("selling_price")]
        public decimal SellingPrice { get; set; }

        [Column("stock_quantity")]
        public decimal StockQuantity { get; set; }

        [Column("min_stock_level")]
        public decimal MinStockLevel { get; set; }
    }
}