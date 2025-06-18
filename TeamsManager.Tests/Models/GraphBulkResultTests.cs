using System;
using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla modelu GraphBulkResult
    /// Testuje funkcjonalność wyniku operacji masowych Graph API
    /// </summary>
    public class GraphBulkResultTests
    {
        #region Constructor and Basic Properties Tests

        [Fact]
        public void GraphBulkResult_WhenCreated_ShouldHaveDefaultValues()
        {
            // Act
            var result = new GraphBulkResult();

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().BeNull();
            result.RequestId.Should().BeNull();
            result.BatchId.Should().BeNull();
            result.GraphEndpoint.Should().BeNull();
            result.HttpMethod.Should().BeNull();
            result.HttpStatusCode.Should().BeNull();
            result.FromCache.Should().BeFalse();
            result.ETag.Should().BeNull();
            result.WasRetried.Should().BeFalse();
            result.RetryCount.Should().Be(0);
            result.ExecutionTimeMs.Should().Be(0);
            result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            result.SuccessfulOperations.Should().BeEmpty();
            result.Errors.Should().BeEmpty();
            result.BatchResults.Should().BeEmpty();
            result.RateLimitInfo.Should().BeNull();
            result.Metadata.Should().BeEmpty();
        }

        [Fact]
        public void GraphBulkResult_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var result = new GraphBulkResult();
            var testDateTime = DateTime.UtcNow.AddMinutes(-5);

            // Act
            result.Success = true;
            result.IsSuccess = true;
            result.ErrorMessage = "Test error";
            result.RequestId = "req-123";
            result.BatchId = "batch-456";
            result.GraphEndpoint = "/v1.0/teams";
            result.HttpMethod = "POST";
            result.HttpStatusCode = HttpStatusCode.OK;
            result.FromCache = true;
            result.ETag = "etag-789";
            result.WasRetried = true;
            result.RetryCount = 2;
            result.ExecutionTimeMs = 1500;
            result.ProcessedAt = testDateTime;

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().Be("Test error");
            result.RequestId.Should().Be("req-123");
            result.BatchId.Should().Be("batch-456");
            result.GraphEndpoint.Should().Be("/v1.0/teams");
            result.HttpMethod.Should().Be("POST");
            result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
            result.FromCache.Should().BeTrue();
            result.ETag.Should().Be("etag-789");
            result.WasRetried.Should().BeTrue();
            result.RetryCount.Should().Be(2);
            result.ExecutionTimeMs.Should().Be(1500);
            result.ProcessedAt.Should().Be(testDateTime);
        }

        #endregion

        #region Computed Properties Tests

        [Fact]
        public void HasPerformanceIssues_WhenExecutionTimeUnder5Seconds_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphBulkResult { ExecutionTimeMs = 4999 };

            // Act & Assert
            result.HasPerformanceIssues.Should().BeFalse();
        }

        [Fact]
        public void HasPerformanceIssues_WhenExecutionTimeOver5Seconds_ShouldReturnTrue()
        {
            // Arrange
            var result = new GraphBulkResult { ExecutionTimeMs = 5001 };

            // Act & Assert
            result.HasPerformanceIssues.Should().BeTrue();
        }

        [Theory]
        [InlineData(true, 95.0, true)]
        [InlineData(true, 85.0, false)]
        [InlineData(false, 95.0, true)]
        [InlineData(false, 85.0, false)]
        public void HasRateLimitIssues_ShouldReturnCorrectValue(bool isLimitReached, double usagePercentage, bool expectedResult)
        {
            // Arrange
            var result = new GraphBulkResult
            {
                RateLimitInfo = new GraphRateLimitStatus
                {
                    IsLimitReached = isLimitReached,
                    UsagePercentage = usagePercentage
                }
            };

            // Act & Assert
            result.HasRateLimitIssues.Should().Be(expectedResult);
        }

        [Fact]
        public void HasRateLimitIssues_WhenRateLimitInfoIsNull_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphBulkResult { RateLimitInfo = null };

            // Act & Assert
            result.HasRateLimitIssues.Should().BeFalse();
        }

        [Fact]
        public void AddedCount_ShouldCountAddAndCreateOperations()
        {
            // Arrange
            var result = new GraphBulkResult();
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess { Operation = "AddUser" });
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess { Operation = "CreateTeam" });
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess { Operation = "UpdateUser" });

            // Act & Assert
            result.AddedCount.Should().Be(2);
        }

        [Fact]
        public void RemovedCount_ShouldCountRemoveAndDeleteOperations()
        {
            // Arrange
            var result = new GraphBulkResult();
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess { Operation = "RemoveUser" });
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess { Operation = "DeleteTeam" });
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess { Operation = "UpdateUser" });

            // Act & Assert
            result.RemovedCount.Should().Be(2);
        }

        [Fact]
        public void TotalOperations_ShouldReturnSumOfSuccessAndErrors()
        {
            // Arrange
            var result = new GraphBulkResult();
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess());
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess());
            result.Errors.Add(new GraphBulkOperationError());

            // Act & Assert
            result.TotalOperations.Should().Be(3);
        }

        [Fact]
        public void SuccessRate_WhenNoOperations_ShouldReturnZero()
        {
            // Arrange
            var result = new GraphBulkResult();

            // Act & Assert
            result.SuccessRate.Should().Be(0.0);
        }

        [Fact]
        public void SuccessRate_ShouldCalculateCorrectPercentage()
        {
            // Arrange
            var result = new GraphBulkResult();
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess());
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess());
            result.SuccessfulOperations.Add(new GraphBulkOperationSuccess());
            result.Errors.Add(new GraphBulkOperationError());

            // Act & Assert
            result.SuccessRate.Should().Be(75.0); // 3/4 = 75%
        }

        #endregion

        #region ShouldRetry Tests

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, 0, true)]
        [InlineData(HttpStatusCode.InternalServerError, 1, true)]
        [InlineData(HttpStatusCode.BadGateway, 2, true)]
        [InlineData(HttpStatusCode.ServiceUnavailable, 0, true)]
        [InlineData(HttpStatusCode.GatewayTimeout, 1, true)]
        public void ShouldRetry_WhenRetryableStatusCodeAndLowRetryCount_ShouldReturnTrue(HttpStatusCode statusCode, int retryCount, bool expectedResult)
        {
            // Arrange
            var result = new GraphBulkResult
            {
                Success = false,
                HttpStatusCode = statusCode,
                RetryCount = retryCount
            };

            // Act & Assert
            result.ShouldRetry.Should().Be(expectedResult);
        }

        [Fact]
        public void ShouldRetry_WhenRetryCountTooHigh_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphBulkResult
            {
                Success = false,
                HttpStatusCode = HttpStatusCode.TooManyRequests,
                RetryCount = 3
            };

            // Act & Assert
            result.ShouldRetry.Should().BeFalse();
        }

        [Fact]
        public void ShouldRetry_WhenSuccessful_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphBulkResult
            {
                Success = true,
                HttpStatusCode = HttpStatusCode.TooManyRequests,
                RetryCount = 0
            };

            // Act & Assert
            result.ShouldRetry.Should().BeFalse();
        }

        [Fact]
        public void ShouldRetry_WhenNonRetryableStatusCode_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphBulkResult
            {
                Success = false,
                HttpStatusCode = HttpStatusCode.BadRequest,
                RetryCount = 0
            };

            // Act & Assert
            result.ShouldRetry.Should().BeFalse();
        }

        #endregion

        #region Static Factory Methods Tests

        [Fact]
        public void CreateSuccess_ShouldReturnSuccessfulResult()
        {
            // Arrange
            var endpoint = "/v1.0/teams";
            var method = "POST";
            var executionTime = 1500L;

            // Act
            var result = GraphBulkResult.CreateSuccess(endpoint, method, executionTime);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.GraphEndpoint.Should().Be(endpoint);
            result.HttpMethod.Should().Be(method);
            result.ExecutionTimeMs.Should().Be(executionTime);
            result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void CreateError_ShouldReturnErrorResult()
        {
            // Arrange
            var errorMessage = "Test error occurred";
            var endpoint = "/v1.0/teams";
            var method = "POST";
            var statusCode = HttpStatusCode.BadRequest;
            var executionTime = 500L;

            // Act
            var result = GraphBulkResult.CreateError(errorMessage, endpoint, method, statusCode, executionTime);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be(errorMessage);
            result.GraphEndpoint.Should().Be(endpoint);
            result.HttpMethod.Should().Be(method);
            result.HttpStatusCode.Should().Be(statusCode);
            result.ExecutionTimeMs.Should().Be(executionTime);
        }

        [Fact]
        public void CreateFromCache_ShouldReturnCachedResult()
        {
            // Arrange
            var endpoint = "/v1.0/users";
            var etag = "etag-123";

            // Act
            var result = GraphBulkResult.CreateFromCache(endpoint, etag);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.GraphEndpoint.Should().Be(endpoint);
            result.FromCache.Should().BeTrue();
            result.ETag.Should().Be(etag);
            result.ExecutionTimeMs.Should().Be(0);
        }

        [Fact]
        public void CreateBatchResult_ShouldReturnBatchResult()
        {
            // Arrange
            var batchResults = new List<GraphBatchOperationResult>
            {
                new GraphBatchOperationResult { Status = 200 },
                new GraphBatchOperationResult { Status = 201 },
                new GraphBatchOperationResult { Status = 400 }
            };
            var batchId = "batch-123";

            // Act
            var result = GraphBulkResult.CreateBatchResult(batchResults, batchId);

            // Assert
            result.BatchResults.Should().HaveCount(3);
            result.BatchResults.Should().BeEquivalentTo(batchResults);
            result.GraphEndpoint.Should().Be("/v1.0/$batch");
            result.HttpMethod.Should().Be("POST");
            result.Success.Should().BeFalse(); // 2 successes, 1 failure
            result.IsSuccess.Should().BeFalse();
            result.BatchId.Should().Be(batchId);
            result.Metadata.Should().ContainKey("BatchId");
        }

        [Fact]
        public void CreateBatchResult_WhenAllSuccessful_ShouldReturnSuccess()
        {
            // Arrange
            var batchResults = new List<GraphBatchOperationResult>
            {
                new GraphBatchOperationResult { Status = 200 },
                new GraphBatchOperationResult { Status = 201 }
            };

            // Act
            var result = GraphBulkResult.CreateBatchResult(batchResults);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
        }

        #endregion

        #region Add Operations Tests

        [Fact]
        public void AddSuccess_ShouldAddOperationAndUpdateStatus()
        {
            // Arrange
            var result = new GraphBulkResult();
            var operation = new GraphBulkOperationSuccess
            {
                Operation = "CreateTeam",
                EntityId = "team-123"
            };

            // Act
            result.AddSuccess(operation);

            // Assert
            result.SuccessfulOperations.Should().Contain(operation);
            result.SuccessfulOperations.Should().HaveCount(1);
        }

        [Fact]
        public void AddError_ShouldAddErrorAndUpdateStatus()
        {
            // Arrange
            var result = new GraphBulkResult();
            var error = new GraphBulkOperationError
            {
                Operation = "CreateTeam",
                EntityId = "team-123",
                Message = "Team creation failed"
            };

            // Act
            result.AddError(error);

            // Assert
            result.Errors.Should().Contain(error);
            result.Errors.Should().HaveCount(1);
        }

        [Fact]
        public void AddSuccess_WhenOnlySuccesses_ShouldMaintainSuccessStatus()
        {
            // Arrange
            var result = new GraphBulkResult { Success = true, IsSuccess = true };
            var operation = new GraphBulkOperationSuccess();

            // Act
            result.AddSuccess(operation);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void AddError_ShouldSetFailureStatus()
        {
            // Arrange
            var result = new GraphBulkResult { Success = true, IsSuccess = true };
            var error = new GraphBulkOperationError();

            // Act
            result.AddError(error);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
        }

        #endregion

        #region Metadata Tests

        [Fact]
        public void AddMetadata_ShouldAddKeyValuePair()
        {
            // Arrange
            var result = new GraphBulkResult();
            var key = "TestKey";
            var value = "TestValue";

            // Act
            result.AddMetadata(key, value);

            // Assert
            result.Metadata.Should().ContainKey(key);
            result.Metadata[key].Should().Be(value);
        }

        [Fact]
        public void AddMetadata_WhenKeyExists_ShouldOverwriteValue()
        {
            // Arrange
            var result = new GraphBulkResult();
            var key = "TestKey";
            var originalValue = "OriginalValue";
            var newValue = "NewValue";

            result.AddMetadata(key, originalValue);

            // Act
            result.AddMetadata(key, newValue);

            // Assert
            result.Metadata[key].Should().Be(newValue);
        }

        [Fact]
        public void GetMetadata_WhenKeyExists_ShouldReturnValue()
        {
            // Arrange
            var result = new GraphBulkResult();
            var key = "TestKey";
            var value = 42;
            result.AddMetadata(key, value);

            // Act
            var retrievedValue = result.GetMetadata<int>(key);

            // Assert
            retrievedValue.Should().Be(value);
        }

        [Fact]
        public void GetMetadata_WhenKeyDoesNotExist_ShouldReturnDefault()
        {
            // Arrange
            var result = new GraphBulkResult();

            // Act
            var retrievedValue = result.GetMetadata<string>("NonExistentKey");

            // Assert
            retrievedValue.Should().BeNull();
        }

        [Fact]
        public void GetMetadata_WithWrongType_ShouldReturnDefault()
        {
            // Arrange
            var result = new GraphBulkResult();
            var key = "TestKey";
            result.AddMetadata(key, "StringValue");

            // Act
            var retrievedValue = result.GetMetadata<int>(key);

            // Assert
            retrievedValue.Should().Be(0); // default int
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void GraphBulkResult_WithNullHttpStatusCode_ShouldHandleGracefully()
        {
            // Arrange
            var result = new GraphBulkResult
            {
                Success = false,
                HttpStatusCode = null,
                RetryCount = 0
            };

            // Act & Assert
            result.ShouldRetry.Should().BeFalse();
        }

        [Fact]
        public void SuccessRate_WithZeroDivision_ShouldNotThrow()
        {
            // Arrange
            var result = new GraphBulkResult();

            // Act & Assert
            var action = () => result.SuccessRate;
            action.Should().NotThrow();
            result.SuccessRate.Should().Be(0.0);
        }

        [Fact]
        public void ProcessedAt_ShouldBeSetOnCreation()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var result = new GraphBulkResult();
            var afterCreation = DateTime.UtcNow;

            // Assert
            result.ProcessedAt.Should().BeOnOrAfter(beforeCreation);
            result.ProcessedAt.Should().BeOnOrBefore(afterCreation);
        }

        #endregion
    }
} 