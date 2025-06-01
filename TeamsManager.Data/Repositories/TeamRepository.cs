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
            // Obecnie nie filtruje po statusie ani IsActive.
            // Jeśli potrzebna jest filtracja, należałoby ją dodać, np.
            // return await _dbSet.FirstOrDefaultAsync(t => t.DisplayName == displayName && t.IsActive);
            // lub
            // return await _dbSet.FirstOrDefaultAsync(t => t.DisplayName == displayName && t.Status == TeamStatus.Active);
            return await _dbSet.FirstOrDefaultAsync(t => t.DisplayName == displayName);
        }

        public async Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn)
        {
            // Zwraca zespoły danego właściciela, które są aktywne (Status == TeamStatus.Active).
            // Właściwość Team.IsActive jest teraz obliczana na podstawie Status,
            // więc warunek t.IsActive w LINQ to Entities będzie prawidłowo przetłumaczony
            // na sprawdzenie Statusu.
            return await _dbSet.Where(t => t.Owner == ownerUpn && t.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<Team>> GetActiveTeamsAsync()
        {
            // Zwraca zespoły, których Status to Active.
            // Właściwość Team.IsActive (obliczeniowa) również będzie true dla tych zespołów.
            // Użycie t.IsActive jest bardziej zwięzłe i zgodne z nową logiką modelu.
            return await _dbSet.Where(t => t.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<Team>> GetArchivedTeamsAsync()
        {
            // Zwraca zespoły, których Status to Archived.
            // Właściwość Team.IsActive (obliczeniowa) będzie false dla tych zespołów.
            return await _dbSet.Where(t => t.Status == TeamStatus.Archived).ToListAsync();
        }

        // Metoda GetByIdAsync z GenericRepository jest wystarczająca,
        // ponieważ serwis decyduje o dalszym przetwarzaniu na podstawie IsActive.
        // Jeśli jednak chcielibyśmy, aby repozytorium ZAWSZE dołączało te encje,
        // można by nadpisać metodę GetByIdAsync tutaj.
        // Obecna implementacja GetByIdAsync w TeamRepository jest dobra,
        // ponieważ dołącza kluczowe zależności.
        public override async Task<Team?> GetByIdAsync(object id)
        {
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