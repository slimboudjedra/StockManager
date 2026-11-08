using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockManager.Models;
using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Controllers
{
    public class PurchaseOrdersController : Controller
    {
        private readonly PurchaseOrderService _orderService;
        private readonly SupplierService _supplierService;

        public PurchaseOrdersController(
            PurchaseOrderService orderService,
            SupplierService supplierService)
        {
            _orderService = orderService;
            _supplierService = supplierService;
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: PurchaseOrders?status=Ordered
        public async Task<IActionResult> Index(PurchaseOrderStatus? status)
        {
            var orders = await _orderService.GetOrdersAsync(status);

            ViewData["Status"] = status;

            return View(orders);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: PurchaseOrders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var viewModel = await _orderService.GetDetailsAsync(id);

            if (viewModel == null)
            {
                return NotFound();
            }

            if (viewModel.CanEdit)
            {
                var products = await _orderService.GetAvailableProductsAsync(id);

                viewModel.ProductOptions = products
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id.ToString(),
                        Text = $"{p.Reference} – {p.Name} ({p.UnitPrice:N2} €)"
                    })
                    .ToList();
            }

            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: PurchaseOrders/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var viewModel = new PurchaseOrderEditViewModel();
            await PopulateSuppliersAsync(viewModel);
            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderEditViewModel input)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var order = await _orderService.CreateDraftAsync(input);

                    TempData["SuccessMessage"] =
                        $"Bestellung {order.OrderNumber} wurde als Entwurf angelegt. Fügen Sie jetzt Positionen hinzu.";

                    return RedirectToAction(nameof(Details), new { id = order.Id });
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(input.SupplierId), ex.Message);
                }
            }

            await PopulateSuppliersAsync(input);
            return View(input);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: PurchaseOrders/Edit/5  (nur Entwürfe)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var viewModel = await _orderService.GetForEditAsync(id);

            if (viewModel == null)
            {
                return NotFound();
            }

            await PopulateSuppliersAsync(viewModel);
            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PurchaseOrderEditViewModel input)
        {
            if (id != input.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var updated = await _orderService.UpdateHeaderAsync(id, input);

                    if (!updated)
                    {
                        return NotFound();
                    }

                    TempData["SuccessMessage"] = "Kopfdaten wurden gespeichert.";

                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await PopulateSuppliersAsync(input);
            return View(input);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/AddLine
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AddLine(int orderId, int productId, int quantity, decimal? purchasePrice)
        {
            return RunOrderActionAsync(
                orderId,
                async () => await _orderService.AddLineAsync(orderId, productId, quantity, purchasePrice) != null,
                "Position wurde hinzugefügt.");
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/UpdateLine
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> UpdateLine(int id, int orderId, int quantity, decimal purchasePrice)
        {
            return RunOrderActionAsync(
                orderId,
                () => _orderService.UpdateLineAsync(id, quantity, purchasePrice),
                "Position wurde gespeichert.");
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/RemoveLine
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> RemoveLine(int id, int orderId)
        {
            return RunOrderActionAsync(
                orderId,
                () => _orderService.RemoveLineAsync(id),
                "Position wurde entfernt.");
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/Place/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Place(int id)
        {
            return RunOrderActionAsync(
                id,
                () => _orderService.PlaceOrderAsync(id),
                "Die Bestellung wurde aufgegeben.");
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Cancel(int id)
        {
            return RunOrderActionAsync(
                id,
                () => _orderService.CancelAsync(id),
                "Die Bestellung wurde storniert.");
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/Close/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Close(int id)
        {
            return RunOrderActionAsync(
                id,
                () => _orderService.CloseAsync(id),
                "Die Bestellung wurde abgeschlossen. Die offene Restmenge wird nicht mehr erwartet.");
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/Delete/5  (nur Entwürfe)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _orderService.DeleteDraftAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Der Entwurf wurde gelöscht.";

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return RedirectToAction(nameof(Details), new { id });
            }
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: PurchaseOrders/Receive/5
        [HttpGet]
        public async Task<IActionResult> Receive(int id)
        {
            var viewModel = await _orderService.GetReceiptFormAsync(id);

            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: PurchaseOrders/Receive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id, ReceiveOrderViewModel input)
        {
            if (input.OrderId != id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return await ShowReceiptFormAgainAsync(id, input, FirstModelError());
            }

            try
            {
                var result = await _orderService.ReceiveAsync(id, input);

                if (result == null)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] =
                    $"Wareneingang gebucht: {result.Units} Stück in {result.LineCount} Position(en). " +
                    $"Status: {result.Status.GetDisplayName()}.";

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                return await ShowReceiptFormAgainAsync(id, input, ex.Message);
            }
        }

        //---------------------------------------------------------------------------------------------------------//

        // Zeigt das Formular nach einem Fehler erneut. Die Anzeigefelder (Produktname, Restmenge)
        // werden nicht mitgesendet, deshalb wird das Formular neu geladen und mit den eingegebenen
        // Mengen und dem Kommentar überlagert.
        private async Task<IActionResult> ShowReceiptFormAgainAsync(
            int id, ReceiveOrderViewModel posted, string message)
        {
            var fresh = await _orderService.GetReceiptFormAsync(id);

            if (fresh == null)
            {
                // Die Bestellung erlaubt keinen Wareneingang mehr (z. B. gerade vollständig geliefert).
                TempData["ErrorMessage"] = message;
                return RedirectToAction(nameof(Details), new { id });
            }

            fresh.Comment = posted.Comment;

            foreach (var line in fresh.Lines)
            {
                var postedLine = posted.Lines.FirstOrDefault(l => l.LineId == line.LineId);

                if (postedLine != null)
                {
                    line.QuantityNow = postedLine.QuantityNow;
                }
            }

            ModelState.Clear();
            ModelState.AddModelError(string.Empty, message);

            return View(fresh);
        }

        //---------------------------------------------------------------------------------------------------------//

        // Führt eine Aktion aus und kehrt immer zur Detailseite zurück. Ein Fehler
        // (ungültige Eingabe oder verletzte Geschäftsregel) wird dort angezeigt.
        private async Task<IActionResult> RunOrderActionAsync(
            int orderId,
            Func<Task<bool>> action,
            string successMessage)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = FirstModelError();
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            try
            {
                var found = await action();

                if (!found)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = successMessage;
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        //---------------------------------------------------------------------------------------------------------//

        private string FirstModelError()
        {
            return ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                ?? "Die Eingabe ist ungültig.";
        }

        //---------------------------------------------------------------------------------------------------------//

        private async Task PopulateSuppliersAsync(PurchaseOrderEditViewModel viewModel)
        {
            var suppliers = await _supplierService.GetSuppliersAsync(null);

            viewModel.Suppliers = suppliers
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name,
                    Selected = s.Id == viewModel.SupplierId
                })
                .ToList();
        }
    }
}