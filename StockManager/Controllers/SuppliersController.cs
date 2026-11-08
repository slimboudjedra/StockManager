using Microsoft.AspNetCore.Mvc;
using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Controllers
{
    public class SuppliersController : Controller
    {
        private readonly SupplierService _supplierService;

        public SuppliersController(SupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        // GET: Suppliers?searchString=...&archived=true
        public async Task<IActionResult> Index(string? searchString, bool archived = false)
        {
            var suppliers = await _supplierService.GetSuppliersAsync(searchString, archived);

            ViewData["SearchString"] = searchString;
            ViewData["Archived"] = archived;

            return View(suppliers);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Suppliers/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var supplier = await _supplierService.GetByIdAsync(id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Suppliers/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new SupplierEditViewModel());
        }

        // POST: Suppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupplierEditViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            try
            {
                var supplier = await _supplierService.CreateAsync(input);

                TempData["SuccessMessage"] = $"Lieferant „{supplier.Name}“ wurde angelegt.";

                return RedirectToAction(nameof(Details), new { id = supplier.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(input.Name), ex.Message);

                return View(input);
            }
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Suppliers/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var viewModel = await _supplierService.GetForEditAsync(id);

            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        // POST: Suppliers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SupplierEditViewModel input)
        {
            if (id != input.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            try
            {
                var updated = await _supplierService.UpdateAsync(id, input);

                if (!updated)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Lieferant wurde gespeichert.";

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(input.Name), ex.Message);

                return View(input);
            }
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Suppliers/Archive/5
        [HttpGet]
        public async Task<IActionResult> Archive(int id)
        {
            var supplier = await _supplierService.GetByIdAsync(id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // POST: Suppliers/Archive/5
        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            try
            {
                var archived = await _supplierService.ArchiveAsync(id);

                if (!archived)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Lieferant wurde archiviert.";

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // Der Lieferant ist noch Standardlieferant aktiver Produkte:
                // Wir bleiben auf der Bestätigungsseite und zeigen den Grund.
                var supplier = await _supplierService.GetByIdAsync(id);

                if (supplier == null)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, ex.Message);

                return View(supplier);
            }
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: Suppliers/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                var restored = await _supplierService.RestoreAsync(id);

                if (!restored)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Lieferant wurde wiederhergestellt.";

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return RedirectToAction(nameof(Index), new { archived = true });
            }
        }
    }
}