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
    public class OperationHistoryService : IOperationHistoryService
    {
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<OperationHistoryService> _logger;

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
                IsActive = true
            };

            if (status == OperationStatus.Pending || status == OperationStatus.InProgress)
            {
                operation.MarkAsStarted();
                if (status == OperationStatus.Pending && operation.Status == OperationStatus.InProgress)
                {
                    operation.Status = OperationStatus.Pending;
                }
            }
            else if (status == OperationStatus.Completed || status == OperationStatus.Failed || status == OperationStatus.Cancelled || status == OperationStatus.PartialSuccess)
            {
                if (operation.StartedAt == default(DateTime))
                {
                    operation.StartedAt = DateTime.UtcNow.AddMilliseconds(-10);
                }
                operation.CompletedAt = DateTime.UtcNow;
                if (operation.StartedAt != default(DateTime))
                {
                    operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
                }
            }

            await SaveOperationHistoryAsync(operation);

            _logger.LogInformation("Operacja ID: {OperationId} została zalogowana.", operation.Id);
            return operation;
        }

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
                else if (newStatus == OperationStatus.Completed || newStatus == OperationStatus.PartialSuccess)
                {
                    operation.OperationDetails = string.IsNullOrWhiteSpace(operation.OperationDetails)
                                                 ? message
                                                 : $"{operation.OperationDetails}{Environment.NewLine}Status Update: {message}";
                }
                else if (newStatus != OperationStatus.Failed)
                {
                    operation.OperationDetails = string.IsNullOrWhiteSpace(operation.OperationDetails)
                                                ? message
                                                : $"{operation.OperationDetails}{Environment.NewLine}Status Update: {message}";
                }
            }

            if (newStatus == OperationStatus.Failed && !string.IsNullOrWhiteSpace(stackTrace))
            {
                operation.ErrorStackTrace = stackTrace;
            }

            if ((newStatus == OperationStatus.Completed || newStatus == OperationStatus.Failed || newStatus == OperationStatus.Cancelled || newStatus == OperationStatus.PartialSuccess)
                && !operation.CompletedAt.HasValue)
            {
                operation.CompletedAt = DateTime.UtcNow;
                if (operation.StartedAt != default(DateTime))
                {
                    operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
                }
            }

            operation.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
            await SaveOperationHistoryAsync(operation);

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

            if (totalItems.HasValue && operation.TotalItems != totalItems.Value)
            {
                operation.TotalItems = totalItems.Value;
            }

            operation.UpdateProgress(processedItems, failedItems);
            operation.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_progress_update");
            await SaveOperationHistoryAsync(operation);

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

            // Budowanie predykatu dynamicznie
            Expression<Func<OperationHistory, bool>> predicate = PredicateBuilder.True<OperationHistory>();

            if (startDate.HasValue)
            {
                predicate = predicate.And(oh => oh.StartedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                // Aby uwzględnić cały dzień endDate, bierzemy początek następnego dnia
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

            // Pobranie przefiltrowanych danych z repozytorium
            // Zakładamy, że FindAsync wykonuje filtrowanie po stronie bazy danych
            var filteredHistory = await _operationHistoryRepository.FindAsync(predicate);

            // Sortowanie i paginacja w pamięci na przefiltrowanych wynikach
            // Dla optymalizacji, sortowanie i paginacja powinny być idealnie wykonywane przez bazę danych,
            // co wymagałoby bardziej zaawansowanego interfejsu repozytorium lub użycia IQueryable.
            var pagedResult = filteredHistory
                .OrderByDescending(oh => oh.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return pagedResult;
        }

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null)
            {
                if (operation.StartedAt == default(DateTime) && (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending))
                {
                    operation.StartedAt = DateTime.UtcNow;
                }
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
                existingLog.Type = operation.Type;
                existingLog.ProcessedItems = operation.ProcessedItems;
                existingLog.FailedItems = operation.FailedItems;
                existingLog.ParentOperationId = operation.ParentOperationId;
                existingLog.SequenceNumber = operation.SequenceNumber;
                existingLog.TotalItems = operation.TotalItems;
                existingLog.UserIpAddress = operation.UserIpAddress;
                existingLog.UserAgent = operation.UserAgent;
                existingLog.SessionId = operation.SessionId;
                existingLog.Tags = operation.Tags;
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
        }
    }

    // Klasa pomocnicza do budowania predykatów (często używana)
    public static class PredicateBuilder
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
