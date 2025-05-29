using TeamsManager.Core.Models;
using TeamsManager.Core.Enums; // Dla OperationType i OperationStatus
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Interfejs repozytorium dla encji OperationHistory, rozszerzający IGenericRepository.
    /// </summary>
    public interface IOperationHistoryRepository : IGenericRepository<OperationHistory>
    {
        /// <summary>
        /// Asynchronicznie pobiera historię operacji dla określonej encji docelowej.
        /// </summary>
        /// <param name="targetEntityType">Typ encji docelowej (np. "Team", "User").</param>
        /// <param name="targetEntityId">Identyfikator encji docelowej.</param>
        /// <param name="count">Opcjonalna liczba ostatnich wpisów do pobrania.</param>
        /// <returns>Kolekcja wpisów historii operacji.</returns>
        Task<IEnumerable<OperationHistory>> GetHistoryForEntityAsync(string targetEntityType, string targetEntityId, int? count = null);

        /// <summary>
        /// Asynchronicznie pobiera historię operacji wykonanych przez określonego użytkownika.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika, który wykonał operacje.</param>
        /// <param name="count">Opcjonalna liczba ostatnich wpisów do pobrania.</param>
        /// <returns>Kolekcja wpisów historii operacji.</returns>
        Task<IEnumerable<OperationHistory>> GetHistoryByUserAsync(string userUpn, int? count = null);

        /// <summary>
        /// Asynchronicznie pobiera historię operacji określonego typu i/lub statusu w danym zakresie dat.
        /// </summary>
        /// <param name="startDate">Początkowa data zakresu.</param>
        /// <param name="endDate">Końcowa data zakresu.</param>
        /// <param name="operationType">Opcjonalny typ operacji.</param>
        /// <param name="operationStatus">Opcjonalny status operacji.</param>
        /// <returns>Kolekcja pasujących wpisów historii operacji.</returns>
        Task<IEnumerable<OperationHistory>> GetHistoryByDateRangeAsync(
            DateTime startDate,
            DateTime endDate,
            OperationType? operationType = null,
            OperationStatus? operationStatus = null);

        // Można rozważyć dodanie metody do pobierania operacji wsadowych z podoperacjami,
        // np. Task<IEnumerable<OperationHistory>> GetBatchOperationWithSubOperationsAsync(string parentOperationId);
    }
}