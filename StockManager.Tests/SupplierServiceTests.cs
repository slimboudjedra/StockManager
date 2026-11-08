using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Tests
{
    public class SupplierServiceTests
    {
        private static SupplierEditViewModel NewSupplier(string name, string? city = null) =>
            new() { Name = name, City = city };

        [Fact]
        public async Task Create_TrimsValues_AndStoresBlankOptionalFieldsAsNull()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var supplier = await service.CreateAsync(new SupplierEditViewModel
            {
                Name = "  Schrauben Müller GmbH  ",
                Email = "   ",
                City = " Hagen "
            });

            Assert.Equal("Schrauben Müller GmbH", supplier.Name);
            Assert.Null(supplier.Email);
            Assert.Equal("Hagen", supplier.City);
            Assert.False(supplier.IsArchived);
        }

        [Fact]
        public async Task Create_WithNameOfAnActiveSupplier_IsRejected()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            await service.CreateAsync(NewSupplier("Alpha GmbH"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(NewSupplier("Alpha GmbH")));

            var all = await service.GetSuppliersAsync(null);
            Assert.Single(all);
        }

        [Fact]
        public async Task Create_WithNameOfAnArchivedSupplier_IsAllowed()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var first = await service.CreateAsync(NewSupplier("Alpha GmbH"));
            await service.ArchiveAsync(first.Id);

            var second = await service.CreateAsync(NewSupplier("Alpha GmbH"));

            Assert.NotEqual(first.Id, second.Id);
        }

        [Fact]
        public async Task Update_ChangesTheFields()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var supplier = await service.CreateAsync(NewSupplier("Alpha GmbH"));

            var updated = await service.UpdateAsync(supplier.Id, new SupplierEditViewModel
            {
                Id = supplier.Id,
                Name = "Alpha AG",
                City = "Bochum"
            });

            Assert.True(updated);

            var edit = await service.GetForEditAsync(supplier.Id);
            Assert.NotNull(edit);
            Assert.Equal("Alpha AG", edit.Name);
            Assert.Equal("Bochum", edit.City);
        }

        [Fact]
        public async Task Update_UnknownSupplier_ReturnsFalse()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var updated = await service.UpdateAsync(9999, NewSupplier("Irgendwer"));

            Assert.False(updated);
        }

        [Fact]
        public async Task Update_ToTheNameOfAnotherActiveSupplier_IsRejected()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            await service.CreateAsync(NewSupplier("Alpha GmbH"));
            var beta = await service.CreateAsync(NewSupplier("Beta AG"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAsync(beta.Id, NewSupplier("Alpha GmbH")));
        }

        [Fact]
        public async Task Update_KeepingTheOwnName_IsAllowed()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var alpha = await service.CreateAsync(NewSupplier("Alpha GmbH"));

            var updated = await service.UpdateAsync(alpha.Id, NewSupplier("Alpha GmbH", "Essen"));

            Assert.True(updated);
        }

        [Fact]
        public async Task Archive_RemovesTheSupplierFromListAndLookups()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var supplier = await service.CreateAsync(NewSupplier("Alpha GmbH"));

            var archived = await service.ArchiveAsync(supplier.Id);

            Assert.True(archived);
            Assert.Empty(await service.GetSuppliersAsync(null));
            Assert.Null(await service.GetByIdAsync(supplier.Id));
            Assert.Null(await service.GetForEditAsync(supplier.Id));
        }

        [Fact]
        public async Task Archive_UnknownSupplier_ReturnsFalse()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            Assert.False(await service.ArchiveAsync(9999));
        }

        [Fact]
        public async Task Search_FiltersByNameAndCity_AndIgnoresArchivedSuppliers()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            await service.CreateAsync(NewSupplier("Schrauben Müller GmbH", "Hagen"));
            await service.CreateAsync(NewSupplier("Kabel Nord AG", "Hamburg"));
            var archived = await service.CreateAsync(NewSupplier("Schrauben Archiv", "Kiel"));
            await service.ArchiveAsync(archived.Id);

            var byName = await service.GetSuppliersAsync("Schrauben");
            var byCity = await service.GetSuppliersAsync("Hamburg");

            Assert.Single(byName);
            Assert.Equal("Schrauben Müller GmbH", byName[0].Name);
            Assert.Single(byCity);
            Assert.Equal("Kabel Nord AG", byCity[0].Name);
        }

        [Fact]
        public async Task List_IsSortedByName()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            await service.CreateAsync(NewSupplier("Zeta KG"));
            await service.CreateAsync(NewSupplier("Alpha GmbH"));
            await service.CreateAsync(NewSupplier("Mitte AG"));

            var all = await service.GetSuppliersAsync(null);

            Assert.Equal(
                new[] { "Alpha GmbH", "Mitte AG", "Zeta KG" },
                all.Select(s => s.Name).ToArray());
        }
                [Fact]
        public async Task ArchivedList_ShowsOnlyArchivedSuppliers()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            await service.CreateAsync(NewSupplier("Aktiv GmbH"));
            var old = await service.CreateAsync(NewSupplier("Alt AG"));
            await service.ArchiveAsync(old.Id);

            var archived = await service.GetSuppliersAsync(null, archived: true);
            var active = await service.GetSuppliersAsync(null);

            Assert.Single(archived);
            Assert.Equal("Alt AG", archived[0].Name);
            Assert.Single(active);
            Assert.Equal("Aktiv GmbH", active[0].Name);
        }

        [Fact]
        public async Task Restore_MakesTheSupplierActiveAgain()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var supplier = await service.CreateAsync(NewSupplier("Alpha GmbH"));
            await service.ArchiveAsync(supplier.Id);

            var restored = await service.RestoreAsync(supplier.Id);

            Assert.True(restored);
            Assert.NotNull(await service.GetByIdAsync(supplier.Id));
            Assert.Empty(await service.GetSuppliersAsync(null, archived: true));
        }

        [Fact]
        public async Task Restore_WhenAnActiveSupplierUsesTheName_IsRejected_AndStaysArchived()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var first = await service.CreateAsync(NewSupplier("Alpha GmbH"));
            await service.ArchiveAsync(first.Id);
            await service.CreateAsync(NewSupplier("Alpha GmbH"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RestoreAsync(first.Id));

            var archived = await service.GetSuppliersAsync(null, archived: true);
            Assert.Single(archived);
            Assert.Equal(first.Id, archived[0].Id);
        }

        [Fact]
        public async Task Restore_ActiveOrUnknownSupplier_ReturnsFalse()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var active = await service.CreateAsync(NewSupplier("Alpha GmbH"));

            Assert.False(await service.RestoreAsync(active.Id));
            Assert.False(await service.RestoreAsync(9999));
        }

        [Fact]
        public async Task Archive_WhenAnActiveProductUsesTheSupplier_IsRejected_AndSupplierStaysActive()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var supplier = await service.CreateAsync(NewSupplier("Alpha GmbH"));
            db.AddProduct(quantity: 0, preferredSupplierId: supplier.Id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ArchiveAsync(supplier.Id));

            Assert.Contains("Standardlieferant", ex.Message);
            Assert.NotNull(await service.GetByIdAsync(supplier.Id));
        }

        [Fact]
        public async Task Archive_Rejection_NamesTheNumberOfProducts()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var supplier = await service.CreateAsync(NewSupplier("Alpha GmbH"));
            db.AddProduct(quantity: 0, preferredSupplierId: supplier.Id);
            db.AddProduct(quantity: 0, preferredSupplierId: supplier.Id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ArchiveAsync(supplier.Id));

            Assert.Contains("2 aktiven Produkten", ex.Message);
        }

        [Fact]
        public async Task Archive_WhenOnlyArchivedProductsUseTheSupplier_IsAllowed()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new SupplierService(context);

            var supplier = await service.CreateAsync(NewSupplier("Alpha GmbH"));
            db.AddProduct(quantity: 0, isArchived: true, preferredSupplierId: supplier.Id);

            var archived = await service.ArchiveAsync(supplier.Id);

            Assert.True(archived);
        }
    }
}