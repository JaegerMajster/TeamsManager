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
            // Przygotowanie
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(operation);
            await SaveChangesAsync();

            // Weryfikacja
            var savedOperation = await Context.OperationHistories.FirstOrDefaultAsync(oh => oh.Id == operation.Id);
            savedOperation.Should().NotBeNull();
            savedOperation!.Type.Should().Be(OperationType.TeamCreated);
            savedOperation.TargetEntityType.Should().Be("Team");
            savedOperation.Status.Should().Be(OperationStatus.Completed);
            savedOperation.CreatedBy.Should().Be("test_user");
            savedOperation.CreatedDate.Should().NotBe(default(DateTime));
            savedOperation.ModifiedBy.Should().BeNull();
            savedOperation.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectOperation()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var operation = CreateOperation( // Metoda pomocnicza CreateOperation również zostanie zmodyfikowana
                OperationType.UserCreated,
                "User",
                Guid.NewGuid().ToString(),
                "John Doe",
                OperationStatus.Completed
            );
            await Context.OperationHistories.AddAsync(operation);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetByIdAsync(operation.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Type.Should().Be(OperationType.UserCreated);
            result.TargetEntityName.Should().Be("John Doe");
            result.CreatedBy.Should().Be("test_user"); // Oczekujemy użytkownika z TestDbContext
        }

        [Fact]
        public async Task GetHistoryForEntityAsync_ShouldReturnCorrectOperations()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var entityType = "Team";
            var entityId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            var operations = new List<OperationHistory>
            {
                CreateOperation(OperationType.TeamCreated, entityType, entityId, "Team A", OperationStatus.Completed, now.AddHours(-5)),
                CreateOperation(OperationType.TeamUpdated, entityType, entityId, "Team A", OperationStatus.Completed, now.AddHours(-3)),
                CreateOperation(OperationType.MemberAdded, entityType, entityId, "Team A", OperationStatus.Completed, now.AddHours(-2)),
                CreateOperation(OperationType.MemberRemoved, entityType, entityId, "Team A", OperationStatus.Failed, now.AddHours(-1)),
                CreateOperation(OperationType.TeamCreated, entityType, Guid.NewGuid().ToString(), "Team B", OperationStatus.Completed, now), // inny entity ID
                CreateOperation(OperationType.UserCreated, "User", entityId, "User X", OperationStatus.Completed, now) // inny entity type
            };

            await Context.OperationHistories.AddRangeAsync(operations);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie - bez limitu
            var resultAll = await _repository.GetHistoryForEntityAsync(entityType, entityId);

            // Weryfikacja
            resultAll.Should().HaveCount(4);
            resultAll.Should().OnlyContain(oh => oh.TargetEntityType == entityType && oh.TargetEntityId == entityId);
            resultAll.First().Type.Should().Be(OperationType.MemberRemoved); // najnowsza operacja
            resultAll.ToList().ForEach(op => op.CreatedBy.Should().Be("test_user"));


            // Działanie - z limitem
            var resultLimited = await _repository.GetHistoryForEntityAsync(entityType, entityId, 2);

            // Weryfikacja
            resultLimited.Should().HaveCount(2);
            resultLimited.First().Type.Should().Be(OperationType.MemberRemoved);
            resultLimited.Last().Type.Should().Be(OperationType.MemberAdded);
        }

        [Fact]
        public async Task GetHistoryByUserAsync_ShouldReturnCorrectOperations()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var userUpnForTest = "test_user_for_history"; // Użyjemy tego użytkownika do stworzenia operacji
            SetTestUser(userUpnForTest); // Ustawiamy użytkownika, który będzie zapisany jako CreatedBy

            var now = DateTime.UtcNow;

            var operations = new List<OperationHistory>
            {
                // Te operacje będą miały CreatedBy = userUpnForTest
                CreateOperation(OperationType.TeamCreated, "Team", "1", "Team 1", OperationStatus.Completed, now.AddDays(-3)),
                CreateOperation(OperationType.UserCreated, "User", "1", "User 1", OperationStatus.Completed, now.AddDays(-2)),
                CreateOperation(OperationType.TeamUpdated, "Team", "2", "Team 2", OperationStatus.Failed, now.AddDays(-1)),
            };
            await Context.OperationHistories.AddRangeAsync(operations);
            await SaveChangesAsync(); // Zapisze operacje z CreatedBy = userUpnForTest

            // Tworzymy operacje dla innego użytkownika, aby przetestować filtrowanie
            SetTestUser("another_user");
            var otherUserOperation = CreateOperation(OperationType.TeamCreated, "Team", "3", "Team 3", OperationStatus.Completed, now);
            await Context.OperationHistories.AddAsync(otherUserOperation);
            await SaveChangesAsync(); // Zapisze z CreatedBy = "another_user"
            ResetTestUser(); // Przywracamy domyślnego użytkownika testowego

            // Działanie - bez limitu, szukamy operacji stworzonych przez userUpnForTest
            var resultAll = await _repository.GetHistoryByUserAsync(userUpnForTest);

            // Weryfikacja
            resultAll.Should().HaveCount(3); // Powinny być 3 operacje dla userUpnForTest
            resultAll.Should().OnlyContain(oh => oh.CreatedBy == userUpnForTest);
            resultAll.OrderByDescending(oh => oh.StartedAt).First().Type.Should().Be(OperationType.TeamUpdated);


            // Działanie - z limitem
            var resultLimited = await _repository.GetHistoryByUserAsync(userUpnForTest, 2);

            // Weryfikacja
            resultLimited.Should().HaveCount(2);
            resultLimited.All(oh => oh.CreatedBy == userUpnForTest).Should().BeTrue();
        }

        [Theory]
        [InlineData(null, null, 6)]
        [InlineData(OperationType.TeamCreated, null, 2)]
        [InlineData(null, OperationStatus.Failed, 3)]
        [InlineData(OperationType.UserCreated, OperationStatus.Completed, 1)]
        [InlineData(OperationType.TeamUpdated, OperationStatus.Failed, 1)]
        public async Task GetHistoryByDateRangeAsync_ShouldReturnCorrectOperations(
            OperationType? filterType,
            OperationStatus? filterStatus,
            int expectedCount)
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);

            var operations = new List<OperationHistory>
            {
                CreateOperation(OperationType.TeamCreated, "Team", "1", "Team 1", OperationStatus.Completed, new DateTime(2024, 2, 1)),
                CreateOperation(OperationType.TeamCreated, "Team", "2", "Team 2", OperationStatus.Failed, new DateTime(2024, 3, 1)),
                CreateOperation(OperationType.UserCreated, "User", "1", "User 1", OperationStatus.Completed, new DateTime(2024, 4, 1)),
                CreateOperation(OperationType.UserCreated, "User", "2", "User 2", OperationStatus.Failed, new DateTime(2024, 5, 1)),
                CreateOperation(OperationType.TeamUpdated, "Team", "1", "Team 1", OperationStatus.Completed, new DateTime(2024, 6, 1)),
                CreateOperation(OperationType.TeamUpdated, "Team", "2", "Team 2", OperationStatus.Failed, new DateTime(2024, 7, 1)),
                CreateOperation(OperationType.TeamCreated, "Team", "3", "Team 3", OperationStatus.Completed, new DateTime(2023, 12, 31)),
                CreateOperation(OperationType.UserCreated, "User", "3", "User 3", OperationStatus.Completed, new DateTime(2025, 1, 1)),
            };

            await Context.OperationHistories.AddRangeAsync(operations);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetHistoryByDateRangeAsync(startDate, endDate, filterType, filterStatus);

            // Weryfikacja
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

            if (result.Count() > 1)
            {
                result.Should().BeInDescendingOrder(oh => oh.StartedAt);
            }
        }

        [Fact]
        public async Task Update_ShouldModifyOperationData()
        {
            // Przygotowanie
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.OperationHistories.AddAsync(operation);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = operation.CreatedBy;
            var initialCreatedDate = operation.CreatedDate;
            var currentUser = "operation_modifier";
            SetTestUser(currentUser);

            // Działanie
            // Pobieramy encję ponownie, aby upewnić się, że działamy na śledzonej przez kontekst encji
            var operationToUpdate = await _repository.GetByIdAsync(operation.Id);
            operationToUpdate!.Status = OperationStatus.Completed;
            operationToUpdate.CompletedAt = DateTime.UtcNow;
            operationToUpdate.Duration = operationToUpdate.CompletedAt.Value - operationToUpdate.StartedAt;
            operationToUpdate.ProcessedItems = 10;
            operationToUpdate.FailedItems = 0;
            operationToUpdate.ErrorMessage = null;
            // operationToUpdate.MarkAsModified(currentUser); // Niepotrzebne, TestDbContext to obsłuży

            _repository.Update(operationToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy na `currentUser`

            // Weryfikacja
            var updatedOperation = await Context.OperationHistories.FirstOrDefaultAsync(oh => oh.Id == operation.Id);
            updatedOperation.Should().NotBeNull();
            updatedOperation!.Status.Should().Be(OperationStatus.Completed);
            updatedOperation.CompletedAt.Should().NotBeNull();
            updatedOperation.Duration.Should().NotBeNull();
            updatedOperation.ProcessedItems.Should().Be(10);
            updatedOperation.FailedItems.Should().Be(0);
            updatedOperation.CreatedBy.Should().Be(initialCreatedBy);
            updatedOperation.CreatedDate.Should().Be(initialCreatedDate);
            updatedOperation.ModifiedBy.Should().Be(currentUser);
            updatedOperation.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldMarkOperationAsInactive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var operation = CreateOperation(
                OperationType.TeamDeleted,
                "Team",
                Guid.NewGuid().ToString(),
                "Team to Delete",
                OperationStatus.Completed
            );
            await Context.OperationHistories.AddAsync(operation);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = operation.CreatedBy;
            var initialCreatedDate = operation.CreatedDate;
            var currentUser = "operation_deleter";
            SetTestUser(currentUser);


            // Działanie
            var operationToUpdate = await _repository.GetByIdAsync(operation.Id);
            operationToUpdate!.MarkAsDeleted(currentUser); // MarkAsDeleted ustawi IsActive i wywoła MarkAsModified
            _repository.Update(operationToUpdate); // Oznacza stan jako Modified
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy

            // Weryfikacja
            var deletedOperation = await Context.OperationHistories.AsNoTracking().FirstOrDefaultAsync(oh => oh.Id == operation.Id);
            deletedOperation.Should().NotBeNull();
            deletedOperation!.IsActive.Should().BeFalse();
            deletedOperation.CreatedBy.Should().Be(initialCreatedBy);
            deletedOperation.CreatedDate.Should().Be(initialCreatedDate);
            deletedOperation.ModifiedBy.Should().Be(currentUser);
            deletedOperation.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        #region Helper Methods

        // Zmodyfikowana metoda pomocnicza - usunięto parametr createdBy
        private OperationHistory CreateOperation(
            OperationType type,
            string targetEntityType,
            string targetEntityId,
            string targetEntityName,
            OperationStatus status,
            DateTime? startedAt = null)
        {
            var started = startedAt ?? DateTime.UtcNow;
            var completed = (status == OperationStatus.Completed || status == OperationStatus.Failed)
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
        }

        #endregion
    }
}