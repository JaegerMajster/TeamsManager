using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks; // Dla operacji asynchronicznych

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Generyczny interfejs repozytorium definiujący podstawowe operacje CRUD.
    /// </summary>
    /// <typeparam name="TEntity">Typ encji, dla której repozytorium jest tworzone. Musi dziedziczyć z BaseEntity.</typeparam>
    public interface IGenericRepository<TEntity> where TEntity : class // Można dodać ograniczenie where TEntity : BaseEntity, jeśli wszystkie encje z niego dziedziczą
    {
        /// <summary>
        /// Pobiera wszystkie encje danego typu.
        /// </summary>
        Task<IEnumerable<TEntity>> GetAllAsync();

        /// <summary>
        /// Pobiera encję na podstawie jej identyfikatora.
        /// </summary>
        /// <param name="id">Identyfikator encji.</param>
        Task<TEntity?> GetByIdAsync(object id); // Id może być stringiem lub int, stąd object

        /// <summary>
        /// Dodaje nową encję.
        /// </summary>
        /// <param name="entity">Encja do dodania.</param>
        Task AddAsync(TEntity entity);

        /// <summary>
        /// Dodaje kolekcję nowych encji.
        /// </summary>
        /// <param name="entities">Kolekcja encji do dodania.</param>
        Task AddRangeAsync(IEnumerable<TEntity> entities);

        /// <summary>
        /// Aktualizuje istniejącą encję.
        /// </summary>
        /// <param name="entity">Encja do zaktualizowania.</param>
        void Update(TEntity entity); // Update jest zwykle synchroniczny na poziomie śledzenia przez EF Core

        /// <summary>
        /// Usuwa encję.
        /// </summary>
        /// <param name="entity">Encja do usunięcia.</param>
        void Delete(TEntity entity); // Podobnie jak Update

        /// <summary>
        /// Usuwa encję na podstawie jej identyfikatora.
        /// </summary>
        /// <param name="id">Identyfikator encji do usunięcia.</param>
        Task DeleteByIdAsync(object id);

        /// <summary>
        /// Usuwa kolekcję encji.
        /// </summary>
        /// <param name="entities">Kolekcja encji do usunięcia.</param>
        void DeleteRange(IEnumerable<TEntity> entities);

        /// <summary>
        /// Wyszukuje encje spełniające określone kryteria.
        /// </summary>
        /// <param name="predicate">Predykat określający warunki wyszukiwania.</param>
        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// Sprawdza, czy istnieje jakakolwiek encja spełniająca określone kryteria.
        /// </summary>
        /// <param name="predicate">Predykat określający warunki.</param>
        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// Asynchronicznie zapisuje wszystkie zmiany do bazy danych.
        /// </summary>
        /// <returns>Liczba zmienionych wpisów w bazie danych.</returns>
        Task<int> SaveChangesAsync();

        // SaveChangesAsync będzie prawdopodobnie w jednostce pracy (Unit of Work) lub bezpośrednio w DbContext,
        // ale czasami dodaje się je do repozytorium, jeśli nie używa się UoW.
        // Na razie pominiemy SaveChangesAsync w generycznym repozytorium,
        // zakładając, że będzie wywoływane na poziomie serwisu przez DbContext lub Unit of Work.
    }
}