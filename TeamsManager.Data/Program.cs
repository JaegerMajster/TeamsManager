using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using TeamsManager.Data;

namespace TeamsManager.Data
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Test bazy danych TeamsManager ===");
            
            // Konfiguracja DbContext
            var optionsBuilder = new DbContextOptionsBuilder<TeamsManagerDbContext>();
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "TeamsManager.UI", "bin", "Debug", "net9.0-windows", "teamsmanager_ui.db");
            
            // Upewnij się że katalog istnieje
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            
            using var context = new TeamsManagerDbContext(optionsBuilder.Options);
            
            try
            {
                Console.WriteLine($"Ścieżka do bazy danych: {dbPath}");
                Console.WriteLine($"Czy baza danych istnieje: {File.Exists(dbPath)}");
                
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    Console.WriteLine($"Rozmiar bazy danych: {fileInfo.Length} bajtów");
                }
                
                // Test połączenia
                Console.WriteLine("\nSprawdzanie połączenia z bazą danych...");
                var canConnect = await context.Database.CanConnectAsync();
                Console.WriteLine($"Można połączyć się z bazą: {canConnect}");
                
                if (canConnect)
                {
                    // Wyświetl obecne statystyki
                    Console.WriteLine("\n=== Obecne statystyki bazy danych ===");
                    Console.WriteLine($"Działy: {await context.Departments.CountAsync()}");
                    Console.WriteLine($"Użytkownicy: {await context.Users.CountAsync()}");
                    Console.WriteLine($"Typy szkół: {await context.SchoolTypes.CountAsync()}");
                    Console.WriteLine($"Lata szkolne: {await context.SchoolYears.CountAsync()}");
                    Console.WriteLine($"Zespoły: {await context.Teams.CountAsync()}");
                    Console.WriteLine($"Członkowie zespołów: {await context.TeamMembers.CountAsync()}");
                    Console.WriteLine($"Kanały: {await context.Channels.CountAsync()}");
                    Console.WriteLine($"Historia operacji: {await context.OperationHistories.CountAsync()}");
                    
                    // Seeding danych testowych
                    Console.WriteLine("\n=== Dodawanie przykładowych danych ===");
                    await TestDataSeeder.SeedAsync(context);
                    
                    // Wyświetl finalne statystyki
                    Console.WriteLine("\n=== Finalne statystyki bazy danych ===");
                    Console.WriteLine($"Działy: {await context.Departments.CountAsync()}");
                    Console.WriteLine($"Użytkownicy: {await context.Users.CountAsync()}");
                    Console.WriteLine($"Typy szkół: {await context.SchoolTypes.CountAsync()}");
                    Console.WriteLine($"Lata szkolne: {await context.SchoolYears.CountAsync()}");
                    Console.WriteLine($"Zespoły: {await context.Teams.CountAsync()}");
                    Console.WriteLine($"Członkowie zespołów: {await context.TeamMembers.CountAsync()}");
                    Console.WriteLine($"Kanały: {await context.Channels.CountAsync()}");  
                    Console.WriteLine($"Historia operacji: {await context.OperationHistories.CountAsync()}");
                    
                    // Test prostego zapytania
                    Console.WriteLine("\n=== Test zapytań ===");
                    var departments = await context.Departments.ToListAsync();
                    Console.WriteLine("Działy w systemie:");
                    foreach (var dept in departments)
                    {
                        Console.WriteLine($"- {dept.Name} ({dept.DepartmentCode})");
                    }
                    
                    var users = await context.Users.Include(u => u.Department).ToListAsync();
                    Console.WriteLine("\nUżytkownicy w systemie:");
                    foreach (var user in users)
                    {
                        Console.WriteLine($"- {user.FirstName} {user.LastName} ({user.UPN}) - {user.Department?.Name ?? "Brak działu"}");
                    }
                }
                
                Console.WriteLine("\n=== Test zakończony pomyślnie! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nBłąd: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nNaciśnij dowolny klawisz aby zakończyć...");
            Console.ReadKey();
        }
    }
} 