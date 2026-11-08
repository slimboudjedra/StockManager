using System.ComponentModel.DataAnnotations;

namespace StockManager.Models
{
    public class PurchaseOrder
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string OrderNumber { get; set; } = string.Empty;

        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Zeitpunkt, an dem die Bestellung aufgegeben wurde (Status "Bestellt")
        public DateTime? OrderedAt { get; set; }

        public DateOnly? ExpectedDeliveryDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // Zeitpunkt des letzten Wareneingangs (leer, solange nichts geliefert wurde)
        public DateTime? LastReceiptAt { get; set; }

        // Versionsnummer zum Schutz vor gleichzeitigen Änderungen (von SQL Server verwaltet)
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    }
}