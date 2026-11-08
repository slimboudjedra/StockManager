
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace StockManager.ViewModels
{
    public class ProductEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Die Angabe der Referenznummer ist obligatorisch.")]
        [StringLength(50, ErrorMessage = "Die Referenz darf maximal 50 Zeichen lang sein.")]
        [Display(Name = "Referenz")]
        public string Reference { get; set; } = string.Empty;

        [Required(ErrorMessage = "Die Angabe des Produktnamens ist obligatorisch.")]
        [StringLength(150, ErrorMessage = "Der Produktnamen darf maximal 150 Zeichen lang sein.")]
        [Display(Name = "Produktname")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Die Beschreibung darf maximal 100 Zeichen lang sein.")]
        [Display(Name = "Beschreibung")]
        public string? Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Bitte wählen Sie eine Kategorie aus.")]
        [Display(Name = "Kategorie")]
        public int CategoryId { get; set; }

        [Range(0, int.MaxValue,
            ErrorMessage = "Der Mindestbestand muss positiv oder null sein.")]
        [Display(Name = "Mindestbestand")]
        public int MinimumStock { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999.99",
            ParseLimitsInInvariantCulture = true,
            ErrorMessage = "Der Preis muss positiv oder null sein.")]
        [Display(Name = "Einzelpreis")]
        public decimal UnitPrice { get; set; }

        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();

        [Display(Name = "Standardlieferant")]
        public int? PreferredSupplierId { get; set; }

        public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
    }
}
