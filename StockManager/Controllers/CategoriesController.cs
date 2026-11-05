using StockManager.Models;
using StockManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace StockManager.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly CategoryService _categoryService;

        public CategoriesController(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        //---------------------------------------------------------------------------------------------------------//

        // Alle Kategorien anzeigen
        public async Task<IActionResult> Index()
        {
            var categories = await _categoryService.GetAllCategoriesAsync();

            return View(categories);
        }

        //---------------------------------------------------------------------------------------------------------//

        // Das Formular zum Anlegen anzeigen
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        //---------------------------------------------------------------------------------------------------------//

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (!ModelState.IsValid)
            {
                return View(category);
            }

            try
            {
                var createdCategory = await _categoryService.CreateAsync(category);
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(category);
            }
        }

        //---------------------------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var viewModel = await _categoryService.GetDetailsAsync(id);

            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        //---------------------------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var categories = await _categoryService.GetAllCategoriesAsync();

            var category = categories.FirstOrDefault(c => c.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        //---------------------------------------------------------------------------------------------------------//

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category input)
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
                var updated = await _categoryService.UpdateAsync(id, input);

                if (!updated)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Kategorie erfolgreich aktualisiert.";

                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(nameof(input.Name), ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(input.Name), ex.Message);
            }

            return View(input);
        }

        //---------------------------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _categoryService.GetByIdAsync(id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        //---------------------------------------------------------------------------------------------------------//

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var deleted = await _categoryService.DeleteAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Kategorie erfolgreich gelöscht.";

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                var category = await _categoryService.GetByIdAsync(id);

                if (category == null)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, ex.Message);

                return View(category);
            }
        }
    }
}
