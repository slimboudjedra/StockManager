using Microsoft.AspNetCore.Mvc;
using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Controllers
{
    public class StockMovementsController : Controller
    {
        private readonly StockService _stockService;

        //---------------------------------------------------------------------------------------------------------//

        public StockMovementsController(StockService stockService)
        {
            _stockService = stockService;
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: StockMovements/Create?productId=5
        [HttpGet]
        public async Task<IActionResult> Create(int productId)
        {
            var viewModel = await _stockService.GetMovementFormAsync(productId);

            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: StockMovements?productId=5&page=1
        [HttpGet]
        public async Task<IActionResult> Index(int productId, int page = 1)
        {
            var viewModel = await _stockService.GetHistoryAsync(productId, page);

            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }
        //---------------------------------------------------------------------------------------------------------//

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockMovementCreateViewModel input)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var movement = await _stockService.RecordMovementAsync(
                        input.ProductId,
                        input.MovementType,
                        input.Quantity.GetValueOrDefault(),
                        input.Comment);

                    if (movement == null)
                    {
                        return NotFound();
                    }

                    TempData["SuccessMessage"] =
                        $"Bewegung gebucht. Neuer Bestand: {movement.QuantityAfter}.";

                    return RedirectToAction(nameof(Index), new { productId = input.ProductId });
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            // Fehlerfall: Die Anzeigefelder werden nicht mitgesendet, also neu laden.
            var productInfo = await _stockService.GetMovementFormAsync(input.ProductId);

            if (productInfo == null)
            {
                return NotFound();
            }

            input.ProductReference = productInfo.ProductReference;
            input.ProductName = productInfo.ProductName;
            input.CurrentQuantity = productInfo.CurrentQuantity;
            input.MinimumStock = productInfo.MinimumStock;

            return View(input);
        }
    }
}