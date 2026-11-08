using System.ComponentModel.DataAnnotations;

namespace StockManager.ViewModels
{
    public class SupplierEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bitte geben Sie den Namen des Lieferanten ein.")]
        [StringLength(120, ErrorMessage = "Der Name darf maximal 120 Zeichen lang sein.")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Der Ansprechpartner darf maximal 100 Zeichen lang sein.")]
        [Display(Name = "Ansprechpartner")]
        public string? ContactPerson { get; set; }

        [EmailAddress(ErrorMessage = "Bitte geben Sie eine gültige E-Mail-Adresse ein.")]
        [StringLength(150, ErrorMessage = "Die E-Mail-Adresse darf maximal 150 Zeichen lang sein.")]
        [Display(Name = "E-Mail")]
        public string? Email { get; set; }

        [StringLength(50, ErrorMessage = "Die Telefonnummer darf maximal 50 Zeichen lang sein.")]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [StringLength(200, ErrorMessage = "Die Straße darf maximal 200 Zeichen lang sein.")]
        [Display(Name = "Straße und Hausnummer")]
        public string? Street { get; set; }

        [StringLength(20, ErrorMessage = "Die Postleitzahl darf maximal 20 Zeichen lang sein.")]
        [Display(Name = "PLZ")]
        public string? PostalCode { get; set; }

        [StringLength(100, ErrorMessage = "Der Ort darf maximal 100 Zeichen lang sein.")]
        [Display(Name = "Ort")]
        public string? City { get; set; }

        [StringLength(500, ErrorMessage = "Die Notizen dürfen maximal 500 Zeichen lang sein.")]
        [Display(Name = "Notizen")]
        public string? Notes { get; set; }
    }
}