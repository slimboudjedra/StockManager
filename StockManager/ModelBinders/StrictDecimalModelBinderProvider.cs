using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StockManager.ModelBinders
{
    public class StrictDecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            // Gilt für decimal und für decimal? (optionale Beträge).
            if (context.Metadata.UnderlyingOrModelType != typeof(decimal))
            {
                return null;
            }

            return new StrictDecimalModelBinder(context.Metadata.IsNullableValueType);
        }
    }
}