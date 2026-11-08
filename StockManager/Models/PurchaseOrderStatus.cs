using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace StockManager.Models
{
    public enum PurchaseOrderStatus
    {
        [Display(Name = "Entwurf")]
        Draft = 1,

        [Display(Name = "Bestellt")]
        Ordered = 2,

        [Display(Name = "Teilweise geliefert")]
        PartiallyReceived = 3,

        [Display(Name = "Geliefert")]
        Received = 4,

        [Display(Name = "Storniert")]
        Cancelled = 5,

        [Display(Name = "Abgeschlossen (unvollständig)")]
        Closed = 6
    }

    public static class PurchaseOrderStatusExtensions
    {
        public static string GetDisplayName(this PurchaseOrderStatus status)
        {
            return status.GetType()
                .GetField(status.ToString())?
                .GetCustomAttribute<DisplayAttribute>()?
                .GetName() ?? status.ToString();
        }

        // Bootstrap-Klassen für das farbige Etikett des Status
        public static string GetBadgeClass(this PurchaseOrderStatus status) => status switch
        {
            PurchaseOrderStatus.Draft => "bg-secondary",
            PurchaseOrderStatus.Ordered => "bg-primary",
            PurchaseOrderStatus.PartiallyReceived => "bg-warning text-dark",
            PurchaseOrderStatus.Received => "bg-success",
            PurchaseOrderStatus.Cancelled => "bg-dark",
            PurchaseOrderStatus.Closed => "bg-info text-dark",
            _ => "bg-secondary"
        };
    }
}