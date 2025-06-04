using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z historią operacji (OperationHistory).
    /// </summary>
    public interface IOperationHistoryService
    {
        /// <summary>
        /// Asynchronicznie tworzy nowy wpis operacji w historii.
        /// Operacja zawsze rozpoczyna się ze statusem InProgress.
        /// </summary>
        /// <param name="type">Typ operacji.</param>
        /// <param name="targetEntityType">Typ encji, której dotyczy operacja.</param>
        /// <param name="targetEntityId">ID encji, której dotyczy operacja (opcjonalne).</param>
        /// <param name="targetEntityName">Nazwa lub opis encji docelowej (opcjonalne).</param>
        /// <param name="details">Opcjonalne szczegóły operacji (np. w formacie JSON).</param>
        /// <param name="parentOperationId">Opcjonalne ID operacji nadrzędnej.</param>
        /// <returns>Utworzony obiekt OperationHistory.</returns>
        Task<OperationHistory> CreateNewOperationEntryAsync(
            OperationType type,
            string targetEntityType,
            string? targetEntityId = null,
            string? targetEntityName = null,
            string? details = null,
            string? parentOperationId = null);

        /// <summary>
        /// Asynchronicznie aktualizuje status istniejącej operacji.
        /// </summary>
        /// <param name="operationId">ID operacji do zaktualizowania.</param>
        /// <param name="newStatus">Nowy status operacji.</param>
        /// <param name="message">Opcjonalna wiadomość (np. błędu lub sukcesu).</param>
        /// <param name="stackTrace">Opcjonalny stos wywołań dla błędów.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateOperationStatusAsync(
            string operationId,
            OperationStatus newStatus,
            string? message = null,
            string? stackTrace = null);

        /// <summary>
        /// Asynchronicznie aktualizuje postęp operacji wsadowej.
        /// </summary>
        /// <param name="operationId">ID operacji wsadowej.</param>
        /// <param name="processedItems">Liczba przetworzonych elementów.</param>
        /// <param name="failedItems">Liczba elementów, których przetwarzanie się nie powiodło.</param>
        /// <param name="totalItems">Opcjonalnie, całkowita liczba elementów (jeśli znana od początku).</param>
        /// <returns>True, jeśli aktualizacja postępu się powiodła.</returns>
        Task<bool> UpdateOperationProgressAsync(
            string operationId,
            int processedItems,
            int failedItems,
            int? totalItems = null);


        /// <summary>
        /// Asynchronicznie pobiera wpis historii operacji na podstawie jego ID.
        /// </summary>
        /// <param name="operationId">Identyfikator wpisu historii.</param>
        /// <returns>Obiekt OperationHistory lub null, jeśli nie znaleziono.</returns>
        Task<OperationHistory?> GetOperationByIdAsync(string operationId);

        /// <summary>
        /// Asynchronicznie pobiera historię operacji dla określonej encji docelowej.
        /// </summary>
        /// <param name="targetEntityType">Typ encji docelowej.</param>
        /// <param name="targetEntityId">Identyfikator encji docelowej.</param>
        /// <param name="count">Opcjonalna liczba ostatnich wpisów do pobrania.</param>
        /// <returns>Kolekcja wpisów historii operacji.</returns>
        Task<IEnumerable<OperationHistory>> GetHistoryForEntityAsync(string targetEntityType, string targetEntityId, int? count = null);

        /// <summary>
        /// Asynchronicznie pobiera historię operacji wykonanych przez określonego użytkownika.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika.</param>
        /// <param name="count">Opcjonalna liczba ostatnich wpisów do pobrania.</param>
        /// <returns>Kolekcja wpisów historii operacji.</returns>
        Task<IEnumerable<OperationHistory>> GetHistoryByUserAsync(string userUpn, int? count = null);

        /// <summary>
        /// Asynchronicznie pobiera historię operacji na podstawie filtrów.
        /// </summary>
        /// <param name="startDate">Początkowa data zakresu (opcjonalna).</param>
        /// <param name="endDate">Końcowa data zakresu (opcjonalna).</param>
        /// <param name="operationType">Opcjonalny typ operacji.</param>
        /// <param name="operationStatus">Opcjonalny status operacji.</param>
        /// <param name="createdBy">Opcjonalny UPN użytkownika, który wykonał operacje.</param>
        /// <param name="page">Numer strony (dla paginacji).</param>
        /// <param name="pageSize">Rozmiar strony (dla paginacji).</param>
        /// <returns>Kolekcja pasujących wpisów historii operacji.</returns>
        Task<IEnumerable<OperationHistory>> GetHistoryByFilterAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            OperationType? operationType = null,
            OperationStatus? operationStatus = null,
            string? createdBy = null,
            int page = 1,
            int pageSize = 20);
    }
}