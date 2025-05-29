using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;

namespace TeamsManager.Data.Repositories
{
    public class ApplicationSettingRepository : GenericRepository<ApplicationSetting>, IApplicationSettingRepository
    {
        public ApplicationSettingRepository(TeamsManagerDbContext context) : base(context)
        {
        }

        public async Task<ApplicationSetting?> GetSettingByKeyAsync(string key)
        {
            return await _dbSet.FirstOrDefaultAsync(s => s.Key == key && s.IsActive);
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsByCategoryAsync(string category)
        {
            return await _dbSet.Where(s => s.Category == category && s.IsActive).ToListAsync();
        }
    }
}