using System;
using System.Threading;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Wzorzec Unit of Work zapewniający transakcyjność operacji na bazie danych.
    /// Zarządza cyklem życia DbContext i koordynuje pracę wielu repozytoriów.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Pobiera repozytorium dla określonego typu encji.
        /// Repozytoria współdzielą ten sam DbContext w ramach jednostki pracy.
        /// </summary>
        /// <typeparam name="TEntity">Typ encji dziedziczący z BaseEntity</typeparam>
        /// <returns>Instancja repozytorium dla danego typu</returns>
        IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;

        /// <summary>
        /// Zatwierdza wszystkie zmiany w ramach jednostki pracy.
        /// W przypadku błędu automatycznie wykonuje rollback.
        /// </summary>
        /// <param name="cancellationToken">Token anulowania operacji</param>
        /// <returns>Liczba zmodyfikowanych rekordów</returns>
        Task<int> CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rozpoczyna nową transakcję bazodanową.
        /// Użyj gdy potrzebujesz jawnej kontroli nad transakcją.
        /// </summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Zatwierdza bieżącą transakcję.
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Wycofuje wszystkie niezatwierdzone zmiany.
        /// </summary>
        Task RollbackAsync();

        /// <summary>
        /// Sprawdza czy istnieją niezapisane zmiany.
        /// </summary>
        bool HasChanges();

        // Specjalizowane repozytoria - dla wygody i zachowania kompatybilności
        IUserRepository Users { get; }
        ITeamRepository Teams { get; }
        ITeamTemplateRepository TeamTemplates { get; }
        ISchoolYearRepository SchoolYears { get; }
        IOperationHistoryRepository OperationHistories { get; }
        IApplicationSettingRepository ApplicationSettings { get; }
        ISubjectRepository Subjects { get; }
    }
} 