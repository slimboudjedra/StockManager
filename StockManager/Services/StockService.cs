using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.ViewModels;

namespace StockManager.Services
{
    public class StockService
    {
        private const int MaxCommentLength = 250;

        private readonly ApplicationDbContext _context;

        public StockService(ApplicationDbContext context)
        {
            _context = context;
        }

        //---------------------------------------------------------------------------------------------------------//

        // Bereitet das Formular vor. Gibt null zurück, wenn das Produkt
        // nicht existiert oder archiviert ist.
        public async Task<StockMovementCreateViewModel?> GetMovementFormAsync(int productId)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId && !p.IsArchived)
                .Select(p => new StockMovementCreateViewModel
                {
                    ProductId = p.Id,
                    ProductReference = p.Reference,
                    ProductName = p.Name,
                    CurrentQuantity = p.Quantity,
                    MinimumStock = p.MinimumStock
                })
                .FirstOrDefaultAsync();
        }

        //---------------------------------------------------------------------------------------------------------//

        // Gibt null zurück, wenn das Produkt nicht existiert oder archiviert ist.
        // Wirft InvalidOperationException, wenn eine Geschäftsregel verletzt wird.
        public async Task<StockMovement?> RecordMovementAsync(
            int productId,
            StockMovementType movementType,
            int quantity,
            string? comment)
        {
            var cleanComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

            if (cleanComment != null && cleanComment.Length > MaxCommentLength)
            {
                throw new InvalidOperationException(
                    $"Der Kommentar darf maximal {MaxCommentLength} Zeichen lang sein.");
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsArchived);

            if (product == null)
            {
                return null;
            }

            var currentQuantity = product.Quantity;
            int change;

            switch (movementType)
            {
                case StockMovementType.Entry:
                    if (quantity < 1)
                    {
                        throw new InvalidOperationException("Die Menge muss mindestens 1 betragen.");
                    }
                    if (quantity > int.MaxValue - currentQuantity)
                    {
                        throw new InvalidOperationException("Die Menge ist zu groß.");
                    }
                    change = quantity;
                    break;

                case StockMovementType.Exit:
                    if (quantity < 1)
                    {
                        throw new InvalidOperationException("Die Menge muss mindestens 1 betragen.");
                    }
                    if (quantity > currentQuantity)
                    {
                        throw new InvalidOperationException(
                            $"Nicht genügend Bestand: verfügbar sind {currentQuantity}, angefordert wurden {quantity}.");
                    }
                    change = -quantity;
                    break;

                case StockMovementType.Adjustment:
                    // Bei einer Korrektur ist "quantity" der gezählte Bestand.
                    if (quantity < 0)
                    {
                        throw new InvalidOperationException("Der gezählte Bestand darf nicht negativ sein.");
                    }
                    change = quantity - currentQuantity;
                    if (change == 0)
                    {
                        throw new InvalidOperationException(
                            $"Der Bestand beträgt bereits {currentQuantity}. Es gibt nichts zu korrigieren.");
                    }
                    break;

                default:
                    throw new InvalidOperationException("Ungültige Bewegungsart.");
            }

            var newQuantity = currentQuantity + change;

            product.Quantity = newQuantity;

            var movement = new StockMovement
            {
                ProductId = product.Id,
                MovementType = movementType,
                QuantityChange = change,
                QuantityAfter = newQuantity,
                Comment = cleanComment,
                CreatedAt = DateTime.UtcNow
            };

            _context.StockMovements.Add(movement);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "Der Bestand wurde zwischenzeitlich geändert. Bitte prüfen Sie den aktuellen Bestand und versuchen Sie es erneut.");
            }

            return movement;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<StockMovementHistoryViewModel?> GetHistoryAsync(int productId, int page, int pageSize = 10)
        {
            if (pageSize <= 0)
            {
                pageSize = 20;
            }

            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId && !p.IsArchived)
                .Select(p => new { p.Id, p.Reference, p.Name, p.Quantity })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return null;
            }

            var query = _context.StockMovements
                .AsNoTracking()
                .Where(m => m.ProductId == productId);

            var totalMovements = await query.CountAsync();

            var totalPages = (int)Math.Ceiling(totalMovements / (double)pageSize);

            page = totalPages > 0 ? Math.Clamp(page, 1, totalPages) : 1;

            var movements = await query
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new StockMovementHistoryViewModel
            {
                ProductId = product.Id,
                ProductReference = product.Reference,
                ProductName = product.Name,
                CurrentQuantity = product.Quantity,
                Movements = movements,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalMovements = totalMovements
            };
        }

    }
}