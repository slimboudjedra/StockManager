using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace StockManager.Models
{
    public enum StockMovementType
    {
        [Display(Name = "Wareneingang")]
        Entry = 1,

        [Display(Name = "Warenausgang")]
        Exit = 2,

        [Display(Name = "Inventurkorrektur")]
        Adjustment = 3
    }

    public static class StockMovementTypeExtensions
    {
        public static string GetDisplayName(this StockMovementType type)
        {
            return type.GetType()
                .GetField(type.ToString())?
                .GetCustomAttribute<DisplayAttribute>()?
                .GetName() ?? type.ToString();
        }
    }
}