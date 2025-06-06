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
            // Ta metoda zwraca pierwszy napotkany zespół o danej nazwie.
            // UWAGA: Nie filtruje po statusie - zwraca zarówno aktywne jak i archiwalne zespoły.
            // Jeśli potrzebujesz tylko aktywnych zespółów, użyj GetActiveTeamByNameAsync.
            return await _dbSet.FirstOrDefaultAsync(t => t.DisplayName == displayName);
        }

        public async Task<Team?> GetActiveTeamByNameAsync(string displayName)
        {
            // Kopiujemy wzorzec Include z GetByIdAsync i dodajemy filtrowanie po Status
            return await _dbSet
                .Include(t => t.SchoolType)
                .Include(t => t.SchoolYear)
                .Include(t => t.Template)
                .Include(t => t.Members)
                    .ThenInclude(m => m.User)
                .Include(t => t.Channels)
                .FirstOrDefaultAsync(t => t.DisplayName == displayName && t.Status == TeamStatus.Active);
        }

        public async Task<Team?> GetActiveByIdAsync(object id)
        {
            if (id is string stringId)
            {
                // Kopiujemy dokładnie wzorzec z GetByIdAsync ale z dodatkowym filtrem Status
                return await _dbSet
                    .Include(t => t.SchoolType)
                    .Include(t => t.SchoolYear)
                    .Include(t => t.Template)
                    .Include(t => t.Members)
                        .ThenInclude(m => m.User)
                    .Include(t => t.Channels)
                    .FirstOrDefaultAsync(t => t.Id == stringId && t.Status == TeamStatus.Active);
            }
            return null;
        }

        public async Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn)
        {
            // Zwraca zespoły danego właściciela, które są aktywne (Status == TeamStatus.Active).
            // Używamy bezpośrednio Status zamiast IsActive, ponieważ EF nie potrafi przetłumaczyć właściwości obliczeniowej.
            return await _dbSet.Where(t => t.Owner == ownerUpn && t.Status == TeamStatus.Active).ToListAsync();
        }

        public async Task<IEnumerable<Team>> GetActiveTeamsAsync()
        {
            // Zwraca zespoły, których Status to Active.
            // Używamy bezpośrednio Status zamiast IsActive dla kompatybilności z LINQ to Entities.
            return await _dbSet.Where(t => t.Status == TeamStatus.Active).ToListAsync();
        }

        public async Task<IEnumerable<Team>> GetArchivedTeamsAsync()
        {
            // Zwraca zespoły, których Status to Archived.
            // Właściwość Team.IsActive (obliczeniowa) będzie false dla tych zespołów.
            return await _dbSet.Where(t => t.Status == TeamStatus.Archived).ToListAsync();
        }

        public override async Task<Team?> GetByIdAsync(object id)
        {
            // UWAGA: Ta metoda zwraca zespół niezależnie od statusu (Active/Archived).
            // Jeśli potrzebujesz tylko aktywnych zespołów, użyj GetActiveByIdAsync.
            if (id is string stringId)
            {
                return await _dbSet
                    .Include(t => t.SchoolType)
                    .Include(t => t.SchoolYear)
                    .Include(t => t.Template)
                    .Include(t => t.Members)
                        .ThenInclude(m => m.User)
                    .Include(t => t.Channels)
                    .FirstOrDefaultAsync(t => t.Id == stringId);
            }
            return null;
        }
    }
}