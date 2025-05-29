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
    public class TeamRepository : GenericRepository<Team>, ITeamRepository
    {
        public TeamRepository(TeamsManagerDbContext context) : base(context)
        {
        }

        public async Task<Team?> GetTeamByNameAsync(string displayName)
        {
            // Uwaga: Ta metoda zwróci pierwszy pasujący zespół. Jeśli nazwy nie są unikalne,
            // rozważ zwracanie IEnumerable<Team> lub dodanie innych kryteriów.
            return await _dbSet.FirstOrDefaultAsync(t => t.DisplayName == displayName);
        }

        public async Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn)
        {
            return await _dbSet.Where(t => t.Owner == ownerUpn && t.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<Team>> GetActiveTeamsAsync()
        {
            return await _dbSet.Where(t => t.Status == TeamStatus.Active && t.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<Team>> GetArchivedTeamsAsync()
        {
            return await _dbSet.Where(t => t.Status == TeamStatus.Archived && t.IsActive).ToListAsync();
            // Zwracamy tylko te zarchiwizowane, które nie są "soft-deleted" jako rekordy
        }

        // Przykład nadpisania GetByIdAsync z dołączaniem kluczowych relacji
        public override async Task<Team?> GetByIdAsync(object id)
        {
            if (id is string stringId)
            {
                return await _dbSet
                    .Include(t => t.SchoolType)
                    .Include(t => t.SchoolYear)
                    .Include(t => t.Template)
                    .Include(t => t.Members)
                        .ThenInclude(m => m.User) // Dołączamy użytkowników dla członków
                    .Include(t => t.Channels)
                    .FirstOrDefaultAsync(t => t.Id == stringId);
            }
            return null;
        }
    }
}