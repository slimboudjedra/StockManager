using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StockManager.ModelBinders
{
    public class StrictDecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            return context.Metadata.ModelType == typeof(decimal)
                ? new StrictDecimalModelBinder()
                : null;
        }
    }
}