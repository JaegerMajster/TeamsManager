using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.Services;

namespace TeamsManager.UI.Scripts
{
    /// <summary>
    /// Skrypt testowy do demonstracji operacji CRUD dla działów
    /// </summary>
    public static class TestDepartmentCRUD
    {
        /// <summary>
        /// Uruchamia kompletny test operacji CRUD dla działów
        /// </summary>
        public static async Task RunTestAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<DepartmentCodeMigrationService>>();
            var departmentService = serviceProvider.GetRequiredService<IDepartmentService>();
            
            logger.LogInformation("=== ROZPOCZYNANIE TESTU OPERACJI CRUD DLA DZIAŁÓW ===");
            
            try
            {
                // 1. CREATE - Tworzenie nowych działów
                await TestCreateOperationsAsync(departmentService, logger);
                
                // 2. READ - Odczytywanie działów
                await TestReadOperationsAsync(departmentService, logger);
                
                // 3. UPDATE - Aktualizacja działów
                await TestUpdateOperationsAsync(departmentService, logger);
                
                // 4. DELETE - Usuwanie działów
                await TestDeleteOperationsAsync(departmentService, logger);
                
                // 5. Migracja kodów działów
                var migrationService = serviceProvider.GetRequiredService<DepartmentCodeMigrationService>();
                await TestCodeMigrationAsync(migrationService, logger);
                
                logger.LogInformation("=== TEST OPERACJI CRUD ZAKOŃCZONY POMYŚLNIE ===");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Błąd podczas testu operacji CRUD: {ErrorMessage}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Test operacji CREATE - tworzenie działów
        /// </summary>
        private static async Task TestCreateOperationsAsync(IDepartmentService departmentService, ILogger logger)
        {
            logger.LogInformation("--- TEST CREATE: Tworzenie działów ---");
            
            // Tworzenie działu głównego
            var mainDepartment = await departmentService.CreateDepartmentAsync(
                "Wydział Techniczny",
                "Główny wydział techniczny szkoły",
                null, // brak rodzica - dział główny
                "TECH"
            );
            
            if (mainDepartment != null)
            {
                logger.LogInformation("✅ Utworzono dział główny: {Name} (ID: {Id})", mainDepartment.Name, mainDepartment.Id);
                
                // Tworzenie poddziałów
                var itDepartment = await departmentService.CreateDepartmentAsync(
                    "Informatyka",
                    "Dział informatyki i programowania",
                    mainDepartment.Id,
                    "IT"
                );
                
                var mechDepartment = await departmentService.CreateDepartmentAsync(
                    "Mechanika",
                    "Dział mechaniki i automatyki",
                    mainDepartment.Id,
                    "MECH"
                );
                
                if (itDepartment != null)
                {
                    logger.LogInformation("✅ Utworzono poddział: {Name} (ID: {Id})", itDepartment.Name, itDepartment.Id);
                    
                    // Tworzenie pod-poddziału
                    var webDepartment = await departmentService.CreateDepartmentAsync(
                        "Programowanie Web",
                        "Specjalizacja w technologiach webowych",
                        itDepartment.Id,
                        "WEB"
                    );
                    
                    if (webDepartment != null)
                    {
                        logger.LogInformation("✅ Utworzono pod-poddział: {Name} (ID: {Id})", webDepartment.Name, webDepartment.Id);
                    }
                }
                
                if (mechDepartment != null)
                {
                    logger.LogInformation("✅ Utworzono poddział: {Name} (ID: {Id})", mechDepartment.Name, mechDepartment.Id);
                }
            }
            else
            {
                logger.LogError("❌ Nie udało się utworzyć działu głównego");
            }
        }
        
        /// <summary>
        /// Test operacji READ - odczytywanie działów
        /// </summary>
        private static async Task TestReadOperationsAsync(IDepartmentService departmentService, ILogger logger)
        {
            logger.LogInformation("--- TEST READ: Odczytywanie działów ---");
            
            // Pobieranie wszystkich działów
            var allDepartments = await departmentService.GetAllDepartmentsAsync();
            logger.LogInformation("📋 Znaleziono {Count} działów w systemie", allDepartments.Count());
            
            foreach (var dept in allDepartments)
            {
                logger.LogInformation("  - {Name} (Kod: {Code}, Rodzic: {Parent})", 
                    dept.Name, 
                    dept.DepartmentCode ?? "brak", 
                    dept.ParentDepartmentId ?? "brak");
            }
            
            // Pobieranie tylko działów głównych
            var rootDepartments = await departmentService.GetAllDepartmentsAsync(onlyRootDepartments: true);
            logger.LogInformation("🏢 Znaleziono {Count} działów głównych", rootDepartments.Count());
            
            // Test pobierania konkretnego działu
            var firstDepartment = allDepartments.FirstOrDefault();
            if (firstDepartment != null)
            {
                var departmentById = await departmentService.GetDepartmentByIdAsync(
                    firstDepartment.Id, 
                    includeSubDepartments: true, 
                    includeUsers: true
                );
                
                if (departmentById != null)
                {
                    logger.LogInformation("🔍 Pobrano dział po ID: {Name}", departmentById.Name);
                    
                    // Pobieranie poddziałów
                    var subDepartments = await departmentService.GetSubDepartmentsAsync(firstDepartment.Id);
                    logger.LogInformation("  📁 Poddziały: {Count}", subDepartments.Count());
                }
            }
        }
        
        /// <summary>
        /// Test operacji UPDATE - aktualizacja działów
        /// </summary>
        private static async Task TestUpdateOperationsAsync(IDepartmentService departmentService, ILogger logger)
        {
            logger.LogInformation("--- TEST UPDATE: Aktualizacja działów ---");
            
            var allDepartments = await departmentService.GetAllDepartmentsAsync();
            var departmentToUpdate = allDepartments.FirstOrDefault(d => d.Name.Contains("Informatyka"));
            
            if (departmentToUpdate != null)
            {
                logger.LogInformation("🔄 Aktualizacja działu: {Name}", departmentToUpdate.Name);
                
                // Zapisz oryginalne wartości
                var originalDescription = departmentToUpdate.Description;
                var originalCode = departmentToUpdate.DepartmentCode;
                
                // Aktualizuj dane
                departmentToUpdate.Description = $"Zaktualizowany opis - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                departmentToUpdate.DepartmentCode = "IT-UPD";
                departmentToUpdate.Email = "informatyka@test.edu.pl";
                departmentToUpdate.Phone = "+48 123 456 789";
                departmentToUpdate.Location = "Budynek A, sala 101";
                departmentToUpdate.SortOrder = 10;
                
                var updateResult = await departmentService.UpdateDepartmentAsync(departmentToUpdate);
                
                if (updateResult)
                {
                    logger.LogInformation("✅ Pomyślnie zaktualizowano dział");
                    
                    // Sprawdź czy zmiany zostały zapisane
                    var updatedDepartment = await departmentService.GetDepartmentByIdAsync(departmentToUpdate.Id);
                    if (updatedDepartment != null)
                    {
                        logger.LogInformation("  📝 Nowy opis: {Description}", updatedDepartment.Description);
                        logger.LogInformation("  🏷️ Nowy kod: {Code}", updatedDepartment.DepartmentCode);
                        logger.LogInformation("  📧 Email: {Email}", updatedDepartment.Email);
                        logger.LogInformation("  📞 Telefon: {Phone}", updatedDepartment.Phone);
                        logger.LogInformation("  📍 Lokalizacja: {Location}", updatedDepartment.Location);
                    }
                }
                else
                {
                    logger.LogError("❌ Nie udało się zaktualizować działu");
                }
            }
            else
            {
                logger.LogWarning("⚠️ Nie znaleziono działu do aktualizacji");
            }
        }
        
        /// <summary>
        /// Test operacji DELETE - usuwanie działów
        /// </summary>
        private static async Task TestDeleteOperationsAsync(IDepartmentService departmentService, ILogger logger)
        {
            logger.LogInformation("--- TEST DELETE: Usuwanie działów ---");
            
            var allDepartments = await departmentService.GetAllDepartmentsAsync();
            
            // Znajdź dział bez poddziałów do usunięcia
            var departmentToDelete = allDepartments
                .Where(d => d.Name.Contains("Web") || d.Name.Contains("Programowanie"))
                .FirstOrDefault();
            
            if (departmentToDelete != null)
            {
                logger.LogInformation("🗑️ Próba usunięcia działu: {Name}", departmentToDelete.Name);
                
                // Sprawdź czy ma poddziały
                var subDepartments = await departmentService.GetSubDepartmentsAsync(departmentToDelete.Id);
                if (subDepartments.Any())
                {
                    logger.LogWarning("⚠️ Dział ma poddziały - nie można usunąć");
                }
                else
                {
                    var deleteResult = await departmentService.DeleteDepartmentAsync(departmentToDelete.Id);
                    
                    if (deleteResult)
                    {
                        logger.LogInformation("✅ Pomyślnie usunięto dział (logicznie)");
                        
                        // Sprawdź czy dział został oznaczony jako nieaktywny
                        var deletedDepartment = await departmentService.GetDepartmentByIdAsync(departmentToDelete.Id);
                        if (deletedDepartment != null)
                        {
                            logger.LogInformation("  🔍 Status działu po usunięciu: IsActive = {IsActive}", deletedDepartment.IsActive);
                        }
                    }
                    else
                    {
                        logger.LogError("❌ Nie udało się usunąć działu");
                    }
                }
            }
            else
            {
                logger.LogWarning("⚠️ Nie znaleziono odpowiedniego działu do usunięcia");
            }
            
            // Test próby usunięcia działu z poddziałami
            var departmentWithChildren = allDepartments
                .FirstOrDefault(d => d.Name.Contains("Techniczny") || d.Name.Contains("Informatyka"));
            
            if (departmentWithChildren != null)
            {
                var subDepartments = await departmentService.GetSubDepartmentsAsync(departmentWithChildren.Id);
                if (subDepartments.Any())
                {
                    logger.LogInformation("🧪 Test usuwania działu z poddziałami: {Name}", departmentWithChildren.Name);
                    
                    try
                    {
                        var deleteResult = await departmentService.DeleteDepartmentAsync(departmentWithChildren.Id);
                        logger.LogWarning("⚠️ Nieoczekiwanie udało się usunąć dział z poddziałami");
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogInformation("✅ Poprawnie zablokowano usunięcie działu z poddziałami: {Message}", ex.Message);
                    }
                }
            }
        }
        
        /// <summary>
        /// Test migracji kodów działów
        /// </summary>
        private static async Task TestCodeMigrationAsync(DepartmentCodeMigrationService migrationService, ILogger logger)
        {
            logger.LogInformation("--- TEST MIGRACJI KODÓW DZIAŁÓW ---");
            
            try
            {
                var result = await migrationService.MigrateDepartmentCodesAsync();
                
                logger.LogInformation("📊 Wyniki migracji:");
                logger.LogInformation("  ✅ Zaktualizowano: {Updated}", result.UpdatedDepartments);
                logger.LogInformation("  ⏭️ Pominięto: {Skipped}", result.SkippedDepartments);
                logger.LogInformation("  ❌ Błędy: {Errors}", result.ErroredDepartments);
                logger.LogInformation("  🎯 Sukces: {Success}", result.IsSuccess);
                
                if (result.Changes.Any())
                {
                    logger.LogInformation("📋 Szczegóły zmian:");
                    foreach (var change in result.Changes.Take(10)) // Pokaż tylko pierwsze 10
                    {
                        logger.LogInformation("  - {DepartmentName}: '{OldCode}' -> '{NewCode}'", 
                            change.DepartmentName, change.OldCode ?? "BRAK", change.NewCode);
                    }
                }
                
                logger.LogInformation("📄 Podsumowanie: {Summary}", result.GetSummary());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Błąd podczas migracji kodów: {Message}", ex.Message);
            }
        }
    }
} 