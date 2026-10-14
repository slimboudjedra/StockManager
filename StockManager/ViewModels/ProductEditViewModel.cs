
using System.ComponentModel.DataAnnotations;

namespace StockManager.ViewModels
{
    public class ProductEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Die Angabe der Referenznummer ist obligatorisch.")]
        [StringLength(50)]
        [Display(Name = "Referenz")]
        public string Reference { get; set; } = string.Empty;

        [Required(ErrorMessage = "Die Angabe des Produktnamens ist obligatorisch.")]
        [StringLength(150)]
        [Display(Name = "Produktname")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Beschreibung")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Die Angabe der Kategorie ist obligatorisch.")]
        [StringLength(80)]
        [Display(Name = "Kategorie")]
        public string Category { get; set; } = string.Empty;

        [Range(0, int.MaxValue,
            ErrorMessage = "Der Mindestbestand muss positiv oder null sein.")]
        [Display(Name = "Mindestbestand")]
        public int MinimumStock { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999.99",
            ParseLimitsInInvariantCulture = true,
            ErrorMessage = "Der Preis muss positiv oder null sein.")]
        [Display(Name = "Einzelpreis")]
        public decimal UnitPrice { get; set; }
    }
}
