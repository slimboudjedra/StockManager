using Microsoft.AspNetCore.Mvc;
using StockManager.Models;
using StockManager.Services;

namespace StockManager.Controllers
{
    public class ReorderController : Controller
    {
        private readonly ReorderService _reorderService;

        public ReorderController(ReorderService reorderService)
        {
            _reorderService = reorderService;
        }

        // GET: Reorder
        public async Task<IActionResult> Index()
        {
            var viewModel = await _reorderService.GetSuggestionsAsync();

            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: Reorder/CreateDrafts
        // Mit supplierId: ein Entwurf für diesen Lieferanten. Ohne: ein Entwurf je Lieferant.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDrafts(int? supplierId)
        {
            try
            {
                var drafts = await _reorderService.CreateDraftsAsync(supplierId);

                if (drafts.Count == 1)
                {
                    TempData["SuccessMessage"] =
                        $"Entwurf {drafts[0].OrderNumber} wurde angelegt. " +
                        "Prüfen Sie Mengen und Preise, bevor Sie bestellen.";

                    return RedirectToAction("Details", "PurchaseOrders", new { id = drafts[0].Id });
                }

                TempData["SuccessMessage"] =
                    $"{drafts.Count} Entwürfe wurden angelegt ({string.Join(", ", drafts.Select(d => d.OrderNumber))}). " +
                    "Prüfen Sie Mengen und Preise, bevor Sie bestellen.";

                return RedirectToAction("Index", "PurchaseOrders", new { status = PurchaseOrderStatus.Draft });
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }
    }
}