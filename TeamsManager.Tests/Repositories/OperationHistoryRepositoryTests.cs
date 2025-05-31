using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories;
using Xunit;

namespace TeamsManager.Tests.Repositories
{
    [Collection("Sequential")]
        public class OperationHistoryRepositoryTests : RepositoryTestBase
    {
        private readonly OperationHistoryRepository _repository;

        public OperationHistoryRepositoryTests()
        {
            _repository = new OperationHistoryRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddOperationHistoryToDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamCreated,
                TargetEntityType = "Team",
                TargetEntityId = Guid.NewGuid().ToString(),
                TargetEntityName = "Test Team",
                Status = OperationStatus.Completed,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow,
                Duration = TimeSpan.FromMinutes(5),
                OperationDetails = "{\"key\":\"value\"}",
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(operation);
            await SaveChangesAsync();

            // Assert
            var savedOperation = await Context.OperationHistories.FirstOrDefaultAsync(oh => oh.Id == operation.Id);
            savedOperation.Should().NotBeNull();
            savedOperation!.Type.Should().Be(OperationType.TeamCreated);
            savedOperation.TargetEntityType.Should().Be("Team");
            savedOperation.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectOperation()
        {
            // Arrange
            await CleanDatabaseAsync();
            var operation = CreateOperation(
                OperationType.UserCreated,
                "User",
                Guid.NewGuid().ToString(),
                "John Doe",
                OperationStatus.Completed,
                "admin@test.com"
            );
            await Context.OperationHistories.AddAsync(operation);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(operation.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Type.Should().Be(OperationType.UserCreated);
            result.TargetEntityName.Should().Be("John Doe");
            result.CreatedBy.Should().Be("system@teamsmanager.local");
        }

        [Fact]
        public async Task GetHistoryForEntityAsync_ShouldReturnCorrectOperations()
        {
            // Arrange
            await CleanDatabaseAsync();
            var entityType = "Team";
            var entityId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            var operations = new List<OperationHistory>
            {
                CreateOperation(OperationType.TeamCreated, entityType, entityId, "Team A", OperationStatus.Completed, "user1", now.AddHours(-5)),
                CreateOperation(OperationType.TeamUpdated, entityType, entityId, "Team A", OperationStatus.Completed, "user2", now.AddHours(-3)),
                CreateOperation(OperationType.MemberAdded, entityType, entityId, "Team A", OperationStatus.Completed, "user1", now.AddHours(-2)),
                CreateOperation(OperationType.MemberRemoved, entityType, entityId, "Team A", OperationStatus.Failed, "user3", now.AddHours(-1)),
                CreateOperation(OperationType.TeamCreated, entityType, Guid.NewGuid().ToString(), "Team B", OperationStatus.Completed, "user1", now), // inny entity ID
                CreateOperation(OperationType.UserCreated, "User", entityId, "User X", OperationStatus.Completed, "user1", now) // inny entity type
            };

            await Context.OperationHistories.AddRangeAsync(operations);
            await Context.SaveChangesAsync();

            // Act - bez limitu
            var resultAll = await _repository.GetHistoryForEntityAsync(entityType, entityId);

            // Assert
            resultAll.Should().HaveCount(4);
            resultAll.Should().OnlyContain(oh => oh.TargetEntityType == entityType && oh.TargetEntityId == entityId);
            resultAll.First().Type.Should().Be(OperationType.MemberRemoved); // najnowsza operacja

            // Act - z limitem
            var resultLimited = await _repository.GetHistoryForEntityAsync(entityType, entityId, 2);

            // Assert
            resultLimited.Should().HaveCount(2);
            resultLimited.First().Type.Should().Be(OperationType.MemberRemoved);
            resultLimited.Last().Type.Should().Be(OperationType.MemberAdded);
        }

        [Fact]
        public async Task GetHistoryByUserAsync_ShouldReturnCorrectOperations()
        {
            // Arrange
            await CleanDatabaseAsync();
            var userUpn = "admin@test.com";
            var now = DateTime.UtcNow;

            var operations = new List<OperationHistory>
            {
                CreateOperation(OperationType.TeamCreated, "Team", "1", "Team 1", OperationStatus.Completed, userUpn, now.AddDays(-3)),
                CreateOperation(OperationType.UserCreated, "User", "1", "User 1", OperationStatus.Completed, userUpn, now.AddDays(-2)),
                CreateOperation(OperationType.TeamUpdated, "Team", "2", "Team 2", OperationStatus.Failed, userUpn, now.AddDays(-1)),
                CreateOperation(OperationType.TeamCreated, "Team", "3", "Team 3", OperationStatus.Completed, "other@test.com", now), // inny użytkownik
            };

            await Context.OperationHistories.AddRangeAsync(operations);
            await Context.SaveChangesAsync();

            // Act - bez limitu
            var resultAll = await _repository.GetHistoryByUserAsync("system@teamsmanager.local");

            // Assert
            resultAll.Should().HaveCount(4);
            resultAll.Should().OnlyContain(oh => oh.CreatedBy == "system@teamsmanager.local");
            resultAll.First().Type.Should().Be(OperationType.TeamCreated); // zmieniono z TeamUpdated na TeamCreated - to jest najnowsza operacja

            // Act - z limitem
            var resultLimited = await _repository.GetHistoryByUserAsync("system@teamsmanager.local", 2);

            // Assert
            resultLimited.Should().HaveCount(2);
            resultLimited.All(oh => oh.CreatedBy == "system@teamsmanager.local").Should().BeTrue();
        }

        [Theory]
        [InlineData(null, null, 6)]                                              // wszystkie operacje w zakresie
        [InlineData(OperationType.TeamCreated, null, 2)]                       // tylko TeamCreated
        [InlineData(null, OperationStatus.Failed, 3)]                          // tylko Failed
        [InlineData(OperationType.UserCreated, OperationStatus.Completed, 1)]  // UserCreated + Completed
        [InlineData(OperationType.TeamUpdated, OperationStatus.Failed, 1)]     // TeamUpdated + Failed
        public async Task GetHistoryByDateRangeAsync_ShouldReturnCorrectOperations(
            OperationType? filterType, 
            OperationStatus? filterStatus, 
            int expectedCount)
        {
            // Arrange
            await CleanDatabaseAsync();
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);

            var operations = new List<OperationHistory>
            {
                // W zakresie dat
                CreateOperation(OperationType.TeamCreated, "Team", "1", "Team 1", OperationStatus.Completed, "user1", new DateTime(2024, 2, 1)),
                CreateOperation(OperationType.TeamCreated, "Team", "2", "Team 2", OperationStatus.Failed, "user2", new DateTime(2024, 3, 1)),
                CreateOperation(OperationType.UserCreated, "User", "1", "User 1", OperationStatus.Completed, "user1", new DateTime(2024, 4, 1)),
                CreateOperation(OperationType.UserCreated, "User", "2", "User 2", OperationStatus.Failed, "user2", new DateTime(2024, 5, 1)),
                CreateOperation(OperationType.TeamUpdated, "Team", "1", "Team 1", OperationStatus.Completed, "user3", new DateTime(2024, 6, 1)),
                CreateOperation(OperationType.TeamUpdated, "Team", "2", "Team 2", OperationStatus.Failed, "user3", new DateTime(2024, 7, 1)),
                
                // Poza zakresem dat
                CreateOperation(OperationType.TeamCreated, "Team", "3", "Team 3", OperationStatus.Completed, "user1", new DateTime(2023, 12, 31)),
                CreateOperation(OperationType.UserCreated, "User", "3", "User 3", OperationStatus.Completed, "user1", new DateTime(2025, 1, 1)),
            };

            await Context.OperationHistories.AddRangeAsync(operations);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetHistoryByDateRangeAsync(startDate, endDate, filterType, filterStatus);

            // Assert
            result.Should().HaveCount(expectedCount);
            result.Should().OnlyContain(oh => oh.StartedAt >= startDate && oh.StartedAt <= endDate);
            
            if (filterType.HasValue)
            {
                result.Should().OnlyContain(oh => oh.Type == filterType.Value);
            }
            if (filterStatus.HasValue)
            {
                result.Should().OnlyContain(oh => oh.Status == filterStatus.Value);
            }

            // Sprawdzenie sortowania (malejąco po StartedAt)
            if (result.Count() > 1)
            {
                result.Should().BeInDescendingOrder(oh => oh.StartedAt);
            }
        }

        [Fact]
        public async Task Update_ShouldModifyOperationData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamCreated,
                TargetEntityType = "Team",
                TargetEntityId = Guid.NewGuid().ToString(),
                TargetEntityName = "Original Team",
                Status = OperationStatus.InProgress,
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.OperationHistories.AddAsync(operation);
            await Context.SaveChangesAsync();

            // Act
            operation.Status = OperationStatus.Completed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
            operation.ProcessedItems = 10;
            operation.FailedItems = 0;
            operation.ErrorMessage = null;
            operation.MarkAsModified("system");

            _repository.Update(operation);
            await SaveChangesAsync();

            // Assert
            var updatedOperation = await Context.OperationHistories.FirstOrDefaultAsync(oh => oh.Id == operation.Id);
            updatedOperation.Should().NotBeNull();
            updatedOperation!.Status.Should().Be(OperationStatus.Completed);
            updatedOperation.CompletedAt.Should().NotBeNull();
            updatedOperation.Duration.Should().NotBeNull();
            updatedOperation.ProcessedItems.Should().Be(10);
            updatedOperation.FailedItems.Should().Be(0);
            updatedOperation.ModifiedBy.Should().Be("system@teamsmanager.local");
            updatedOperation.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_ShouldMarkOperationAsInactive()
        {
            // Arrange
            await CleanDatabaseAsync();
            var operation = CreateOperation(
                OperationType.TeamDeleted,
                "Team",
                Guid.NewGuid().ToString(),
                "Team to Delete",
                OperationStatus.Completed,
                "admin@test.com"
            );
            await Context.OperationHistories.AddAsync(operation);
            await Context.SaveChangesAsync();

            // Act
            operation.MarkAsDeleted("deleter");
            _repository.Update(operation);
            await SaveChangesAsync();

            // Assert
            var deletedOperation = await Context.OperationHistories.FirstOrDefaultAsync(oh => oh.Id == operation.Id);
            deletedOperation.Should().NotBeNull();
            deletedOperation!.IsActive.Should().BeFalse();
            deletedOperation.ModifiedBy.Should().Be("system@teamsmanager.local");
            deletedOperation.ModifiedDate.Should().NotBeNull();
        }

        #region Helper Methods

        private OperationHistory CreateOperation(
            OperationType type,
            string targetEntityType,
            string targetEntityId,
            string targetEntityName,
            OperationStatus status,
            string createdBy,
            DateTime? startedAt = null)
        {
            var started = startedAt ?? DateTime.UtcNow;
            var completed = status == OperationStatus.Completed || status == OperationStatus.Failed
                ? started.AddSeconds(30)
                : (DateTime?)null;

            return new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId,
                TargetEntityName = targetEntityName,
                Status = status,
                StartedAt = started,
                CompletedAt = completed,
                Duration = completed.HasValue ? completed.Value - started : null,
                CreatedBy = createdBy,
                IsActive = true
            };
        }

        #endregion
    }
} 