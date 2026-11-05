using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.ViewModels;


namespace StockManager.Services
{
    public class ProductService
    {
        private readonly ApplicationDbContext _context;
        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        //---------------------------------------------------------------------------------------------------------//

        public IQueryable<Product> GetActiveProducts()
        {
            return _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsArchived);
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived); 
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<ProductIndexViewModel> GetProductsAsync(string? searchString, int page, bool lowStockOnly,int pageSize = 10)
        {
            if(pageSize <= 0)
            {
                pageSize = 10;
            }

            var query = GetActiveProducts();

            // Apply search filter if provided

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchString) ||
                    p.Reference.Contains(searchString));
            }

            if (lowStockOnly)
            {
                query = query.Where(p => p.Quantity <= p.MinimumStock);
            }

            // Implementation for pagination and other logic would go here

            var totalProducts = await query.CountAsync();

            var totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

            page = totalPages > 0 ? Math.Clamp(page, 1, totalPages) : 1;

            var products = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Create a new instance of ProductIndexViewModel and populate it with the data

            var lowStockCount = await GetLowStockCountAsync();

            return new ProductIndexViewModel
            {
                Products = products,
                CurrentPage = page,
                TotalPages = totalPages,
                SearchString = searchString,
                LowStockOnly = lowStockOnly,
                LowStockCount = lowStockCount
            };
        }

        //---------------------------------------------------------------------------------------------------------//
        public async Task<List<Category>> GetCategoriesAsync()
        {
            return await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<ProductEditViewModel?> GetProductForEditAsync(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (product == null)
            {
                return null;
            }
            return new ProductEditViewModel
            {
                Id = product.Id,
                Reference = product.Reference,
                Name = product.Name,
                Description = product.Description,
                CategoryId = product.CategoryId,
                MinimumStock = product.MinimumStock,
                UnitPrice = product.UnitPrice
            };
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<int> GetLowStockCountAsync()
        {
            return await _context.Products.CountAsync(p => !p.IsArchived && p.Quantity <= p.MinimumStock);
        }

      
        //---------------------------------------------------------------------------------------------------------//

        public Task<bool> ReferenceExistsAsync(string reference, int? excludeProductId = null)
        {
            reference = reference.Trim() ;
            return _context.Products.AnyAsync(p =>
            p.Reference == reference &&
            !p.IsArchived &&
            (excludeProductId == null || p.Id != excludeProductId.Value));
        }

        //---------------------------------------------------------------------------------------------------------//
        public async Task<bool> CategoryExistsAsync(int categoryId)
        {
            return await _context.Categories
                .AnyAsync(c => c.Id == categoryId);
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<Product> CreateProductAsync(ProductEditViewModel input)
        {

            if(!await CategoryExistsAsync(input.CategoryId))
            {
                throw new ArgumentException("Die angegebene Kategorie existiert nicht.", nameof(input.CategoryId));
            }

            var product = new Product
            {

                Reference = input.Reference.Trim(),
                Name = input.Name,
                Description = input.Description,
                CategoryId = input.CategoryId,
                MinimumStock = input.MinimumStock,
                UnitPrice = input.UnitPrice,
                Quantity = 0,
                CreatedAt = DateTime.UtcNow,
                IsArchived = false
            };

            _context.Products.Add(product);

            await _context.SaveChangesAsync(); 

            return product;
        }

        //---------------------------------------------------------------------------------------------------------//
        public async Task<bool> UpdateProductAsync(int id, ProductEditViewModel input)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);

            if (product == null)
            {
                return false;
            }
            if(!await CategoryExistsAsync(input.CategoryId))
            {
                throw new ArgumentException("Die angegebene Kategorie existiert nicht.", nameof(input.CategoryId));
            }

            product.Reference = input.Reference.Trim();
            product.Name = input.Name;
            product.Description = input.Description;
            product.CategoryId = input.CategoryId;
            product.MinimumStock = input.MinimumStock;
            product.UnitPrice = input.UnitPrice;

            await _context.SaveChangesAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//
        public async Task<bool> ArchiveProductAsync(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);
            if (product == null)
            {
                return false;
            }
            product.IsArchived = true;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
