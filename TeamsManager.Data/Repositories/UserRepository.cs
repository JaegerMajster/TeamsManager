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

        /// <summary>
        /// Pobiera użytkownika po UPN.
        /// UWAGA: Ta metoda NIE filtruje po IsActive - może zwrócić nieaktywnych użytkowników.
        /// Rozważ użycie GetActiveUserByUpnAsync() jeśli potrzebujesz tylko aktywnych użytkowników.
        /// </summary>
        public async Task<User?> GetUserByUpnAsync(string upn)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.UPN == upn);
        }

        /// <summary>
        /// Pobiera aktywnego użytkownika po UPN z pełnym dołączeniem relacji.
        /// Zwraca tylko użytkowników z IsActive = true.
        /// </summary>
        public async Task<User?> GetActiveUserByUpnAsync(string upn)
        {
            return await _dbSet
                .Include(u => u.Department)
                .Include(u => u.TeamMemberships)
                    .ThenInclude(tm => tm.Team)
                .Include(u => u.SchoolTypeAssignments)
                    .ThenInclude(sta => sta.SchoolType)
                .Include(u => u.SupervisedSchoolTypes)
                .Include(u => u.TaughtSubjects)
                    .ThenInclude(us => us.Subject)
                .FirstOrDefaultAsync(u => u.UPN == upn && u.IsActive);
        }

        /// <summary>
        /// Pobiera aktywnego użytkownika po ID z pełnym dołączeniem relacji.
        /// Zwraca tylko użytkowników z IsActive = true.
        /// </summary>
        public async Task<User?> GetActiveByIdAsync(string id)
        {
            return await _dbSet
                .Include(u => u.Department)
                .Include(u => u.TeamMemberships)
                    .ThenInclude(tm => tm.Team)
                .Include(u => u.SchoolTypeAssignments)
                    .ThenInclude(sta => sta.SchoolType)
                .Include(u => u.SupervisedSchoolTypes)
                .Include(u => u.TaughtSubjects)
                    .ThenInclude(us => us.Subject)
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _dbSet.Where(u => u.Role == role && u.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                // Zwróć tylko aktywnych użytkowników zamiast wszystkich
                return await _dbSet.Where(u => u.IsActive).ToListAsync();
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