
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockManager.Models;
using StockManager.Data;
using StockManager.ViewModels;

public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: PRODUCTS

    public async Task<IActionResult> Index(string? searchString, int page = 1)
    {
        const int pageSize = 10;

        var products = _context.Products
            .Where(p => !p.IsArchived);

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            products = products.Where(p =>
                p.Name.Contains(searchString) ||
                p.Reference.Contains(searchString));
        }

        ViewData["CurrentFilter"] = searchString;

        var totalProducts = await products.CountAsync();

        var totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

        if (totalPages > 0)
        {
            page = Math.Clamp(page, 1, totalPages);
        }
        else
        {
            page = 1;
        }

        var productList = await products
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;

        return View(productList);
    }


    // GET: PRODUCTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null)
        {
            return NotFound();
        }

        return View(product);
    }

    // GET: PRODUCTS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: PRODUCTS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Reference,Name,Description,Category,Quantity,MinimumStock,UnitPrice,CreatedAt,IsArchived")] Product product)
    {
        if (ModelState.IsValid)
        {
            _context.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    // GET: PRODUCTS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        return View(product);
    }

    // POST: PRODUCTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product input)
    {
        if(id != input.Id)
        {
            return NotFound();
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);

        if (product == null)
        {
            return NotFound();
        }
        if(!ModelState.IsValid)
        {
            return View(input);
        }

        product.Reference = input.Reference;
        product.Name = input.Name;
        product.Description = input.Description;
        product.Category = input.Category;
        product.MinimumStock = input.MinimumStock;
        product.UnitPrice = input.UnitPrice;
        
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: PRODUCTS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null)
        {
            return NotFound();
        }

        return View(product);
    }

    // POST: PRODUCTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            _context.Products.Remove(product);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool ProductExists(int? id)
    {
        return _context.Products.Any(e => e.Id == id);
    }
}
