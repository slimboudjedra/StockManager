using System.ComponentModel.DataAnnotations;

namespace StockManager.Models
{
    public class StockMovement
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public StockMovementType MovementType { get; set; }

        // Mit Vorzeichen: +10 bei Eingang, -3 bei Ausgang, +/- bei Korrektur
        public int QuantityChange { get; set; }

        // Bestand des Produkts direkt nach dieser Bewegung
        public int QuantityAfter { get; set; }

        [StringLength(250)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}