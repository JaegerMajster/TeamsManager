using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;

namespace TeamsManager.Data.Repositories
{
    public class SchoolYearRepository : GenericRepository<SchoolYear>, ISchoolYearRepository
    {
        public SchoolYearRepository(TeamsManagerDbContext context) : base(context)
        {
        }

        public async Task<SchoolYear?> GetCurrentSchoolYearAsync()
        {
            return await _dbSet.FirstOrDefaultAsync(sy => sy.IsCurrent && sy.IsActive);
        }

        public async Task<SchoolYear?> GetSchoolYearByNameAsync(string name)
        {
            return await _dbSet.FirstOrDefaultAsync(sy => sy.Name == name && sy.IsActive);
        }

        public async Task<IEnumerable<SchoolYear>> GetSchoolYearsActiveOnDateAsync(DateTime date)
        {
            var dateOnly = date.Date;
            return await _dbSet
                .Where(sy => sy.IsActive && sy.StartDate.Date <= dateOnly && sy.EndDate.Date >= dateOnly)
                .ToListAsync();
        }
    }
}