using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StockManager.Models
{
    public class PurchaseOrderLine
    {
        public int Id { get; set; }

        public int PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; } = null!;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int QuantityOrdered { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantityReceived { get; set; }

        // Einkaufspreis zum Zeitpunkt der Bestellung (unabhängig vom Produktpreis)
        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true)]
        public decimal PurchasePrice { get; set; }
    }
}