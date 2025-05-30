using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    public class OperationHistoryService : IOperationHistoryService
    {
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<OperationHistoryService> _logger;
        // Brak bezpośredniej zależności od DbContext

        public OperationHistoryService(
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<OperationHistoryService> logger)
        {
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OperationHistory> LogOperationAsync(
            OperationType type,
            OperationStatus status,
            string targetEntityType,
            string? targetEntityId = null,
            string? targetEntityName = null,
            string? details = null,
            string? parentOperationId = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_log";
            _logger.LogInformation("Logowanie operacji: Typ={OperationType}, Status={OperationStatus}, Encja={TargetEntityType}, IDEncji={TargetEntityId}, NazwaEncji={TargetEntityName}, Użytkownik={CurrentUser}",
                type, status, targetEntityType, targetEntityId, targetEntityName, currentUserUpn);

            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Status = status,
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId ?? string.Empty,
                TargetEntityName = targetEntityName ?? string.Empty,
                OperationDetails = details ?? string.Empty,
                ParentOperationId = parentOperationId,
                CreatedBy = currentUserUpn,
                IsActive = true // Logi są zawsze aktywne przy tworzeniu
            };

            // Ustawianie StartedAt i ewentualnie CompletedAt w zależności od statusu
            if (status == OperationStatus.Pending || status == OperationStatus.InProgress)
            {
                operation.MarkAsStarted(); // Ustawia StartedAt i zmienia Status na InProgress, jeśli Pending
                if (status == OperationStatus.Pending && operation.Status == OperationStatus.InProgress)
                {
                    // Jeśli MarkAsStarted zmienił status z Pending na InProgress, a my chcemy zachować Pending
                    operation.Status = OperationStatus.Pending;
                }
            }
            else if (status == OperationStatus.Completed || status == OperationStatus.Failed || status == OperationStatus.Cancelled || status == OperationStatus.PartialSuccess)
            {
                // Jeśli logujemy już zakończoną operację, ustawmy StartedAt i CompletedAt na ten sam czas (lub StartedAt na nieco wcześniej)
                operation.StartedAt = DateTime.UtcNow.AddMilliseconds(-10); // Małe przesunięcie dla Duration
                operation.MarkAsCompleted(details); // Używa MarkAsCompleted do ustawienia Status, CompletedAt, Duration
                operation.Status = status; // Nadpisujemy status, jeśli MarkAsCompleted ustawił tylko Completed
            }

            await _operationHistoryRepository.AddAsync(operation);
            // SaveChangesAsync będzie na wyższym poziomie

            _logger.LogInformation("Operacja ID: {OperationId} została zalogowana.", operation.Id);
            return operation;
        }

        public async Task<bool> UpdateOperationStatusAsync(
            string operationId,
            OperationStatus newStatus,
            string? message = null, // Może być errorMessage lub successMessage
            string? stackTrace = null)
        {
            _logger.LogInformation("Aktualizowanie statusu operacji ID: {OperationId} na {NewStatus}. Wiadomość: {Message}", operationId, newStatus, message);
            var operation = await _operationHistoryRepository.GetByIdAsync(operationId);
            if (operation == null)
            {
                _logger.LogWarning("Nie znaleziono operacji o ID: {OperationId} do aktualizacji statusu.", operationId);
                return false;
            }

            operation.Status = newStatus;
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (newStatus == OperationStatus.Failed)
                    operation.ErrorMessage = message;
                else if (newStatus == OperationStatus.Completed || newStatus == OperationStatus.PartialSuccess)
                    operation.OperationDetails = string.IsNullOrWhiteSpace(operation.OperationDetails) ? message : $"{operation.OperationDetails}\nStatus Update: {message}";
            }
            if (newStatus == OperationStatus.Failed && !string.IsNullOrWhiteSpace(stackTrace))
            {
                operation.ErrorStackTrace = stackTrace;
            }

            if (newStatus == OperationStatus.Completed || newStatus == OperationStatus.Failed || newStatus == OperationStatus.Cancelled || newStatus == OperationStatus.PartialSuccess)
            {
                if (!operation.CompletedAt.HasValue) operation.CompletedAt = DateTime.UtcNow;
                if (operation.StartedAt != default && operation.CompletedAt.HasValue) // StartedAt z BaseEntity może być default(DateTime)
                {
                    operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
                }
            }

            operation.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
            _operationHistoryRepository.Update(operation);
            // SaveChangesAsync na wyższym poziomie

            _logger.LogInformation("Status operacji ID: {OperationId} zaktualizowany na {NewStatus}.", operationId, newStatus);
            return true;
        }

        public async Task<bool> UpdateOperationProgressAsync(
            string operationId,
            int processedItems,
            int failedItems,
            int? totalItems = null)
        {
            _logger.LogInformation("Aktualizowanie postępu operacji ID: {OperationId}. Przetworzone: {ProcessedItems}, Nieudane: {FailedItems}, Razem: {TotalItems}",
                                operationId, processedItems, failedItems, totalItems);
            var operation = await _operationHistoryRepository.GetByIdAsync(operationId);
            if (operation == null)
            {
                _logger.LogWarning("Nie znaleziono operacji o ID: {OperationId} do aktualizacji postępu.", operationId);
                return false;
            }

            // Jeśli totalItems jest przekazywane i różni się od zapisanego, aktualizuj
            if (totalItems.HasValue && operation.TotalItems != totalItems.Value)
            {
                operation.TotalItems = totalItems.Value;
            }

            operation.UpdateProgress(processedItems, failedItems); // Użycie metody z modelu OperationHistory
            operation.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_progress_update");
            _operationHistoryRepository.Update(operation);
            // SaveChangesAsync na wyższym poziomie

            _logger.LogInformation("Postęp operacji ID: {OperationId} zaktualizowany. Status: {Status}", operationId, operation.Status);
            return true;
        }

        public async Task<OperationHistory?> GetOperationByIdAsync(string operationId)
        {
            _logger.LogInformation("Pobieranie operacji o ID: {OperationId}", operationId);
            return await _operationHistoryRepository.GetByIdAsync(operationId);
        }

        public async Task<IEnumerable<OperationHistory>> GetHistoryForEntityAsync(string targetEntityType, string targetEntityId, int? count = null)
        {
            _logger.LogInformation("Pobieranie historii dla encji: {TargetEntityType} ID: {TargetEntityId}, Limit: {Count}", targetEntityType, targetEntityId, count);
            return await _operationHistoryRepository.GetHistoryForEntityAsync(targetEntityType, targetEntityId, count);
        }

        public async Task<IEnumerable<OperationHistory>> GetHistoryByUserAsync(string userUpn, int? count = null)
        {
            _logger.LogInformation("Pobieranie historii dla użytkownika: {UserUPN}, Limit: {Count}", userUpn, count);
            return await _operationHistoryRepository.GetHistoryByUserAsync(userUpn, count);
        }

        public async Task<IEnumerable<OperationHistory>> GetHistoryByFilterAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            OperationType? operationType = null,
            OperationStatus? operationStatus = null,
            string? createdBy = null,
            int page = 1,
            int pageSize = 20)
        {
            _logger.LogInformation("Pobieranie historii z filtrowaniem. Zakres dat: {StartDate}-{EndDate}, Typ: {OperationType}, Status: {OperationStatus}, Użytkownik: {CreatedBy}, Strona: {Page}, Rozmiar: {PageSize}",
                startDate, endDate, operationType, operationStatus, createdBy, page, pageSize);

            // Implementacja filtrowania i paginacji. Na razie proste delegowanie,
            // ale repozytorium może potrzebować bardziej złożonej implementacji lub serwis musi to zrobić.
            // Jeśli IOperationHistoryRepository nie ma metody GetHistoryByFilterAsync, trzeba ją dodać tam
            // lub zaimplementować logikę filtrowania tutaj, używając FindAsync.

            // Przykład prostej implementacji filtrowania (bez paginacji na razie)
            // jeśli IOperationHistoryRepository nie ma metody GetHistoryByFilterAsync:
            /*
            Expression<Func<OperationHistory, bool>> predicate = oh => 
                (!startDate.HasValue || oh.StartedAt >= startDate.Value) &&
                (!endDate.HasValue || oh.StartedAt <= endDate.Value) &&
                (!operationType.HasValue || oh.Type == operationType.Value) &&
                (!operationStatus.HasValue || oh.Status == operationStatus.Value) &&
                (string.IsNullOrEmpty(createdBy) || oh.CreatedBy == createdBy);

            var allMatching = await _operationHistoryRepository.FindAsync(predicate);
            return allMatching.OrderByDescending(oh => oh.StartedAt)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize);
            */

            // Zakładając, że IOperationHistoryRepository ma metodę GetHistoryByDateRangeAsync,
            // ale nie pełne filtrowanie jak zdefiniowane w IOperationHistoryService.
            // Na razie oddelegujemy do tej, która istnieje, a filtrowanie rozbudujemy później.
            if (startDate.HasValue && endDate.HasValue)
            {
                return await _operationHistoryRepository.GetHistoryByDateRangeAsync(startDate.Value, endDate.Value, operationType, operationStatus);
            }

            // Proste pobranie wszystkiego, jeśli nie ma zakresu dat (do poprawy)
            _logger.LogWarning("GetHistoryByFilterAsync: Pobieranie wszystkich operacji z powodu braku pełnej implementacji filtrowania.");
            var allOps = await _operationHistoryRepository.GetAllAsync();
            if (operationType.HasValue) allOps = allOps.Where(oh => oh.Type == operationType.Value);
            if (operationStatus.HasValue) allOps = allOps.Where(oh => oh.Status == operationStatus.Value);
            if (!string.IsNullOrEmpty(createdBy)) allOps = allOps.Where(oh => oh.CreatedBy == createdBy);

            return allOps.OrderByDescending(oh => oh.StartedAt).Skip((page - 1) * pageSize).Take(pageSize);
        }

        // Metoda pomocnicza do zapisu OperationHistory (taka sama jak w innych serwisach)
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            // Logika tej metody jest taka sama jak w TeamService i UserService,
            // można by ją przenieść do jakiejś klasy bazowej dla serwisów lub wspólnego helpera.
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();

            if (string.IsNullOrEmpty(operation.CreatedBy)) // Upewnij się, że CreatedBy jest ustawione
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null)
            {
                await _operationHistoryRepository.AddAsync(operation);
            }
            else
            {
                existingLog.Status = operation.Status;
                existingLog.CompletedAt = operation.CompletedAt;
                existingLog.Duration = operation.Duration;
                existingLog.ErrorMessage = operation.ErrorMessage;
                existingLog.ErrorStackTrace = operation.ErrorStackTrace;
                existingLog.OperationDetails = operation.OperationDetails;
                existingLog.TargetEntityName = operation.TargetEntityName;
                existingLog.TargetEntityId = operation.TargetEntityId;
                existingLog.ProcessedItems = operation.ProcessedItems;
                existingLog.FailedItems = operation.FailedItems;
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
            // SaveChangesAsync będzie na wyższym poziomie
        }
    }
}