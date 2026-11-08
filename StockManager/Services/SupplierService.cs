using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.ViewModels;

namespace StockManager.Services
{
    public class SupplierService
    {
        private const string DuplicateNameMessage =
            "Ein aktiver Lieferant mit diesem Namen existiert bereits.";

        private readonly ApplicationDbContext _context;

        public SupplierService(ApplicationDbContext context)
        {
            _context = context;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<List<Supplier>> GetSuppliersAsync(string? searchString, bool archived = false)
        {
            var query = _context.Suppliers
                .AsNoTracking()
                .Where(s => s.IsArchived == archived);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var term = searchString.Trim();

                query = query.Where(s =>
                    s.Name.Contains(term) ||
                    (s.ContactPerson != null && s.ContactPerson.Contains(term)) ||
                    (s.City != null && s.City.Contains(term)));
            }

            return await query
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<Supplier?> GetByIdAsync(int id)
        {
            return await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsArchived);
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<SupplierEditViewModel?> GetForEditAsync(int id)
        {
            return await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.Id == id && !s.IsArchived)
                .Select(s => new SupplierEditViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    Phone = s.Phone,
                    Street = s.Street,
                    PostalCode = s.PostalCode,
                    City = s.City,
                    Notes = s.Notes
                })
                .FirstOrDefaultAsync();
        }

        //---------------------------------------------------------------------------------------------------------//

        // Wirft InvalidOperationException, wenn der Name leer ist oder schon vergeben wurde.
        public async Task<Supplier> CreateAsync(SupplierEditViewModel input)
        {
            var name = input.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Der Name darf nicht leer sein.");
            }

            if (await NameExistsAsync(name, null))
            {
                throw new InvalidOperationException(DuplicateNameMessage);
            }

            var supplier = new Supplier
            {
                Name = name,
                ContactPerson = Clean(input.ContactPerson),
                Email = Clean(input.Email),
                Phone = Clean(input.Phone),
                Street = Clean(input.Street),
                PostalCode = Clean(input.PostalCode),
                City = Clean(input.City),
                Notes = Clean(input.Notes),
                CreatedAt = DateTime.UtcNow,
                IsArchived = false
            };

            _context.Suppliers.Add(supplier);

            await SaveAsync();

            return supplier;
        }

        //---------------------------------------------------------------------------------------------------------//

        // Gibt false zurück, wenn der Lieferant nicht existiert oder archiviert ist.
        public async Task<bool> UpdateAsync(int id, SupplierEditViewModel input)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsArchived);

            if (supplier == null)
            {
                return false;
            }

            var name = input.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Der Name darf nicht leer sein.");
            }

            if (await NameExistsAsync(name, id))
            {
                throw new InvalidOperationException(DuplicateNameMessage);
            }

            supplier.Name = name;
            supplier.ContactPerson = Clean(input.ContactPerson);
            supplier.Email = Clean(input.Email);
            supplier.Phone = Clean(input.Phone);
            supplier.Street = Clean(input.Street);
            supplier.PostalCode = Clean(input.PostalCode);
            supplier.City = Clean(input.City);
            supplier.Notes = Clean(input.Notes);

            await SaveAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//

        // Gibt false zurück, wenn der Lieferant nicht existiert oder schon archiviert ist.
        // Wirft InvalidOperationException, wenn er noch Standardlieferant aktiver Produkte ist.
        public async Task<bool> ArchiveAsync(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsArchived);

            if (supplier == null)
            {
                return false;
            }

            var productCount = await _context.Products
                .CountAsync(p => p.PreferredSupplierId == id && !p.IsArchived);

            if (productCount > 0)
            {
                throw new InvalidOperationException(
                    productCount == 1
                        ? "Dieser Lieferant ist noch Standardlieferant eines aktiven Produkts. Ändern Sie zuerst den Standardlieferanten dieses Produkts."
                        : $"Dieser Lieferant ist noch Standardlieferant von {productCount} aktiven Produkten. Ändern Sie zuerst deren Standardlieferanten.");
            }

            var openOrderCount = await _context.PurchaseOrders
                                .CountAsync(o => o.SupplierId == id &&
                                (o.Status == PurchaseOrderStatus.Draft ||
                                 o.Status == PurchaseOrderStatus.Ordered ||
                                 o.Status == PurchaseOrderStatus.PartiallyReceived));

            if (openOrderCount > 0)
            {
                throw new InvalidOperationException(
                    openOrderCount == 1
                        ? "Dieser Lieferant hat noch eine offene Bestellung. Schließen oder stornieren Sie sie zuerst."
                        : $"Dieser Lieferant hat noch {openOrderCount} offene Bestellungen. Schließen oder stornieren Sie sie zuerst.");
            }

            supplier.IsArchived = true;

            await _context.SaveChangesAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//

        // Gibt false zurück, wenn der Lieferant nicht existiert oder nicht archiviert ist.
        // Wirft InvalidOperationException, wenn inzwischen ein aktiver Lieferant denselben Namen hat.
        public async Task<bool> RestoreAsync(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id && s.IsArchived);

            if (supplier == null)
            {
                return false;
            }

            if (await NameExistsAsync(supplier.Name, id))
            {
                throw new InvalidOperationException(
                    "Ein aktiver Lieferant mit diesem Namen existiert bereits. " +
                    "Benennen Sie zuerst den aktiven Lieferanten um oder archivieren Sie ihn.");
            }

            supplier.IsArchived = false;

            await SaveAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//

        private Task<bool> NameExistsAsync(string name, int? excludeSupplierId)
        {
            return _context.Suppliers.AnyAsync(s =>
                !s.IsArchived &&
                s.Name == name &&
                (excludeSupplierId == null || s.Id != excludeSupplierId.Value));
        }

        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                // Sicherheitsnetz: Der eindeutige Index der Datenbank hat den Eintrag abgelehnt
                // (z. B. wenn zwei Personen gleichzeitig denselben Namen anlegen).
                throw new InvalidOperationException(DuplicateNameMessage);
            }
        }

        private static bool IsDuplicateKey(DbUpdateException ex) =>
            ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);

        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}