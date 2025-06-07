using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Services.Dashboard
{
    /// <summary>
    /// Uproszczona implementacja IOperationHistoryService tylko dla potrzeb Dashboard
    /// </summary>
    public class SimpleDashboardOperationHistoryService : IOperationHistoryService
    {
        private readonly List<OperationHistory> _mockOperations;

        public SimpleDashboardOperationHistoryService()
        {
            _mockOperations = new List<OperationHistory>
            {
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.TeamCreated,
                    TargetEntityType = "Team",
                    TargetEntityId = "1",
                    TargetEntityName = "Team Alpha",
                    Status = OperationStatus.Completed,
                    StartedAt = DateTime.Now.AddMinutes(-15),
                    CreatedBy = "jan.kowalski@contoso.com",
                    CompletedAt = DateTime.Now.AddMinutes(-14),
                    OperationDetails = "Utworzono zespół 'Team Alpha'"
                },
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.UserCreated,
                    TargetEntityType = "User",
                    TargetEntityId = "2",
                    TargetEntityName = "Anna Nowak",
                    Status = OperationStatus.Completed,
                    StartedAt = DateTime.Now.AddMinutes(-32),
                    CreatedBy = "anna.nowak@contoso.com",
                    CompletedAt = DateTime.Now.AddMinutes(-30),
                    OperationDetails = "Dodano użytkownika 'anna.nowak@contoso.com'"
                },
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.TeamArchived,
                    TargetEntityType = "Team",
                    TargetEntityId = "3",
                    TargetEntityName = "Team Gamma",
                    Status = OperationStatus.Failed,
                    StartedAt = DateTime.Now.AddHours(-1),
                    CreatedBy = "piotr.wisniewski@contoso.com",
                    CompletedAt = DateTime.Now.AddHours(-1).AddMinutes(2),
                    OperationDetails = "Próba archiwizacji zespołu 'Team Gamma'",
                    ErrorMessage = "Brak uprawnień do archiwizacji"
                },
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.GenericOperation,
                    TargetEntityType = "Report",
                    TargetEntityId = "monthly-2024-06",
                    TargetEntityName = "Raport miesięczny",
                    Status = OperationStatus.Completed,
                    StartedAt = DateTime.Now.AddHours(-2),
                    CreatedBy = "maria.kowalczyk@contoso.com",
                    CompletedAt = DateTime.Now.AddHours(-2).AddMinutes(5),
                    OperationDetails = "Wygenerowano raport miesięczny"
                },
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.BulkUserImport,
                    TargetEntityType = "User",
                    TargetEntityId = "bulk-import-001",
                    TargetEntityName = "Import CSV",
                    Status = OperationStatus.Completed,
                    StartedAt = DateTime.Now.AddHours(-3),
                    CreatedBy = "tomasz.lewandowski@contoso.com",
                    CompletedAt = DateTime.Now.AddHours(-3).AddMinutes(10),
                    OperationDetails = "Zaimportowano 25 użytkowników z pliku CSV",
                    ProcessedItems = 25,
                    FailedItems = 0,
                    TotalItems = 25
                },
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.TeamSynchronized,
                    TargetEntityType = "System",
                    TargetEntityId = "sync-teams",
                    TargetEntityName = "Synchronizacja Teams",
                    Status = OperationStatus.Completed,
                    StartedAt = DateTime.Now.AddHours(-6),
                    CreatedBy = "system@contoso.com",
                    CompletedAt = DateTime.Now.AddHours(-6).AddMinutes(15),
                    OperationDetails = "Synchronizacja z Microsoft Teams"
                }
            };
        }

        public Task<IEnumerable<OperationHistory>> GetHistoryByFilterAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            OperationType? operationType = null,
            OperationStatus? operationStatus = null,
            string? createdBy = null,
            int page = 1,
            int pageSize = 20)
        {
            var filtered = _mockOperations.AsQueryable();

            if (startDate.HasValue)
                filtered = filtered.Where(o => o.StartedAt >= startDate.Value);

            if (endDate.HasValue)
                filtered = filtered.Where(o => o.StartedAt <= endDate.Value);

            if (operationType.HasValue)
                filtered = filtered.Where(o => o.Type == operationType.Value);

            if (operationStatus.HasValue)
                filtered = filtered.Where(o => o.Status == operationStatus.Value);

            if (!string.IsNullOrEmpty(createdBy))
                filtered = filtered.Where(o => o.CreatedBy != null && o.CreatedBy.Contains(createdBy, StringComparison.OrdinalIgnoreCase));

            var results = filtered
                .OrderByDescending(o => o.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<IEnumerable<OperationHistory>>(results);
        }

        // Pozostałe metody - implementacje zaślepkowe
        public Task<OperationHistory> CreateNewOperationEntryAsync(
            OperationType type,
            string targetEntityType,
            string? targetEntityId = null,
            string? targetEntityName = null,
            string? details = null,
            string? parentOperationId = null)
        {
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId ?? string.Empty,
                TargetEntityName = targetEntityName ?? string.Empty,
                Status = OperationStatus.InProgress,
                StartedAt = DateTime.Now,
                CreatedBy = "current.user@contoso.com",
                OperationDetails = details ?? string.Empty,
                ParentOperationId = parentOperationId
            };

            _mockOperations.Add(operation);
            return Task.FromResult(operation);
        }

        public Task<bool> UpdateOperationStatusAsync(
            string operationId,
            OperationStatus newStatus,
            string? message = null,
            string? stackTrace = null)
        {
            var operation = _mockOperations.FirstOrDefault(o => o.Id == operationId);
            if (operation != null)
            {
                operation.Status = newStatus;
                if (newStatus == OperationStatus.Completed || newStatus == OperationStatus.Failed)
                {
                    operation.CompletedAt = DateTime.Now;
                }
                if (!string.IsNullOrEmpty(message))
                {
                    if (newStatus == OperationStatus.Failed)
                        operation.ErrorMessage = message;
                    else
                        operation.OperationDetails = message;
                }
                if (!string.IsNullOrEmpty(stackTrace))
                    operation.ErrorStackTrace = stackTrace;
                
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> UpdateOperationProgressAsync(
            string operationId,
            int processedItems,
            int failedItems,
            int? totalItems = null)
        {
            var operation = _mockOperations.FirstOrDefault(o => o.Id == operationId);
            if (operation != null)
            {
                operation.ProcessedItems = processedItems;
                operation.FailedItems = failedItems;
                if (totalItems.HasValue)
                    operation.TotalItems = totalItems.Value;
                
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<OperationHistory?> GetOperationByIdAsync(string operationId)
        {
            var operation = _mockOperations.FirstOrDefault(o => o.Id == operationId);
            return Task.FromResult(operation);
        }

        public Task<IEnumerable<OperationHistory>> GetHistoryForEntityAsync(string targetEntityType, string targetEntityId, int? count = null)
        {
            var filtered = _mockOperations
                .Where(o => o.TargetEntityType == targetEntityType && o.TargetEntityId == targetEntityId)
                .OrderByDescending(o => o.StartedAt);

            if (count.HasValue)
                filtered = filtered.Take(count.Value).OrderByDescending(o => o.StartedAt);

            return Task.FromResult<IEnumerable<OperationHistory>>(filtered.ToList());
        }

        public Task<IEnumerable<OperationHistory>> GetHistoryByUserAsync(string userUpn, int? count = null)
        {
            var filtered = _mockOperations
                .Where(o => o.CreatedBy != null && o.CreatedBy.Equals(userUpn, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.StartedAt);

            if (count.HasValue)
                filtered = filtered.Take(count.Value).OrderByDescending(o => o.StartedAt);

            return Task.FromResult<IEnumerable<OperationHistory>>(filtered.ToList());
        }

        public Task<IEnumerable<OperationHistory>> GetActiveOperationsAsync()
        {
            var activeOperations = _mockOperations
                .Where(o => o.Status == OperationStatus.InProgress || o.Status == OperationStatus.Pending)
                .OrderByDescending(o => o.StartedAt);

            return Task.FromResult<IEnumerable<OperationHistory>>(activeOperations.ToList());
        }
    }
} 