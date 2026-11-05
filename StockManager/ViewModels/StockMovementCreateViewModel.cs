using System.ComponentModel.DataAnnotations;
using StockManager.Models;

namespace StockManager.ViewModels
{
    public class StockMovementCreateViewModel
    {
        public int ProductId { get; set; }

        // Nur zur Anzeige im Formular
        public string? ProductReference { get; set; }
        public string? ProductName { get; set; }
        public int CurrentQuantity { get; set; }
        public int MinimumStock { get; set; }

        [Display(Name = "Art der Bewegung")]
        public StockMovementType MovementType { get; set; } = StockMovementType.Entry;

        [Required(ErrorMessage = "Bitte geben Sie eine Menge ein.")]
        [Range(0, int.MaxValue, ErrorMessage = "Die Menge darf nicht negativ sein.")]
        [Display(Name = "Menge")]
        public int? Quantity { get; set; }

        [StringLength(250, ErrorMessage = "Der Kommentar darf maximal 250 Zeichen lang sein.")]
        [Display(Name = "Kommentar")]
        public string? Comment { get; set; }
    }
}