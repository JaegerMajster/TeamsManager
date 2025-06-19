using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Models;
using TeamsManager.Data;

namespace TeamsManager.UI.Scripts
{
    /// <summary>
    /// Skrypt do utworzenia domyślnej jednostki organizacyjnej "Podstawowy"
    /// i przypisania do niej wszystkich obecnych działów
    /// </summary>
    public static class CreateDefaultOrganizationalUnit
    {
        public static async Task ExecuteAsync(TeamsManagerDbContext context)
        {
            Console.WriteLine("🔄 Rozpoczynam tworzenie domyślnej jednostki organizacyjnej...");

            try
            {
    
                try
                {
                    var tableExists = await context.OrganizationalUnits.AnyAsync();
                    Console.WriteLine($"✅ Tabela OrganizationalUnits istnieje i zawiera {await context.OrganizationalUnits.CountAsync()} rekordów");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Tabela OrganizationalUnits nie istnieje lub jest niedostępna: {ex.Message}");
                    Console.WriteLine("🔄 Próbuję zastosować migracje...");
                    
                    // Spróbuj zastosować migracje
                    await context.Database.MigrateAsync();
                    Console.WriteLine("✅ Migracje zostały zastosowane");
                }

    
                var existingUnit = await context.OrganizationalUnits
                    .FirstOrDefaultAsync(ou => ou.Name == "Podstawowy");

                string defaultUnitId;

                if (existingUnit == null)
                {
                    // Utwórz domyślną jednostkę organizacyjną
                    var defaultUnit = new OrganizationalUnit
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Podstawowy",
                        Description = "Domyślna jednostka organizacyjna dla wszystkich działów",
                        ParentUnitId = null, // To jest jednostka główna
                        SortOrder = 0,
                        IsActive = true
                        // CreatedAt i UpdatedAt są automatycznie ustawiane przez BaseEntity
                    };

                    context.OrganizationalUnits.Add(defaultUnit);
                    await context.SaveChangesAsync();
                    
                    defaultUnitId = defaultUnit.Id;
                    Console.WriteLine($"✅ Utworzono domyślną jednostkę organizacyjną: {defaultUnit.Name} (ID: {defaultUnitId})");
                }
                else
                {
                    defaultUnitId = existingUnit.Id;
                    Console.WriteLine($"ℹ️ Jednostka organizacyjna 'Podstawowy' już istnieje (ID: {defaultUnitId})");
                }

                // Znajdź wszystkie działy które nie mają przypisanej jednostki organizacyjnej
                var departmentsWithoutOU = await context.Departments
                    .Where(d => d.OrganizationalUnitId == null)
                    .ToListAsync();

                if (departmentsWithoutOU.Any())
                {
                    Console.WriteLine($"🔄 Przypisuję {departmentsWithoutOU.Count} działów do domyślnej jednostki organizacyjnej...");

                    foreach (var department in departmentsWithoutOU)
                    {
                        department.OrganizationalUnitId = defaultUnitId;
                        // UpdatedAt jest automatycznie ustawiane przez BaseEntity
                        Console.WriteLine($"   📁 {department.Name} → Podstawowy");
                    }

                    await context.SaveChangesAsync();
                    Console.WriteLine($"✅ Przypisano {departmentsWithoutOU.Count} działów do jednostki organizacyjnej 'Podstawowy'");
                }
                else
                {
                    Console.WriteLine("ℹ️ Wszystkie działy mają już przypisane jednostki organizacyjne");
                }

                // Podsumowanie
                var totalDepartments = await context.Departments.CountAsync();
                var departmentsInDefaultOU = await context.Departments
                    .CountAsync(d => d.OrganizationalUnitId == defaultUnitId);

                Console.WriteLine($"📊 Podsumowanie:");
                Console.WriteLine($"   • Łączna liczba działów: {totalDepartments}");
                Console.WriteLine($"   • Działów w jednostce 'Podstawowy': {departmentsInDefaultOU}");
                Console.WriteLine("✅ Skrypt zakończony pomyślnie!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas wykonywania skryptu: {ex.Message}");
                Console.WriteLine($"📋 Szczegóły: {ex}");
                throw;
            }
        }
    }
} 