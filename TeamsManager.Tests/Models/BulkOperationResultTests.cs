using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla modeli BulkOperationResult, BulkOperationSuccess i BulkOperationError
    /// Pokrycie: konstruktory, właściwości, metody statyczne, implicit operator
    /// </summary>
    public class BulkOperationResultTests
    {
        #region BulkOperationResult Constructor Tests

        [Fact]
        public void BulkOperationResult_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var result = new BulkOperationResult();

            // Assert - podstawowe właściwości
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().BeNull();
            result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.AdditionalData.Should().BeNull();
            result.OperationType.Should().BeNull();
            result.ExecutionTimeMs.Should().BeNull();
            result.GraphEndpoint.Should().BeNull();
            result.HttpMethod.Should().BeNull();

            // Assert - kolekcje
            result.SuccessfulOperations.Should().NotBeNull().And.BeEmpty();
            result.Errors.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void BulkOperationResult_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var result = new BulkOperationResult();
            var timestamp = DateTime.UtcNow.AddMinutes(-5);
            var additionalData = new Dictionary<string, object> { { "key1", "value1" } };

            // Act
            result.Success = true;
            result.IsSuccess = true;
            result.ErrorMessage = "Test error";
            result.ProcessedAt = timestamp;
            result.AdditionalData = additionalData;
            result.OperationType = "CREATE_USER";
            result.ExecutionTimeMs = 150;
            result.GraphEndpoint = "/v1.0/users";
            result.HttpMethod = "POST";

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().Be("Test error");
            result.ProcessedAt.Should().Be(timestamp);
            result.AdditionalData.Should().BeSameAs(additionalData);
            result.OperationType.Should().Be("CREATE_USER");
            result.ExecutionTimeMs.Should().Be(150);
            result.GraphEndpoint.Should().Be("/v1.0/users");
            result.HttpMethod.Should().Be("POST");
        }

        #endregion

        #region BulkOperationResult Static Methods Tests

        [Fact]
        public void BulkOperationResult_CreateSuccess_ShouldReturnSuccessResult()
        {
            // Act
            var result = BulkOperationResult.CreateSuccess();

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.OperationType.Should().BeNull();
            result.ExecutionTimeMs.Should().BeNull();
            result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void BulkOperationResult_CreateSuccess_WithParameters_ShouldReturnSuccessResultWithData()
        {
            // Arrange
            var operationType = "CREATE_TEAM";
            var executionTime = 250L;

            // Act
            var result = BulkOperationResult.CreateSuccess(operationType, executionTime);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be(operationType);
            result.ExecutionTimeMs.Should().Be(executionTime);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void BulkOperationResult_CreateError_ShouldReturnErrorResult()
        {
            // Arrange
            var errorMessage = "Operation failed";

            // Act
            var result = BulkOperationResult.CreateError(errorMessage);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be(errorMessage);
            result.OperationType.Should().BeNull();
            result.ExecutionTimeMs.Should().BeNull();
        }

        [Fact]
        public void BulkOperationResult_CreateError_WithParameters_ShouldReturnErrorResultWithData()
        {
            // Arrange
            var errorMessage = "Graph API error";
            var operationType = "DELETE_USER";
            var executionTime = 500L;

            // Act
            var result = BulkOperationResult.CreateError(errorMessage, operationType, executionTime);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be(errorMessage);
            result.OperationType.Should().Be(operationType);
            result.ExecutionTimeMs.Should().Be(executionTime);
        }

        #endregion

        #region BulkOperationResult Implicit Operator Tests

        [Fact]
        public void BulkOperationResult_ImplicitBoolConversion_ShouldWork()
        {
            var successResult = BulkOperationResult.CreateSuccess();
            var errorResult = BulkOperationResult.CreateError("Error");

            bool success = successResult;
            bool failure = errorResult;

            success.Should().BeTrue();
            failure.Should().BeFalse();
        }

        #endregion

        #region BulkOperationResult Collections Tests

        [Fact]
        public void BulkOperationResult_WhenAddingSuccessfulOperations_ShouldMaintainCollection()
        {
            // Arrange
            var result = new BulkOperationResult();
            var success1 = new BulkOperationSuccess 
            { 
                Operation = "CREATE", 
                EntityId = "user-1", 
                Message = "Created successfully" 
            };
            var success2 = new BulkOperationSuccess 
            { 
                Operation = "UPDATE", 
                EntityId = "user-2", 
                Message = "Updated successfully" 
            };

            // Act
            result.SuccessfulOperations.Add(success1);
            result.SuccessfulOperations.Add(success2);

            // Assert
            result.SuccessfulOperations.Should().HaveCount(2);
            result.SuccessfulOperations.Should().Contain(success1);
            result.SuccessfulOperations.Should().Contain(success2);
        }

        [Fact]
        public void BulkOperationResult_WhenAddingErrors_ShouldMaintainCollection()
        {
            // Arrange
            var result = new BulkOperationResult();
            var error1 = new BulkOperationError 
            { 
                Operation = "CREATE", 
                EntityId = "user-1", 
                Message = "Creation failed" 
            };
            var error2 = new BulkOperationError 
            { 
                Operation = "DELETE", 
                EntityId = "user-2", 
                Message = "Deletion failed" 
            };

            // Act
            result.Errors.Add(error1);
            result.Errors.Add(error2);

            // Assert
            result.Errors.Should().HaveCount(2);
            result.Errors.Should().Contain(error1);
            result.Errors.Should().Contain(error2);
        }

        #endregion

        #region BulkOperationSuccess Tests

        [Fact]
        public void BulkOperationSuccess_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var success = new BulkOperationSuccess();

            // Assert
            success.Operation.Should().Be(string.Empty);
            success.EntityId.Should().Be(string.Empty);
            success.EntityName.Should().BeNull();
            success.Message.Should().BeNull();
            success.AdditionalData.Should().BeNull();
        }

        [Fact]
        public void BulkOperationSuccess_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var success = new BulkOperationSuccess();
            var additionalData = new Dictionary<string, object> 
            { 
                { "userId", "12345" },
                { "teamId", "team-abc" }
            };

            // Act
            success.Operation = "CREATE_TEAM_MEMBER";
            success.EntityId = "member-123";
            success.EntityName = "John Doe";
            success.Message = "User added to team successfully";
            success.AdditionalData = additionalData;

            // Assert
            success.Operation.Should().Be("CREATE_TEAM_MEMBER");
            success.EntityId.Should().Be("member-123");
            success.EntityName.Should().Be("John Doe");
            success.Message.Should().Be("User added to team successfully");
            success.AdditionalData.Should().BeSameAs(additionalData);
            success.AdditionalData.Should().ContainKey("userId").WhoseValue.Should().Be("12345");
            success.AdditionalData.Should().ContainKey("teamId").WhoseValue.Should().Be("team-abc");
        }

        #endregion

        #region BulkOperationError Tests

        [Fact]
        public void BulkOperationError_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var error = new BulkOperationError();

            // Assert
            error.Operation.Should().Be(string.Empty);
            error.EntityId.Should().BeNull();
            error.EntityName.Should().BeNull();
            error.Message.Should().Be(string.Empty);
            error.Exception.Should().BeNull();
            error.AdditionalData.Should().BeNull();
        }

        [Fact]
        public void BulkOperationError_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var error = new BulkOperationError();
            var exception = new InvalidOperationException("Test exception");
            var additionalData = new Dictionary<string, object> 
            { 
                { "statusCode", 400 },
                { "graphError", "InvalidUser" }
            };

            // Act
            error.Operation = "DELETE_USER";
            error.EntityId = "user-456";
            error.EntityName = "Jane Smith";
            error.Message = "User deletion failed";
            error.Exception = exception;
            error.AdditionalData = additionalData;

            // Assert
            error.Operation.Should().Be("DELETE_USER");
            error.EntityId.Should().Be("user-456");
            error.EntityName.Should().Be("Jane Smith");
            error.Message.Should().Be("User deletion failed");
            error.Exception.Should().Be(exception);
            error.AdditionalData.Should().BeSameAs(additionalData);
            error.AdditionalData.Should().ContainKey("statusCode").WhoseValue.Should().Be(400);
            error.AdditionalData.Should().ContainKey("graphError").WhoseValue.Should().Be("InvalidUser");
        }

        #endregion

        #region Real World Scenarios Tests

        [Fact]
        public void BulkOperationResult_MixedSuccessAndErrorScenario_ShouldHandleCorrectly()
        {
            // Arrange
            var result = new BulkOperationResult
            {
                Success = false, // Partial failure
                IsSuccess = false,
                OperationType = "BULK_USER_CREATION",
                ExecutionTimeMs = 2500
            };

            var success1 = new BulkOperationSuccess
            {
                Operation = "CREATE_USER",
                EntityId = "user-001",
                EntityName = "Alice Johnson",
                Message = "User created successfully"
            };

            var success2 = new BulkOperationSuccess
            {
                Operation = "CREATE_USER",
                EntityId = "user-002",
                EntityName = "Bob Wilson",
                Message = "User created successfully"
            };

            var error1 = new BulkOperationError
            {
                Operation = "CREATE_USER",
                EntityId = "user-003",
                EntityName = "Invalid User",
                Message = "Email already exists"
            };

            // Act
            result.SuccessfulOperations.Add(success1);
            result.SuccessfulOperations.Add(success2);
            result.Errors.Add(error1);

            // Assert
            result.SuccessfulOperations.Should().HaveCount(2);
            result.Errors.Should().HaveCount(1);
            result.Success.Should().BeFalse(); // Because there were errors
            result.OperationType.Should().Be("BULK_USER_CREATION");
            
            // Check successful operations
            var successfulIds = result.SuccessfulOperations.Select(s => s.EntityId).ToList();
            successfulIds.Should().Contain("user-001");
            successfulIds.Should().Contain("user-002");

            // Check errors
            result.Errors.First().EntityId.Should().Be("user-003");
            result.Errors.First().Message.Should().Contain("Email already exists");
        }

        [Fact]
        public void BulkOperationResult_CompleteSuccessScenario_ShouldIndicateSuccess()
        {
            // Arrange & Act
            var result = BulkOperationResult.CreateSuccess("BULK_TEAM_CREATION", 1200);
            
            result.SuccessfulOperations.Add(new BulkOperationSuccess 
            { 
                Operation = "CREATE_TEAM", 
                EntityId = "team-math", 
                EntityName = "Mathematics Team",
                Message = "Team created with 15 members"
            });
            
            result.SuccessfulOperations.Add(new BulkOperationSuccess 
            { 
                Operation = "CREATE_TEAM", 
                EntityId = "team-science", 
                EntityName = "Science Team",
                Message = "Team created with 12 members"
            });

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.SuccessfulOperations.Should().HaveCount(2);
            
            // Implicit bool conversion should work
            if (result)
            {
                result.ExecutionTimeMs.Should().Be(1200);
                result.OperationType.Should().Be("BULK_TEAM_CREATION");
            }
            else
            {
                false.Should().BeTrue("Success result should evaluate to true");
            }
        }

        [Fact]
        public void BulkOperationResult_CompleteFailureScenario_ShouldIndicateFailure()
        {
            // Arrange & Act
            var result = BulkOperationResult.CreateError("Graph API unavailable", "BULK_DELETION", 5000);
            
            result.Errors.Add(new BulkOperationError 
            { 
                Operation = "DELETE_USER", 
                EntityId = "user-001", 
                Message = "Service unavailable",
                Exception = new TimeoutException("Request timeout")
            });

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.SuccessfulOperations.Should().BeEmpty();
            result.Errors.Should().HaveCount(1);
            result.ErrorMessage.Should().Be("Graph API unavailable");
            
            // Implicit bool conversion should work
            bool isSuccess = result;
            isSuccess.Should().BeFalse();
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void BulkOperationResult_WithNullCollections_ShouldInitializeCollections()
        {
            // Arrange & Act
            var result = new BulkOperationResult();
            
            // Collections should be initialized by default
            result.SuccessfulOperations = null!;
            result.Errors = null!;

            // Re-initialize
            result.SuccessfulOperations = new List<BulkOperationSuccess>();
            result.Errors = new List<BulkOperationError>();

            // Assert
            result.SuccessfulOperations.Should().NotBeNull().And.BeEmpty();
            result.Errors.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void BulkOperationResult_WithLargeExecutionTime_ShouldHandleCorrectly()
        {
            // Arrange & Act
            var result = BulkOperationResult.CreateSuccess("BULK_OPERATION", long.MaxValue);

            // Assert
            result.ExecutionTimeMs.Should().Be(long.MaxValue);
        }

        [Fact]
        public void BulkOperationError_WithComplexException_ShouldRetainExceptionData()
        {
            // Arrange
            var innerException = new ArgumentNullException("userId", "User ID cannot be null");
            var outerException = new InvalidOperationException("Operation failed", innerException);
            
            var error = new BulkOperationError
            {
                Operation = "UPDATE_USER",
                EntityId = "user-123",
                Message = "Complex error occurred",
                Exception = outerException
            };

            // Assert
            error.Exception.Should().Be(outerException);
            error.Exception.InnerException.Should().Be(innerException);
            error.Exception.Message.Should().Be("Operation failed");
            error.Exception.InnerException?.Message.Should().Contain("User ID cannot be null");
        }

        #endregion
    }
} 