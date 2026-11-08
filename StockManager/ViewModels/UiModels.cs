namespace StockManager.ViewModels
{
    public record StockLevelModel(int Quantity, int MinimumStock);

    public record EmptyStateModel(string Icon, string Title, string Text);
}