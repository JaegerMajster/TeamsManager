using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.Services;

namespace TeamsManager.UI.Scripts
{
    /// <summary>
    /// Skrypt testowy do demonstracji migracji kodów działów
    /// </summary>
    public static class TestDepartmentCodeMigration
    {
        /// <summary>
        /// Uruchamia test migracji kodów działów
        /// </summary>
        public static async Task RunTestAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<DepartmentCodeMigrationService>>();
            var departmentService = serviceProvider.GetRequiredService<IDepartmentService>();
            var migrationService = serviceProvider.GetRequiredService<DepartmentCodeMigrationService>();

            Console.WriteLine("=== TEST MIGRACJI KODÓW DZIAŁÓW ===");
            Console.WriteLine();

            try
            {
                // 1. Pokaż obecne działy
                Console.WriteLine("1. OBECNE DZIAŁY W BAZIE:");
                var currentDepartments = await departmentService.GetAllDepartmentsAsync();
                
                foreach (var dept in currentDepartments)
                {
                    var parentName = string.IsNullOrEmpty(dept.ParentDepartmentId) ? "BRAK" : 
                        (await departmentService.GetDepartmentByIdAsync(dept.ParentDepartmentId))?.Name ?? "NIEZNANY";
                    
                    Console.WriteLine($"  - {dept.Name}");
                    Console.WriteLine($"    Kod: {dept.DepartmentCode ?? "BRAK"}");
                    Console.WriteLine($"    Rodzic: {parentName}");
                    Console.WriteLine($"    Aktywny: {dept.IsActive}");
                    Console.WriteLine();
                }

                // 2. Dodaj przykładowe działy hierarchiczne dla testu
                Console.WriteLine("2. DODAWANIE PRZYKŁADOWYCH DZIAŁÓW HIERARCHICZNYCH:");
                
                // Główny dział "Nauki Ścisłe"
                var naukiScisle = await departmentService.CreateDepartmentAsync(
                    "Nauki Ścisłe", 
                    "Wydział Nauk Ścisłych", 
                    null, // brak rodzica
                    "NAUKI_SCISLE_OLD"); // stary kod
                
                Console.WriteLine($"  Utworzono: {naukiScisle?.Name} (kod: {naukiScisle?.DepartmentCode})");

                // Poddział "Matematyka"
                var matematyka = await departmentService.CreateDepartmentAsync(
                    "Matematyka", 
                    "Katedra Matematyki", 
                    naukiScisle?.Id,
                    "MAT_OLD"); // stary kod
                
                Console.WriteLine($"  Utworzono: {matematyka?.Name} (kod: {matematyka?.DepartmentCode})");

                // Poddział "Algebra"
                var algebra = await departmentService.CreateDepartmentAsync(
                    "Algebra", 
                    "Zakład Algebry", 
                    matematyka?.Id,
                    "ALG_OLD"); // stary kod
                
                Console.WriteLine($"  Utworzono: {algebra?.Name} (kod: {algebra?.DepartmentCode})");

                // Dział z polskimi znakami
                var fizyka = await departmentService.CreateDepartmentAsync(
                    "Fizyka Jądrowa", 
                    "Katedra Fizyki Jądrowej", 
                    naukiScisle?.Id,
                    "FIZ_JADR_OLD"); // stary kod
                
                Console.WriteLine($"  Utworzono: {fizyka?.Name} (kod: {fizyka?.DepartmentCode})");
                Console.WriteLine();

                // 3. Uruchom migrację
                Console.WriteLine("3. URUCHAMIANIE MIGRACJI KODÓW:");
                var result = await migrationService.MigrateDepartmentCodesAsync();
                
                Console.WriteLine(result.GetSummary());
                Console.WriteLine();

                // 4. Pokaż działy po migracji
                Console.WriteLine("4. DZIAŁY PO MIGRACJI:");
                var migratedDepartments = await departmentService.GetAllDepartmentsAsync();
                
                foreach (var dept in migratedDepartments)
                {
                    var parentName = string.IsNullOrEmpty(dept.ParentDepartmentId) ? "BRAK" : 
                        (await departmentService.GetDepartmentByIdAsync(dept.ParentDepartmentId))?.Name ?? "NIEZNANY";
                    
                    Console.WriteLine($"  - {dept.Name}");
                    Console.WriteLine($"    Kod: {dept.DepartmentCode ?? "BRAK"}");
                    Console.WriteLine($"    Rodzic: {parentName}");
                    Console.WriteLine($"    Pełna ścieżka: {dept.FullPath}");
                    Console.WriteLine();
                }

                Console.WriteLine("=== TEST ZAKOŃCZONY POMYŚLNIE ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BŁĄD PODCZAS TESTU: {ex.Message}");
                logger.LogError(ex, "Błąd podczas testu migracji kodów działów");
            }
        }

        /// <summary>
        /// Demonstracja normalizacji nazw działów
        /// </summary>
        public static void DemonstrateNameNormalization()
        {
            Console.WriteLine("=== DEMONSTRACJA NORMALIZACJI NAZW ===");
            Console.WriteLine();

            var testNames = new[]
            {
                "Matematyka",
                "Fizyka Jądrowa", 
                "Chemia Organiczna",
                "Język Polski",
                "Wychowanie Fizyczne",
                "Informatyka & Technologie",
                "Nauki Społeczne (Socjologia)",
                "Biologia - Ekologia",
                "Historia Polski XIX w."
            };

            foreach (var name in testNames)
            {
                var normalized = NormalizeName(name);
                Console.WriteLine($"  '{name}' -> '{normalized}'");
            }

            Console.WriteLine();
            Console.WriteLine("=== KONIEC DEMONSTRACJI ===");
        }

        /// <summary>
        /// Pomocnicza metoda normalizacji (kopia z DepartmentCodeMigrationService)
        /// </summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Usuń polskie znaki
            var normalizedString = name.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var result = stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
            
            // Usuń spacje i znaki specjalne, zostaw tylko litery i cyfry
            result = new string(result.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            return result;
        }
    }
} 