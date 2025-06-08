using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace TeamsManager.Data
{
    /// <summary>
    /// Fabryka DbContext używana przez narzędzia Entity Framework Core w czasie projektowania.
    /// Potrzebna do wykonywania migracji i innych operacji EF Core.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TeamsManagerDbContext>
    {
        public TeamsManagerDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TeamsManagerDbContext>();
            
            // Ścieżka do bazy danych - w tym samym katalogu co w aplikacji UI
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "TeamsManager.UI", "bin", "Debug", "net9.0-windows", "teamsmanager_ui.db");
            
            // Upewnij się że katalog istnieje
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            
            // Używamy konstruktora bez ICurrentUserService
            return new TeamsManagerDbContext(optionsBuilder.Options);
        }
    }
} 