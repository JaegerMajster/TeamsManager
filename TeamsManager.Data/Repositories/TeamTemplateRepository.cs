using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;

namespace TeamsManager.Data.Repositories
{
    public class TeamTemplateRepository : GenericRepository<TeamTemplate>, ITeamTemplateRepository
    {
        public TeamTemplateRepository(TeamsManagerDbContext context) : base(context)
        {
        }

        public async Task<TeamTemplate?> GetDefaultTemplateForSchoolTypeAsync(string schoolTypeId)
        {
            return await _dbSet
                .Include(tt => tt.SchoolType) // Opcjonalnie dołączamy SchoolType
                .FirstOrDefaultAsync(tt => tt.SchoolTypeId == schoolTypeId && tt.IsDefault && tt.IsActive);
        }

        public async Task<IEnumerable<TeamTemplate>> GetUniversalTemplatesAsync()
        {
            return await _dbSet
                .Include(tt => tt.SchoolType)
                .Where(tt => tt.IsUniversal && tt.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<TeamTemplate>> GetTemplatesBySchoolTypeAsync(string schoolTypeId)
        {
            return await _dbSet
                .Include(tt => tt.SchoolType)
                .Where(tt => tt.SchoolTypeId == schoolTypeId && tt.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<TeamTemplate>> SearchTemplatesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await _dbSet.Where(tt => tt.IsActive).Include(tt => tt.SchoolType).ToListAsync();
            }
            var lowerSearchTerm = searchTerm.ToLower();
            return await _dbSet
                .Include(tt => tt.SchoolType)
                .Where(tt => tt.IsActive &&
                             (tt.Name.ToLower().Contains(lowerSearchTerm) ||
                              (tt.Description != null && tt.Description.ToLower().Contains(lowerSearchTerm)) ||
                              (tt.Category != null && tt.Category.ToLower().Contains(lowerSearchTerm))))
                .ToListAsync();
        }
    }
}