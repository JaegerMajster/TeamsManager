using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Data.Repositories
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(TeamsManagerDbContext context) : base(context)
        {
        }

        public async Task<User?> GetUserByUpnAsync(string upn)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.UPN == upn);
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _dbSet.Where(u => u.Role == role && u.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllAsync(); // Lub pustą listę, w zależności od wymagań
            }

            var lowerSearchTerm = searchTerm.ToLower();
            return await _dbSet.Where(u => u.IsActive &&
                                           (u.FirstName.ToLower().Contains(lowerSearchTerm) ||
                                            u.LastName.ToLower().Contains(lowerSearchTerm) ||
                                            u.UPN.ToLower().Contains(lowerSearchTerm)))
                               .ToListAsync();
        }

        // Możesz tutaj zaimplementować inne specyficzne metody dla User,
        // np. z bardziej złożonymi Include() dla pobierania powiązanych danych.
        // Przykład:
        public override async Task<User?> GetByIdAsync(object id)
        {
            if (id is string stringId)
            {
                return await _dbSet
                    .Include(u => u.Department)
                    .Include(u => u.TeamMemberships)
                        .ThenInclude(tm => tm.Team)
                    .Include(u => u.SchoolTypeAssignments)
                        .ThenInclude(sta => sta.SchoolType)
                    .Include(u => u.SupervisedSchoolTypes)
                    .FirstOrDefaultAsync(u => u.Id == stringId);
            }
            return null;
        }
    }
}