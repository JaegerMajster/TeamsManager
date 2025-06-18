using System;
using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla modelu GraphOperationResult<T>
    /// Testuje funkcjonalność wyniku operacji Graph API
    /// </summary>
    public class GraphOperationResultTests
    {
        #region Constructor and Basic Properties Tests

        [Fact]
        public void GraphOperationResult_WhenCreated_ShouldHaveDefaultValues()
        {
            // Act
            var result = new GraphOperationResult<string>();

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().BeNull();
            result.Data.Should().BeNull();
            result.GraphEndpoint.Should().BeNull();
            result.HttpMethod.Should().BeNull();
            result.HttpStatusCode.Should().BeNull();
            result.RequestId.Should().BeNull();
            result.ErrorCode.Should().BeNull();
            result.ErrorDetails.Should().BeNull();
            result.FromCache.Should().BeFalse();
            result.ETag.Should().BeNull();
            result.WasRetried.Should().BeFalse();
            result.RetryCount.Should().Be(0);
            result.ExecutionTimeMs.Should().Be(0);
            result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            result.Metadata.Should().BeEmpty();
        }

        [Fact]
        public void GraphOperationResult_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var result = new GraphOperationResult<string>();
            var testDateTime = DateTime.UtcNow.AddMinutes(-5);
            var testData = "Test Data";

            // Act
            result.Success = true;
            result.IsSuccess = true;
            result.ErrorMessage = "Test error";
            result.Data = testData;
            result.GraphEndpoint = "/v1.0/users";
            result.HttpMethod = "GET";
            result.HttpStatusCode = HttpStatusCode.OK;
            result.RequestId = "req-123";
            result.ErrorCode = "InvalidRequest";
            result.ErrorDetails = "Detailed error info";
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
            result.Data.Should().Be(testData);
            result.GraphEndpoint.Should().Be("/v1.0/users");
            result.HttpMethod.Should().Be("GET");
            result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
            result.RequestId.Should().Be("req-123");
            result.ErrorCode.Should().Be("InvalidRequest");
            result.ErrorDetails.Should().Be("Detailed error info");
            result.FromCache.Should().BeTrue();
            result.ETag.Should().Be("etag-789");
            result.WasRetried.Should().BeTrue();
            result.RetryCount.Should().Be(2);
            result.ExecutionTimeMs.Should().Be(1500);
            result.ProcessedAt.Should().Be(testDateTime);
        }

        #endregion

        #region Generic Type Tests

        [Fact]
        public void GraphOperationResult_WithIntType_ShouldWorkCorrectly()
        {
            // Arrange
            var result = new GraphOperationResult<int>();
            var testData = 42;

            // Act
            result.Data = testData;

            // Assert
            result.Data.Should().Be(testData);
        }

        [Fact]
        public void GraphOperationResult_WithComplexType_ShouldWorkCorrectly()
        {
            // Arrange
            var result = new GraphOperationResult<List<string>>();
            var testData = new List<string> { "item1", "item2", "item3" };

            // Act
            result.Data = testData;

            // Assert
            result.Data.Should().BeEquivalentTo(testData);
            result.Data.Should().HaveCount(3);
        }

        [Fact]
        public void GraphOperationResult_WithNullableType_ShouldHandleNull()
        {
            // Arrange
            var result = new GraphOperationResult<string?>();

            // Act
            result.Data = null;

            // Assert
            result.Data.Should().BeNull();
        }

        #endregion

        #region Computed Properties Tests

        [Fact]
        public void HasPerformanceIssues_WhenExecutionTimeUnder2Seconds_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphOperationResult<string> { ExecutionTimeMs = 1999 };

            // Act & Assert
            result.HasPerformanceIssues.Should().BeFalse();
        }

        [Fact]
        public void HasPerformanceIssues_WhenExecutionTimeOver2Seconds_ShouldReturnTrue()
        {
            // Arrange
            var result = new GraphOperationResult<string> { ExecutionTimeMs = 2001 };

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
            var result = new GraphOperationResult<string>
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
            var result = new GraphOperationResult<string> { RateLimitInfo = null };

            // Act & Assert
            result.HasRateLimitIssues.Should().BeFalse();
        }

        [Fact]
        public void IsRetryable_WhenSuccessful_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                Success = true,
                HttpStatusCode = HttpStatusCode.OK,
                RetryCount = 0
            };

            // Act & Assert
            result.IsRetryable.Should().BeFalse();
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, 0, true)]
        [InlineData(HttpStatusCode.InternalServerError, 1, true)]
        [InlineData(HttpStatusCode.BadGateway, 2, true)]
        [InlineData(HttpStatusCode.ServiceUnavailable, 0, true)]
        [InlineData(HttpStatusCode.GatewayTimeout, 1, true)]
        public void IsRetryable_WhenRetryableStatusCodeAndLowRetryCount_ShouldReturnTrue(HttpStatusCode statusCode, int retryCount, bool expectedResult)
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                Success = false,
                HttpStatusCode = statusCode,
                RetryCount = retryCount
            };

            // Act & Assert
            result.IsRetryable.Should().Be(expectedResult);
        }

        [Fact]
        public void IsRetryable_WhenRetryCountTooHigh_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                Success = false,
                HttpStatusCode = HttpStatusCode.TooManyRequests,
                RetryCount = 3
            };

            // Act & Assert
            result.IsRetryable.Should().BeFalse();
        }

        [Fact]
        public void IsRetryable_WhenNonRetryableStatusCode_ShouldReturnFalse()
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                Success = false,
                HttpStatusCode = HttpStatusCode.BadRequest,
                RetryCount = 0
            };

            // Act & Assert
            result.IsRetryable.Should().BeFalse();
        }

        #endregion

        #region Static Factory Methods Tests

        [Fact]
        public void CreateSuccess_ShouldReturnSuccessfulResult()
        {
            // Arrange
            var data = "Test Data";
            var endpoint = "/v1.0/users";
            var method = "GET";
            var executionTime = 500L;

            // Act
            var result = GraphOperationResult<string>.CreateSuccess(data, endpoint, method, executionTime);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(data);
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
            var endpoint = "/v1.0/users";
            var method = "GET";
            var statusCode = HttpStatusCode.BadRequest;
            var errorCode = "InvalidRequest";
            var executionTime = 200L;

            // Act
            var result = GraphOperationResult<string>.CreateError(errorMessage, endpoint, method, statusCode, errorCode, executionTime);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be(errorMessage);
            result.GraphEndpoint.Should().Be(endpoint);
            result.HttpMethod.Should().Be(method);
            result.HttpStatusCode.Should().Be(statusCode);
            result.ErrorCode.Should().Be(errorCode);
            result.ExecutionTimeMs.Should().Be(executionTime);
            result.Data.Should().BeNull();
        }

        [Fact]
        public void CreateFromCache_ShouldReturnCachedResult()
        {
            // Arrange
            var data = "Cached Data";
            var endpoint = "/v1.0/users";
            var etag = "etag-123";

            // Act
            var result = GraphOperationResult<string>.CreateFromCache(data, endpoint, etag);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(data);
            result.GraphEndpoint.Should().Be(endpoint);
            result.FromCache.Should().BeTrue();
            result.ETag.Should().Be(etag);
            result.ExecutionTimeMs.Should().Be(0);
        }

        [Fact]
        public void CreateFromException_ShouldReturnErrorResult()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var endpoint = "/v1.0/users";
            var method = "GET";

            // Act
            var result = GraphOperationResult<string>.CreateFromException(exception, endpoint, method);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be("Test exception");
            result.GraphEndpoint.Should().Be(endpoint);
            result.HttpMethod.Should().Be(method);
            result.Data.Should().BeNull();
        }

        #endregion

        #region Metadata Tests

        [Fact]
        public void AddMetadata_ShouldAddKeyValuePair()
        {
            // Arrange
            var result = new GraphOperationResult<string>();
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
            var result = new GraphOperationResult<string>();
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
            var result = new GraphOperationResult<string>();
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
            var result = new GraphOperationResult<string>();

            // Act
            var retrievedValue = result.GetMetadata<string>("NonExistentKey");

            // Assert
            retrievedValue.Should().BeNull();
        }

        [Fact]
        public void GetMetadata_WithWrongType_ShouldReturnDefault()
        {
            // Arrange
            var result = new GraphOperationResult<string>();
            var key = "TestKey";
            result.AddMetadata(key, "StringValue");

            // Act
            var retrievedValue = result.GetMetadata<int>(key);

            // Assert
            retrievedValue.Should().Be(0); // default int
        }

        #endregion

        #region Conversion Methods Tests

        [Fact]
        public void ToOperationResult_ShouldConvertToGenericOperationResult()
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                Success = true,
                Data = "Test Data",
                ErrorMessage = "Test Error",
                ExecutionTimeMs = 1000
            };

            // Act
            var operationResult = result.ToOperationResult();

            // Assert
            operationResult.Success.Should().Be(result.Success);
            operationResult.Data.Should().Be(result.Data);
            operationResult.ErrorMessage.Should().Be(result.ErrorMessage);
            operationResult.ExecutionTimeMs.Should().Be(result.ExecutionTimeMs);
        }

        [Fact]
        public void ToBulkResult_ShouldConvertToBulkResult()
        {
            // Arrange
            var result = new GraphOperationResult<List<string>>
            {
                Success = true,
                Data = new List<string> { "item1", "item2" },
                GraphEndpoint = "/v1.0/users",
                HttpMethod = "GET"
            };

            // Act
            var bulkResult = result.ToBulkResult();

            // Assert
            bulkResult.Success.Should().Be(result.Success);
            bulkResult.GraphEndpoint.Should().Be(result.GraphEndpoint);
            bulkResult.HttpMethod.Should().Be(result.HttpMethod);
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void GraphOperationResult_WithNullHttpStatusCode_ShouldHandleGracefully()
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                Success = false,
                HttpStatusCode = null,
                RetryCount = 0
            };

            // Act & Assert
            result.IsRetryable.Should().BeFalse();
        }

        [Fact]
        public void ProcessedAt_ShouldBeSetOnCreation()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var result = new GraphOperationResult<string>();
            var afterCreation = DateTime.UtcNow;

            // Assert
            result.ProcessedAt.Should().BeOnOrAfter(beforeCreation);
            result.ProcessedAt.Should().BeOnOrBefore(afterCreation);
        }

        [Fact]
        public void GraphOperationResult_WithValueType_ShouldHandleDefault()
        {
            // Arrange & Act
            var result = new GraphOperationResult<int>();

            // Assert
            result.Data.Should().Be(0); // default int value
        }

        [Fact]
        public void GraphOperationResult_WithReferenceType_ShouldHandleNull()
        {
            // Arrange & Act
            var result = new GraphOperationResult<object>();

            // Assert
            result.Data.Should().BeNull();
        }

        #endregion

        #region Performance and Rate Limit Integration Tests

        [Fact]
        public void GraphOperationResult_WithBothPerformanceAndRateLimitIssues_ShouldReportBoth()
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                ExecutionTimeMs = 3000, // > 2 seconds
                RateLimitInfo = new GraphRateLimitStatus
                {
                    IsLimitReached = true,
                    UsagePercentage = 95.0
                }
            };

            // Act & Assert
            result.HasPerformanceIssues.Should().BeTrue();
            result.HasRateLimitIssues.Should().BeTrue();
        }

        [Fact]
        public void GraphOperationResult_WhenOptimal_ShouldReportNoIssues()
        {
            // Arrange
            var result = new GraphOperationResult<string>
            {
                ExecutionTimeMs = 500, // < 2 seconds
                RateLimitInfo = new GraphRateLimitStatus
                {
                    IsLimitReached = false,
                    UsagePercentage = 50.0
                }
            };

            // Act & Assert
            result.HasPerformanceIssues.Should().BeFalse();
            result.HasRateLimitIssues.Should().BeFalse();
        }

        #endregion
    }
} 