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
            
            // UŻYWAMY TEJ SAMEJ ŚCIEŻKI CO W APLIKACJI UI
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolderPath = Path.Combine(appDataPath, "TeamsManager");
            
            // Upewnij się że katalog istnieje
            if (!Directory.Exists(appFolderPath))
            {
                Directory.CreateDirectory(appFolderPath);
            }
            
            var dbPath = Path.Combine(appFolderPath, "teamsmanager.db");
            
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            
            // Używamy konstruktora bez ICurrentUserService
            return new TeamsManagerDbContext(optionsBuilder.Options);
        }
    }
} 