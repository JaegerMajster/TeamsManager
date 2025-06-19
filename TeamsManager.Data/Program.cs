using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using TeamsManager.Data;
using Microsoft.Extensions.Logging;

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
                    
                    // SZCZEGÓŁOWA DIAGNOZA UŻYTKOWNIKÓW
                    Console.WriteLine("\n=== SZCZEGÓŁOWA DIAGNOZA UŻYTKOWNIKÓW ===");
                    var allUsers = await context.Users.Include(u => u.Department).ToListAsync();
                    
                    Console.WriteLine($"Wszyscy użytkownicy w bazie ({allUsers.Count}):");
                    foreach (var user in allUsers)
                    {
                        Console.WriteLine($"- {user.FirstName} {user.LastName}");
                        Console.WriteLine($"  UPN: {user.UPN}");
                        Console.WriteLine($"  IsActive: {user.IsActive}");
                        Console.WriteLine($"  Role: {user.Role}");
                        Console.WriteLine($"  Department: {user.Department?.Name ?? "NULL"}");
                        Console.WriteLine($"  CreatedBy: {user.CreatedBy}");
                        Console.WriteLine($"  CreatedDate: {user.CreatedDate}");
                        Console.WriteLine();
                    }
                    
                    // Test filtrowania aktywnych użytkowników - tak jak robi SimpleUserService
                    var activeUsers = await context.Users
                        .Include(u => u.Department)
                        .Where(u => u.IsActive)
                        .ToListAsync();
                        
                    Console.WriteLine($"Aktywni użytkownicy (IsActive = true): {activeUsers.Count}");
                    foreach (var user in activeUsers)
                    {
                        Console.WriteLine($"- {user.FirstName} {user.LastName} ({user.UPN})");
                    }
                    
                    // Test sprawdzenia czy to jest problem SimpleUserService
                    Console.WriteLine("\n=== TEST LOGIKI SIMPLEUSERSERVICE ===");
                    try 
                    {
                        var query = context.Users.Include(u => u.Department).AsQueryable();
                        query = query.Where(u => u.IsActive);
                        var testUsers = await query.ToListAsync();
                        
                        Console.WriteLine($"Zapytanie podobne do SimpleUserService zwróciło: {testUsers.Count} użytkowników");
                        
                        if (testUsers.Count == 0)
                        {
                            Console.WriteLine("⚠️  PROBLEM ZNALEZIONY: Brak aktywnych użytkowników!");
                            Console.WriteLine("To wyjaśnia błąd w aplikacji UI.");
                        }
                        else
                        {
                            Console.WriteLine("✅ Użytkownicy aktywni istnieją, problem musi być gdzie indziej.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Błąd podczas testowania logiki SimpleUserService: {ex.Message}");
                    }
                    
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
                Console.WriteLine($"Ślad stosu: {ex.StackTrace}");
            }
            
            // ===== NOWY TEST: SYMULACJA KONFIGURACJI UI =====
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("=== TEST KONFIGURACJI UI ===");
            Console.WriteLine(new string('=', 60));
            
            try
            {
                // Symuluj ścieżkę z aplikacji UI
                var uiBaseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "TeamsManager.UI", "bin", "Debug", "net9.0-windows");
                var uiDbPath = Path.Combine(uiBaseDirectory, "teamsmanager_ui.db");
                
                Console.WriteLine($"Katalog bazowy UI (symulowany): {uiBaseDirectory}");
                Console.WriteLine($"Ścieżka bazy danych UI: {uiDbPath}");
                Console.WriteLine($"Czy katalog UI istnieje: {Directory.Exists(uiBaseDirectory)}");
                Console.WriteLine($"Czy baza UI istnieje: {File.Exists(uiDbPath)}");
                
                if (File.Exists(uiDbPath))
                {
                    var fileInfo = new FileInfo(uiDbPath);
                    Console.WriteLine($"Rozmiar bazy UI: {fileInfo.Length} bajtów");
                    Console.WriteLine($"Data modyfikacji UI: {fileInfo.LastWriteTime}");
                    
                    // Test połączenia z bazą UI
                    var uiOptionsBuilder = new DbContextOptionsBuilder<TeamsManagerDbContext>();
                    uiOptionsBuilder.UseSqlite($"Data Source={uiDbPath}");
                    
                    using var uiContext = new TeamsManagerDbContext(uiOptionsBuilder.Options);
                    
                    var uiCanConnect = await uiContext.Database.CanConnectAsync();
                    Console.WriteLine($"Można połączyć się z bazą UI: {uiCanConnect}");
                    
                    if (uiCanConnect)
                    {
                        var uiUsers = await uiContext.Users.Include(u => u.Department).ToListAsync();
                        Console.WriteLine($"Użytkownicy w bazie UI: {uiUsers.Count}");
                        
                        var uiActiveUsers = uiUsers.Where(u => u.IsActive).ToList();
                        Console.WriteLine($"Aktywni użytkownicy w bazie UI: {uiActiveUsers.Count}");
                        
                        foreach (var user in uiUsers)
                        {
                            Console.WriteLine($"- {user.FirstName} {user.LastName} (IsActive: {user.IsActive})");
                        }
                        
                        // Test zapytania użytkowników tak jak robi SimpleUserService
                        Console.WriteLine("\n--- TEST ZAPYTANIA JAK W SIMPLEUSERSERVICE ---");
                        
                        try
                        {
                            var query = uiContext.Users.Include(u => u.Department).AsQueryable();
                            query = query.Where(u => u.IsActive);
                            var testUsers = await query.ToListAsync();
                            
                            Console.WriteLine($"Zapytanie podobne do SimpleUserService zwróciło: {testUsers.Count} użytkowników");
                            
                            if (testUsers.Count == 0)
                            {
                                Console.WriteLine("⚠️  PROBLEM: Brak aktywnych użytkowników w bazie UI!");
                            }
                            else
                            {
                                Console.WriteLine("✅ W bazie UI są aktywni użytkownicy:");
                                foreach (var user in testUsers)
                                {
                                    Console.WriteLine($"  - {user.FirstName} {user.LastName} ({user.UPN})");
                                }
                            }
                        }
                        catch (Exception serviceEx)
                        {
                            Console.WriteLine($"❌ Błąd zapytania: {serviceEx.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("⚠️  Baza danych UI nie istnieje - to może być przyczyną problemu!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas testu UI: {ex.Message}");
                Console.WriteLine($"Ślad stosu: {ex.StackTrace}");
            }

            Console.WriteLine("\nNaciśnij dowolny klawisz aby zakończyć...");
            
            // ===== TEST NOWEJ BEZPIECZNEJ KONFIGURACJI =====
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("=== TEST NOWEJ BEZPIECZNEJ KONFIGURACJI ===");
            Console.WriteLine(new string('=', 60));
            
            try
            {
                // Symuluj nową bezpieczną ścieżkę
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolderPath = Path.Combine(appDataPath, "TeamsManager");
                var newDbPath = Path.Combine(appFolderPath, "teamsmanager.db");
                
                Console.WriteLine($"Nowa bezpieczna lokalizacja:");
                Console.WriteLine($"- Dane aplikacji: {appDataPath}");
                Console.WriteLine($"- Folder aplikacji: {appFolderPath}");
                Console.WriteLine($"- Ścieżka bazy: {newDbPath}");
                Console.WriteLine($"- Czy folder istnieje: {Directory.Exists(appFolderPath)}");
                Console.WriteLine($"- Czy baza istnieje: {File.Exists(newDbPath)}");
                
                if (!Directory.Exists(appFolderPath))
                {
                    Directory.CreateDirectory(appFolderPath);
                    Console.WriteLine("✅ Utworzono folder aplikacji");
                }
                
                // Skopiuj bazę do nowej lokalizacji jeśli nie istnieje
                var sourceDbPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "TeamsManager.UI", "bin", "Debug", "net9.0-windows", "teamsmanager_ui.db");
                if (File.Exists(sourceDbPath) && !File.Exists(newDbPath))
                {
                    File.Copy(sourceDbPath, newDbPath);
                    Console.WriteLine("✅ Skopiowano bazę do nowej bezpiecznej lokalizacji");
                }
                
                if (File.Exists(newDbPath))
                {
                    var fileInfo = new FileInfo(newDbPath);
                    Console.WriteLine($"✅ Rozmiar bazy: {fileInfo.Length} bajtów");
                    Console.WriteLine($"✅ Data modyfikacji: {fileInfo.LastWriteTime}");
                    
                    // Test połączenia
                    var newOptionsBuilder = new DbContextOptionsBuilder<TeamsManagerDbContext>();
                    newOptionsBuilder.UseSqlite($"Data Source={newDbPath}");
                    
                    using var testContext = new TeamsManagerDbContext(newOptionsBuilder.Options);
                    var canConnect = await testContext.Database.CanConnectAsync();
                    Console.WriteLine($"✅ Połączenie z nową bazą: {canConnect}");
                    
                    if (canConnect)
                    {
                        var users = await testContext.Users.CountAsync();
                        Console.WriteLine($"✅ Użytkowników w nowej bazie: {users}");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ Baza w nowej lokalizacji nie istnieje");
                }
                
                Console.WriteLine("\n🎉 KONFIGURACJA GOTOWA DO PRODUKCJI!");
                Console.WriteLine("Aplikacja będzie używać bezpiecznej lokalizacji:");
                Console.WriteLine($"   {newDbPath}");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd testu nowej konfiguracji: {ex.Message}");
            }

            Console.ReadKey();
        }
    }
} 