using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Controllers
{
    public class ProductsController : Controller
    {
        private const string DuplicateReferenceMessage = "Diese Referenz wird bereits verwendet.";

        private readonly ProductService _productService;

        public ProductsController(ProductService productService)
        {
            _productService = productService;
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Products
        public async Task<IActionResult> Index(string? searchString, int page = 1, bool lowStockOnly = false)
        {
            var viewModel = await _productService.GetProductsAsync(searchString, page, lowStockOnly);
            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _productService.GetProductByIdAsync(id.Value);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Products/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new ProductEditViewModel();
            await PopulateListsAsync(viewModel);
            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductEditViewModel input)
        {
            if (ModelState.IsValid)
            {
                if (await _productService.ReferenceExistsAsync(input.Reference))
                {
                    ModelState.AddModelError(nameof(input.Reference), DuplicateReferenceMessage);
                }
                else
                {
                    try
                    {
                        await _productService.CreateProductAsync(input);
                        return RedirectToAction(nameof(Index));
                    }
                    catch (DbUpdateException ex) when (IsDuplicateKey(ex))
                    {
                        ModelState.AddModelError(nameof(input.Reference), DuplicateReferenceMessage);
                    }
                    catch (ArgumentException ex) when (IsFormFieldError(ex))
                    {
                        ModelState.AddModelError(ex.ParamName!, ex.Message);
                    }
                }
            }

            await PopulateListsAsync(input);
            return View(input);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var viewModel = await _productService.GetProductForEditAsync(id.Value);
            if (viewModel == null)
            {
                return NotFound();
            }

            await PopulateListsAsync(viewModel);
            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductEditViewModel input)
        {
            if (id != input.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (await _productService.ReferenceExistsAsync(input.Reference, id))
                {
                    ModelState.AddModelError(nameof(input.Reference), DuplicateReferenceMessage);
                }
                else
                {
                    try
                    {
                        var updated = await _productService.UpdateProductAsync(id, input);
                        if (!updated)
                        {
                            return NotFound();
                        }

                        return RedirectToAction(nameof(Index));
                    }
                    catch (DbUpdateException ex) when (IsDuplicateKey(ex))
                    {
                        ModelState.AddModelError(nameof(input.Reference), DuplicateReferenceMessage);
                    }
                    catch (ArgumentException ex) when (IsFormFieldError(ex))
                    {
                        ModelState.AddModelError(ex.ParamName!, ex.Message);
                    }
                }
            }

            await PopulateListsAsync(input);
            return View(input);
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _productService.GetProductByIdAsync(id.Value);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: Products/Delete/5  (archiviert das Produkt, löscht es nicht)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var archived = await _productService.ArchiveProductAsync(id);
                if (!archived)
                {
                    return NotFound();
                }

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // Das Produkt steckt noch in einer offenen Bestellung: Wir bleiben auf der
                // Bestätigungsseite und zeigen den Grund.
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, ex.Message);
                return View(product);
            }
        }

        //---------------------------------------------------------------------------------------------------------//

        // Füllt die beiden Auswahllisten (Kategorie und Standardlieferant) des Formulars.
        private async Task PopulateListsAsync(ProductEditViewModel viewModel)
        {
            var categories = await _productService.GetCategoriesAsync();
            var suppliers = await _productService.GetActiveSuppliersAsync();

            viewModel.Categories = categories
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = c.Id == viewModel.CategoryId
                })
                .ToList();

            viewModel.Suppliers = suppliers
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name,
                    Selected = s.Id == viewModel.PreferredSupplierId
                })
                .ToList();
        }

        //---------------------------------------------------------------------------------------------------------//

        private static bool IsDuplicateKey(DbUpdateException ex) =>
            ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);

        // Der Parametername der ArgumentException entspricht dem Feld des Formulars.
        private static bool IsFormFieldError(ArgumentException ex) =>
            ex.ParamName is nameof(ProductEditViewModel.CategoryId)
                or nameof(ProductEditViewModel.PreferredSupplierId);
    }
}