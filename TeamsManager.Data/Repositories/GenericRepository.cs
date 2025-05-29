using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Data; // Dla IGenericRepository
// Zakładamy, że BaseEntity jest w TeamsManager.Core.Models
using TeamsManager.Core.Models;

namespace TeamsManager.Data.Repositories
{
    /// <summary>
    /// Generyczna implementacja repozytorium dla podstawowych operacji CRUD.
    /// </summary>
    /// <typeparam name="TEntity">Typ encji, która musi być klasą i opcjonalnie dziedziczyć z BaseEntity.</typeparam>
    public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class // Można dodać: , BaseEntity
    {
        protected readonly TeamsManagerDbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public GenericRepository(TeamsManagerDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<TEntity>();
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<TEntity?> GetByIdAsync(object id)
        {
            // Dla stringowego ID, jak w BaseEntity
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public virtual void Update(TEntity entity)
        {
            _dbSet.Attach(entity); // Dołącza encję do kontekstu, jeśli jeszcze nie jest śledzona
            _context.Entry(entity).State = EntityState.Modified; // Oznacza encję jako zmodyfikowaną
        }

        public virtual void Delete(TEntity entity)
        {
            // Jeśli encja jest śledzona przez kontekst, wystarczy ją oznaczyć jako usuniętą
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity); // Dołącz, jeśli nie jest śledzona
            }
            _dbSet.Remove(entity); // Oznacza encję jako usuniętą
        }

        public virtual async Task DeleteByIdAsync(object id)
        {
            var entityToDelete = await GetByIdAsync(id);
            if (entityToDelete != null)
            {
                Delete(entityToDelete);
            }
        }

        public virtual void DeleteRange(IEnumerable<TEntity> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        // Metoda SaveChangesAsync nie jest tutaj implementowana.
        // Zmiany będą zapisywane przez wywołanie _context.SaveChangesAsync()
        // na wyższym poziomie (np. w serwisie lub jednostce pracy).
    }
}