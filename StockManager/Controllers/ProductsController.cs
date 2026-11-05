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

        // GET: Products
        public async Task<IActionResult> Index(string? searchString, int page = 1, bool lowStockOnly = false)
        {
            var viewModel = await _productService.GetProductsAsync(searchString, page, lowStockOnly);
            return View(viewModel);
        }

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

        // GET: Products/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new ProductEditViewModel();
            await PopulateCategoriesAsync(viewModel);
            return View(viewModel);
        }

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
                    catch (ArgumentException ex) when (ex.ParamName == nameof(input.CategoryId))
                    {
                        ModelState.AddModelError(nameof(input.CategoryId), ex.Message);
                    }
                }
            }

            await PopulateCategoriesAsync(input);
            return View(input);
        }

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

            await PopulateCategoriesAsync(viewModel);
            return View(viewModel);
        }

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
                    catch (ArgumentException ex) when (ex.ParamName == nameof(input.CategoryId))
                    {
                        ModelState.AddModelError(nameof(input.CategoryId), ex.Message);
                    }
                }
            }

            await PopulateCategoriesAsync(input);
            return View(input);
        }

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

        // POST: Products/Delete/5  (archiviert das Produkt, löscht es nicht)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var archived = await _productService.ArchiveProductAsync(id);
            if (!archived)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }

        //---------------------------------------------------------------------------------------------------------//

        private async Task PopulateCategoriesAsync(ProductEditViewModel viewModel)
        {
            var categories = await _productService.GetCategoriesAsync();

            viewModel.Categories = categories
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = c.Id == viewModel.CategoryId
                })
                .ToList();
        }

        private static bool IsDuplicateKey(DbUpdateException ex) =>
            ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);
    }
}