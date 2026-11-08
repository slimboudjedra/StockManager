using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockManager.Models;

namespace StockManager.ViewModels
{
    public class PurchaseOrderEditViewModel
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Bitte wählen Sie einen Lieferanten aus.")]
        [Display(Name = "Lieferant")]
        public int SupplierId { get; set; }

        [Display(Name = "Gewünschtes Lieferdatum")]
        public DateOnly? ExpectedDeliveryDate { get; set; }

        [StringLength(500, ErrorMessage = "Die Notizen dürfen maximal 500 Zeichen lang sein.")]
        [Display(Name = "Notizen")]
        public string? Notes { get; set; }

        public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
    }

    public class PurchaseOrderListItem
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public PurchaseOrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateOnly? ExpectedDeliveryDate { get; set; }
        public int LineCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PurchaseOrderLineItem
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductReference { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }
        public decimal PurchasePrice { get; set; }

        public decimal LineTotal => QuantityOrdered * PurchasePrice;
    }

    public class PurchaseOrderDetailsViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public PurchaseOrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? OrderedAt { get; set; }
        public DateOnly? ExpectedDeliveryDate { get; set; }
        public string? Notes { get; set; }
        public DateTime? LastReceiptAt { get; set; }

        public bool CanReceive =>
            Status is PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived;

        public bool CanClose => Status == PurchaseOrderStatus.PartiallyReceived;

        public List<PurchaseOrderLineItem> Lines { get; set; } = new();

        public decimal TotalAmount => Lines.Sum(l => l.LineTotal);

        public IEnumerable<SelectListItem> ProductOptions { get; set; } = new List<SelectListItem>();
        public bool CanEdit => Status == PurchaseOrderStatus.Draft;
        public bool CanPlace => Status == PurchaseOrderStatus.Draft && Lines.Count > 0;
        public bool CanCancel => Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered;
        public bool CanDelete => Status == PurchaseOrderStatus.Draft;
    }
    public class ProductChoice
    {
        public int Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
    }
    public class ReceiveLineInput
    {
        public int LineId { get; set; }

        // Nur zur Anzeige (das Formular sendet sie nicht zurück)
        public string? ProductReference { get; set; }
        public string? ProductName { get; set; }
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }

        public int Remaining => QuantityOrdered - QuantityReceived;

        // Ein leeres Feld zählt als 0
        [Range(0, int.MaxValue, ErrorMessage = "Die Menge darf nicht negativ sein.")]
        public int? QuantityNow { get; set; }
    }

    public class ReceiveOrderViewModel
    {
        public int OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? SupplierName { get; set; }

        [StringLength(200, ErrorMessage = "Der Kommentar darf maximal 200 Zeichen lang sein.")]
        [Display(Name = "Kommentar (z. B. Lieferschein-Nr.)")]
        public string? Comment { get; set; }

        public List<ReceiveLineInput> Lines { get; set; } = new();
    }

    public record ReceiptResult(PurchaseOrderStatus Status, int Units, int LineCount);

    public record DraftLineInput(int ProductId, int Quantity, decimal PurchasePrice);

    public class ReorderItem
    {
        public int ProductId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int MinimumStock { get; set; }

        // Noch erwartete Menge aus Entwürfen und offenen Bestellungen
        public int OpenQuantity { get; set; }

        public int SuggestedQuantity { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal LineTotal => SuggestedQuantity * UnitPrice;
    }

    public class ReorderSupplierGroup
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public List<ReorderItem> Items { get; set; } = new();

        public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    }

    public class ReorderSuggestionsViewModel
    {
        public List<ReorderSupplierGroup> Groups { get; set; } = new();
        public List<ReorderItem> WithoutSupplier { get; set; } = new();

        public bool IsEmpty => Groups.Count == 0 && WithoutSupplier.Count == 0;
    }
}