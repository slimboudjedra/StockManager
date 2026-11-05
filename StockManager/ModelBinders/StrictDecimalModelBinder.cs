using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StockManager.ModelBinders
{
    public class StrictDecimalModelBinder : IModelBinder
    {
        private static readonly CultureInfo GermanCulture = new("de-DE");

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

            if (result == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);

            var text = result.FirstValue?.Trim();

            if (decimal.TryParse(
                    text,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    GermanCulture,
                    out var value))
            {
                bindingContext.Result = ModelBindingResult.Success(value);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    "Bitte geben Sie den Preis im Format 12,50 ein.");
            }

            return Task.CompletedTask;
        }
    }
}