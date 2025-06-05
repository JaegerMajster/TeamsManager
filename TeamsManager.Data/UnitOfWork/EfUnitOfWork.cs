using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories;

namespace TeamsManager.Data.UnitOfWork
{
    /// <summary>
    /// Implementacja wzorca Unit of Work dla Entity Framework Core.
    /// Zarządza transakcjami i koordynuje pracę repozytoriów.
    /// </summary>
    public class EfUnitOfWork : IUnitOfWork
    {
        private readonly TeamsManagerDbContext _context;
        private readonly Dictionary<Type, object> _repositories;
        private readonly ILogger<EfUnitOfWork> _logger;
        private IDbContextTransaction? _currentTransaction;
        private bool _disposed;

        // Lazy-loaded specjalizowane repozytoria
        private IUserRepository? _userRepository;
        private ITeamRepository? _teamRepository;
        private ITeamTemplateRepository? _teamTemplateRepository;
        private ISchoolYearRepository? _schoolYearRepository;
        private IOperationHistoryRepository? _operationHistoryRepository;
        private IApplicationSettingRepository? _applicationSettingRepository;
        private ISubjectRepository? _subjectRepository;

        public EfUnitOfWork(
            TeamsManagerDbContext context,
            ILogger<EfUnitOfWork> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repositories = new Dictionary<Type, object>();
        }

        /// <inheritdoc />
        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
        {
            var type = typeof(TEntity);
            
            if (!_repositories.ContainsKey(type))
            {
                _repositories[type] = new GenericRepository<TEntity>(_context);
                _logger.LogDebug("Utworzono nowe repozytorium dla typu {EntityType}", type.Name);
            }

            return (IGenericRepository<TEntity>)_repositories[type];
        }

        /// <inheritdoc />
        public IUserRepository Users => 
            _userRepository ??= new UserRepository(_context);

        /// <inheritdoc />
        public ITeamRepository Teams => 
            _teamRepository ??= new TeamRepository(_context);

        /// <inheritdoc />
        public ITeamTemplateRepository TeamTemplates => 
            _teamTemplateRepository ??= new TeamTemplateRepository(_context);

        /// <inheritdoc />
        public ISchoolYearRepository SchoolYears => 
            _schoolYearRepository ??= new SchoolYearRepository(_context);

        /// <inheritdoc />
        public IOperationHistoryRepository OperationHistories => 
            _operationHistoryRepository ??= new OperationHistoryRepository(_context);

        /// <inheritdoc />
        public IApplicationSettingRepository ApplicationSettings => 
            _applicationSettingRepository ??= new ApplicationSettingRepository(_context);

        /// <inheritdoc />
        public ISubjectRepository Subjects => 
            _subjectRepository ??= new SubjectRepository(_context);

        /// <inheritdoc />
        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Rozpoczynanie zatwierdzania zmian w Unit of Work");
                
                var changesCount = _context.ChangeTracker.Entries()
                    .Count(e => e.State == EntityState.Added || 
                               e.State == EntityState.Modified || 
                               e.State == EntityState.Deleted);
                
                if (changesCount == 0)
                {
                    _logger.LogDebug("Brak zmian do zatwierdzenia");
                    return 0;
                }

                _logger.LogInformation("Zatwierdzanie {ChangesCount} zmian w bazie danych", changesCount);
                var result = await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Pomyślnie zatwierdzono {SavedCount} zmian", result);
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Błąd współbieżności podczas zapisywania zmian");
                await RollbackAsync();
                throw new InvalidOperationException("Dane zostały zmodyfikowane przez innego użytkownika. Odśwież dane i spróbuj ponownie.", ex);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania zmian w bazie danych");
                await RollbackAsync();
                throw new InvalidOperationException("Wystąpił błąd podczas zapisywania danych. Sprawdź poprawność danych.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas zatwierdzania zmian");
                await RollbackAsync();
                throw;
            }
        }

        /// <inheritdoc />
        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
            {
                _logger.LogWarning("Transakcja jest już rozpoczęta");
                return;
            }

            _logger.LogDebug("Rozpoczynanie nowej transakcji");
            _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                _logger.LogWarning("Brak aktywnej transakcji do zatwierdzenia");
                return;
            }

            try
            {
                _logger.LogDebug("Zatwierdzanie transakcji");
                await _currentTransaction.CommitAsync(cancellationToken);
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zatwierdzania transakcji");
                await RollbackAsync();
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RollbackAsync()
        {
            try
            {
                if (_currentTransaction != null)
                {
                    _logger.LogWarning("Wycofywanie transakcji");
                    await _currentTransaction.RollbackAsync();
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }

                // Odrzuć wszystkie śledzenia zmian w kontekście
                foreach (var entry in _context.ChangeTracker.Entries())
                {
                    switch (entry.State)
                    {
                        case EntityState.Modified:
                        case EntityState.Deleted:
                            entry.State = EntityState.Unchanged;
                            break;
                        case EntityState.Added:
                            entry.State = EntityState.Detached;
                            break;
                    }
                }
                
                _logger.LogInformation("Pomyślnie wycofano wszystkie zmiany");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wycofywania zmian");
                throw;
            }
        }

        /// <inheritdoc />
        public bool HasChanges()
        {
            return _context.ChangeTracker.HasChanges();
        }

        /// <summary>
        /// Zwalnia zasoby używane przez Unit of Work.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _currentTransaction?.Dispose();
                    _context.Dispose();
                    _logger.LogDebug("Unit of Work został zwolniony");
                }
                _disposed = true;
            }
        }
    }
} 