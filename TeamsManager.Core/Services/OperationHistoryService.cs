using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za zarządzanie historią operacji w systemie.
    /// </summary>
    public class OperationHistoryService : IOperationHistoryService
    {
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<OperationHistoryService> _logger;

        /// <summary>
        /// Konstruktor serwisu historii operacji.
        /// </summary>
        public OperationHistoryService(
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<OperationHistoryService> logger)
        {
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<OperationHistory> CreateNewOperationEntryAsync(
            OperationType type,
            string targetEntityType,
            string? targetEntityId = null,
            string? targetEntityName = null,
            string? details = null,
            string? parentOperationId = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_log";
            _logger.LogInformation("Logowanie nowej operacji: Typ={OperationType}, Encja={TargetEntityType}, IDEncji={TargetEntityId}, NazwaEncji={TargetEntityName}, Użytkownik={CurrentUser}",
                type, targetEntityType, targetEntityId ?? "N/A", targetEntityName ?? "N/A", currentUserUpn);

            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Status = OperationStatus.InProgress, // Zawsze zaczyna się jako InProgress
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId ?? string.Empty,
                TargetEntityName = targetEntityName ?? string.Empty,
                OperationDetails = details ?? string.Empty,
                ParentOperationId = parentOperationId,
                StartedAt = DateTime.UtcNow, // Ustawiamy od razu rozpoczęcie
                CreatedBy = currentUserUpn,
                IsActive = true
            };

            await _operationHistoryRepository.AddAsync(operation);
            // SaveChangesAsync będzie wywołane na wyższym poziomie (np. w kontrolerze)

            _logger.LogInformation("Nowy wpis historii operacji ID: {OperationId} został utworzony.", operation.Id);
            return operation;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateOperationStatusAsync(
            string operationId,
            OperationStatus newStatus,
            string? message = null,
            string? stackTrace = null)
        {
            _logger.LogInformation("Aktualizowanie statusu operacji ID: {OperationId} na {NewStatus}. Wiadomość: {Message}", operationId, newStatus, message);
            var operation = await _operationHistoryRepository.GetByIdAsync(operationId);
            if (operation == null)
            {
                _logger.LogWarning("Nie znaleziono operacji o ID: {OperationId} do aktualizacji statusu.", operationId);
                return false;
            }

            var oldStatus = operation.Status;
            operation.Status = newStatus;

            if (!string.IsNullOrWhiteSpace(message))
            {
                if (newStatus == OperationStatus.Failed)
                {
                    operation.ErrorMessage = message;
                }
                else // Dodajemy do OperationDetails tylko, jeśli nie jest to błąd
                {
                    operation.OperationDetails = string.IsNullOrWhiteSpace(operation.OperationDetails)
                                                 ? message
                                                 : $"{operation.OperationDetails}{Environment.NewLine}Aktualizacja statusu: {message}";
                }
            }

            if (newStatus == OperationStatus.Failed && !string.IsNullOrWhiteSpace(stackTrace))
            {
                operation.ErrorStackTrace = stackTrace;
            }

            // Jeśli operacja jest oznaczana jako zakończona (w jakikolwiek sposób)
            if ((newStatus == OperationStatus.Completed || newStatus == OperationStatus.Failed || newStatus == OperationStatus.Cancelled || newStatus == OperationStatus.PartialSuccess)
                && !operation.CompletedAt.HasValue) // Ustawiamy tylko raz
            {
                if (operation.StartedAt == default(DateTime))
                {
                    operation.StartedAt = DateTime.UtcNow.AddMilliseconds(-5); // Ustawiamy, jeśli jakoś się pominęło rozpoczęcie
                }
                operation.CompletedAt = DateTime.UtcNow;
                operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
            }

            operation.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_status_update");
            _operationHistoryRepository.Update(operation);
            // SaveChangesAsync na wyższym poziomie

            _logger.LogInformation("Status operacji ID: {OperationId} zaktualizowany z {OldStatus} na {NewStatus}.", operationId, oldStatus, newStatus);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateOperationProgressAsync(
            string operationId,
            int processedItems,
            int failedItems,
            int? totalItems = null)
        {
            _logger.LogInformation("Aktualizowanie postępu operacji ID: {OperationId}. Przetworzone: {ProcessedItems}, Nieudane: {FailedItems}, Razem: {TotalItems}",
                                operationId, processedItems, failedItems, totalItems ?? -1);
            var operation = await _operationHistoryRepository.GetByIdAsync(operationId);
            if (operation == null)
            {
                _logger.LogWarning("Nie znaleziono operacji o ID: {OperationId} do aktualizacji postępu.", operationId);
                return false;
            }

            if (totalItems.HasValue && operation.TotalItems != totalItems.Value)
            {
                operation.TotalItems = totalItems.Value;
            }

            operation.UpdateProgress(processedItems, failedItems); // Metoda w modelu OperationHistory zaktualizuje status
            operation.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_progress_update");
            _operationHistoryRepository.Update(operation);
            // SaveChangesAsync na wyższym poziomie

            _logger.LogInformation("Postęp operacji ID: {OperationId} zaktualizowany. Nowy status: {Status}, Przetworzone: {Processed}, Nieudane: {Failed}, Postęp: {Progress}%",
                operationId, operation.Status, operation.ProcessedItems, operation.FailedItems, operation.ProgressPercentage);
            return true;
        }


        /// <inheritdoc />
        public async Task<OperationHistory?> GetOperationByIdAsync(string operationId)
        {
            _logger.LogInformation("Pobieranie operacji o ID: {OperationId}", operationId);
            return await _operationHistoryRepository.GetByIdAsync(operationId);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<OperationHistory>> GetHistoryForEntityAsync(string targetEntityType, string targetEntityId, int? count = null)
        {
            _logger.LogInformation("Pobieranie historii dla encji: {TargetEntityType} ID: {TargetEntityId}, Limit: {Count}", targetEntityType, targetEntityId, count ?? -1);
            return await _operationHistoryRepository.GetHistoryForEntityAsync(targetEntityType, targetEntityId, count);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<OperationHistory>> GetHistoryByUserAsync(string userUpn, int? count = null)
        {
            _logger.LogInformation("Pobieranie historii dla użytkownika: {UserUPN}, Limit: {Count}", userUpn, count ?? -1);
            return await _operationHistoryRepository.GetHistoryByUserAsync(userUpn, count);
        }

        /// <inheritdoc />
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

            Expression<Func<OperationHistory, bool>> predicate = PredicateBuilder.True<OperationHistory>();

            if (startDate.HasValue)
            {
                predicate = predicate.And(oh => oh.StartedAt >= startDate.Value.Date); // Używamy .Date dla spójności
            }
            if (endDate.HasValue)
            {
                var nextDay = endDate.Value.Date.AddDays(1);
                predicate = predicate.And(oh => oh.StartedAt < nextDay);
            }
            if (operationType.HasValue)
            {
                predicate = predicate.And(oh => oh.Type == operationType.Value);
            }
            if (operationStatus.HasValue)
            {
                predicate = predicate.And(oh => oh.Status == operationStatus.Value);
            }
            if (!string.IsNullOrEmpty(createdBy))
            {
                predicate = predicate.And(oh => oh.CreatedBy == createdBy);
            }

            var filteredHistory = await _operationHistoryRepository.FindAsync(predicate);

            var pagedResult = filteredHistory
                .OrderByDescending(oh => oh.StartedAt) // Sortowanie przed paginacją
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return pagedResult;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<OperationHistory>> GetActiveOperationsAsync()
        {
            _logger.LogInformation("Pobieranie aktywnych operacji");
            
            Expression<Func<OperationHistory, bool>> predicate = oh => 
                oh.Status == OperationStatus.InProgress || 
                oh.Status == OperationStatus.Pending;
            
            var activeOperations = await _operationHistoryRepository.FindAsync(predicate);
            
            _logger.LogInformation("Znaleziono {Count} aktywnych operacji", activeOperations.Count());
            return activeOperations.OrderByDescending(oh => oh.StartedAt);
        }
    }

    // Klasa pomocnicza do budowania predykatów (często używana)
    internal static class PredicateBuilder // Zmieniono na internal, aby nie kolidować z ewentualną definicją w innym miejscu
    {
        public static Expression<Func<T, bool>> True<T>() { return f => true; }
        public static Expression<Func<T, bool>> False<T>() { return f => false; }

        public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expr1,
                                                            Expression<Func<T, bool>> expr2)
        {
            var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>());
            return Expression.Lambda<Func<T, bool>>
                  (Expression.OrElse(expr1.Body, invokedExpr), expr1.Parameters);
        }

        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> expr1,
                                                             Expression<Func<T, bool>> expr2)
        {
            var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>());
            return Expression.Lambda<Func<T, bool>>
                  (Expression.AndAlso(expr1.Body, invokedExpr), expr1.Parameters);
        }
    }
}