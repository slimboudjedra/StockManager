using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.ViewModels;  

namespace StockManager.Services
{
    public class CategoryService
    {
        private readonly ApplicationDbContext _context;

        private const string CategoryInUseMessage =
            "Diese Kategorie kann nicht gelöscht werden, weil ihr noch Produkte zugeordnet sind.";

        //---------------------------------------------------------------------------------------------------------//

        public CategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<Category> CreateAsync(Category category)
        {
            var name = category.Name?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Der Kategoriename darf nicht leer sein.",
                    nameof(category.Name));
            }

            if (name.Length > 80)
            {
                throw new ArgumentException(
                    "Der Kategoriename darf maximal 80 Zeichen lang sein.",
                    nameof(category.Name));
            }

            var normalizedName = name.ToUpper();

            var alreadyExists = await _context.Categories
                .AnyAsync(c => c.Name.ToUpper() == normalizedName);

            if (alreadyExists)
            {
                throw new InvalidOperationException(
                    "Diese Kategorie existiert bereits.");
            }

            var newCategory = new Category
            {
                Name = name,
                Description = category.Description?.Trim()
            };

            _context.Categories.Add(newCategory);
            await _context.SaveChangesAsync();

            return newCategory;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<bool> UpdateAsync(int id, Category input)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return false;
            }

            var name = input.Name?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Der Kategoriename darf nicht leer sein.",
                    nameof(input.Name));
            }

            if (name.Length > 80)
            {
                throw new ArgumentException(
                    "Der Kategoriename darf maximal 80 Zeichen lang sein.",
                    nameof(input.Name));
            }

            var normalizedName = name.ToUpper();

            var alreadyExists = await _context.Categories
                .AnyAsync(c =>
                    c.Id != id &&
                    c.Name.ToUpper() == normalizedName);

            if (alreadyExists)
            {
                throw new InvalidOperationException(
                    "Diese Kategorie existiert bereits.");
            }

            category.Name = name;
            category.Description = input.Description?.Trim();

            await _context.SaveChangesAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return false;
            }

            var hasProducts = await _context.Products
                .AnyAsync(p => p.CategoryId == id);

            if (hasProducts)
            {
                throw new InvalidOperationException(CategoryInUseMessage);
            }

            _context.Categories.Remove(category);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Sicherheitsnetz: Die Datenbank hat die Anfrage abgelehnt (Fremdschlüssel „Restrict“),
                // beispielsweise, wenn in der Zwischenzeit ein Produkt hinzugefügt wurde.
                throw new InvalidOperationException(CategoryInUseMessage);
            }

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//
        public async Task<CategoryDetailsViewModel?> GetDetailsAsync(int id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return null;
            }

            var activeProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.CategoryId == id && !p.IsArchived)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var archivedCount = await _context.Products
                .CountAsync(p => p.CategoryId == id && p.IsArchived);

            return new CategoryDetailsViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ActiveProductCount = activeProducts.Count,
                ArchivedProductCount = archivedCount,
                Products = activeProducts
            };
        }
    }


}
