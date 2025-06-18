using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla HealthOperationResult i powiązanych klas
    /// Pokrycie: HealthOperationResult, HealthOperationSuccess, HealthOperationError, 
    /// HealthCheckDetail, HealthMetrics, HealthMonitoringProcessStatus
    /// </summary>
    public class HealthOperationResultTests
    {
        #region HealthOperationResult Constructor Tests

        [Fact]
        public void HealthOperationResult_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var result = new HealthOperationResult();

            // Assert - podstawowe właściwości
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().BeNull();
            result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.OperationType.Should().BeNull();
            result.ExecutionTimeMs.Should().BeNull();

            // Assert - kolekcje
            result.SuccessfulOperations.Should().NotBeNull().And.BeEmpty();
            result.Errors.Should().NotBeNull().And.BeEmpty();
            result.HealthChecks.Should().NotBeNull().And.BeEmpty();
            result.Recommendations.Should().NotBeNull().And.BeEmpty();
            result.Metrics.Should().BeNull();
        }

        [Fact]
        public void HealthOperationResult_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var result = new HealthOperationResult();
            var timestamp = DateTime.UtcNow.AddMinutes(-10);
            var metrics = new HealthMetrics
            {
                AverageApiResponseTimeMs = 150.5,
                ActiveConnections = 25
            };

            // Act
            result.Success = true;
            result.IsSuccess = true;
            result.ErrorMessage = "Test warning";
            result.ProcessedAt = timestamp;
            result.OperationType = "COMPREHENSIVE_HEALTH_CHECK";
            result.ExecutionTimeMs = 2500;
            result.Metrics = metrics;

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().Be("Test warning");
            result.ProcessedAt.Should().Be(timestamp);
            result.OperationType.Should().Be("COMPREHENSIVE_HEALTH_CHECK");
            result.ExecutionTimeMs.Should().Be(2500);
            result.Metrics.Should().Be(metrics);
        }

        #endregion

        #region HealthOperationResult Static Methods Tests

        [Fact]
        public void HealthOperationResult_CreateSuccess_ShouldReturnSuccessResult()
        {
            // Act
            var result = HealthOperationResult.CreateSuccess();

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.OperationType.Should().BeNull();
            result.ExecutionTimeMs.Should().BeNull();
            result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void HealthOperationResult_CreateSuccess_WithParameters_ShouldReturnSuccessResultWithData()
        {
            // Arrange
            var operationType = "SYSTEM_HEALTH_CHECK";
            var executionTime = 1800L;

            // Act
            var result = HealthOperationResult.CreateSuccess(operationType, executionTime);

            // Assert
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be(operationType);
            result.ExecutionTimeMs.Should().Be(executionTime);
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void HealthOperationResult_CreateError_ShouldReturnErrorResult()
        {
            // Arrange
            var errorMessage = "Critical system failure";

            // Act
            var result = HealthOperationResult.CreateError(errorMessage);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be(errorMessage);
            result.OperationType.Should().BeNull();
            result.ExecutionTimeMs.Should().BeNull();
        }

        [Fact]
        public void HealthOperationResult_CreateError_WithParameters_ShouldReturnErrorResultWithData()
        {
            // Arrange
            var errorMessage = "Graph API connection failed";
            var operationType = "GRAPH_HEALTH_CHECK";
            var executionTime = 5000L;

            // Act
            var result = HealthOperationResult.CreateError(errorMessage, operationType, executionTime);

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be(errorMessage);
            result.OperationType.Should().Be(operationType);
            result.ExecutionTimeMs.Should().Be(executionTime);
        }

        #endregion

        #region HealthOperationResult Implicit Operator Tests

        [Fact]
        public void HealthOperationResult_ImplicitBoolConversion_WhenSuccess_ShouldReturnTrue()
        {
            // Arrange
            var result = HealthOperationResult.CreateSuccess();

            // Act & Assert
            bool isSuccess = result;
            isSuccess.Should().BeTrue();
        }

        [Fact]
        public void HealthOperationResult_ImplicitBoolConversion_WhenFailure_ShouldReturnFalse()
        {
            // Arrange
            var result = HealthOperationResult.CreateError("Error");

            // Act & Assert
            bool isSuccess = result;
            isSuccess.Should().BeFalse();
        }

        #endregion

        #region HealthOperationSuccess Tests

        [Fact]
        public void HealthOperationSuccess_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var success = new HealthOperationSuccess();

            // Assert
            success.Operation.Should().Be(string.Empty);
            success.Component.Should().Be(string.Empty);
            success.ComponentName.Should().BeNull();
            success.Message.Should().BeNull();
            success.AdditionalData.Should().BeNull();
        }

        [Fact]
        public void HealthOperationSuccess_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var success = new HealthOperationSuccess();
            var additionalData = new Dictionary<string, object>
            {
                { "responseTime", 125.5 },
                { "statusCode", 200 }
            };

            // Act
            success.Operation = "DATABASE_CONNECTION_CHECK";
            success.Component = "Database";
            success.ComponentName = "Main SQL Database";
            success.Message = "Database connection is healthy";
            success.AdditionalData = additionalData;

            // Assert
            success.Operation.Should().Be("DATABASE_CONNECTION_CHECK");
            success.Component.Should().Be("Database");
            success.ComponentName.Should().Be("Main SQL Database");
            success.Message.Should().Be("Database connection is healthy");
            success.AdditionalData.Should().BeSameAs(additionalData);
            success.AdditionalData.Should().ContainKey("responseTime").WhoseValue.Should().Be(125.5);
            success.AdditionalData.Should().ContainKey("statusCode").WhoseValue.Should().Be(200);
        }

        #endregion

        #region HealthOperationError Tests

        [Fact]
        public void HealthOperationError_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var error = new HealthOperationError();

            // Assert
            error.Operation.Should().Be(string.Empty);
            error.Component.Should().BeNull();
            error.ComponentName.Should().BeNull();
            error.Message.Should().Be(string.Empty);
            error.Exception.Should().BeNull();
            error.AdditionalData.Should().BeNull();
            error.Severity.Should().Be(HealthErrorSeverity.Warning);
        }

        [Fact]
        public void HealthOperationError_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var error = new HealthOperationError();
            var exception = new TimeoutException("Database timeout");
            var additionalData = new Dictionary<string, object>
            {
                { "timeoutSeconds", 30 },
                { "retryAttempts", 3 }
            };

            // Act
            error.Operation = "DATABASE_HEALTH_CHECK";
            error.Component = "Database";
            error.ComponentName = "Primary Database";
            error.Message = "Database health check failed";
            error.Exception = exception;
            error.AdditionalData = additionalData;
            error.Severity = HealthErrorSeverity.Critical;

            // Assert
            error.Operation.Should().Be("DATABASE_HEALTH_CHECK");
            error.Component.Should().Be("Database");
            error.ComponentName.Should().Be("Primary Database");
            error.Message.Should().Be("Database health check failed");
            error.Exception.Should().Be(exception);
            error.AdditionalData.Should().BeSameAs(additionalData);
            error.Severity.Should().Be(HealthErrorSeverity.Critical);
        }

        #endregion

        #region HealthCheckDetail Tests

        [Fact]
        public void HealthCheckDetail_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var detail = new HealthCheckDetail();

            // Assert
            detail.ComponentName.Should().Be(string.Empty);
            detail.Status.Should().Be(HealthStatus.Healthy); // default enum value
            detail.Description.Should().Be(string.Empty);
            detail.DurationMs.Should().Be(0);
            detail.Data.Should().BeNull();
            detail.CheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void HealthCheckDetail_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var detail = new HealthCheckDetail();
            var checkedAt = DateTime.UtcNow.AddMinutes(-5);
            var data = new Dictionary<string, object>
            {
                { "memoryUsageMB", 512 },
                { "cpuPercent", 25.6 }
            };

            // Act
            detail.ComponentName = "Graph API";
            detail.Status = HealthStatus.Degraded;
            detail.Description = "Graph API responding slowly";
            detail.DurationMs = 750;
            detail.Data = data;
            detail.CheckedAt = checkedAt;

            // Assert
            detail.ComponentName.Should().Be("Graph API");
            detail.Status.Should().Be(HealthStatus.Degraded);
            detail.Description.Should().Be("Graph API responding slowly");
            detail.DurationMs.Should().Be(750);
            detail.Data.Should().BeSameAs(data);
            detail.CheckedAt.Should().Be(checkedAt);
        }

        #endregion

        #region HealthMetrics Tests

        [Fact]
        public void HealthMetrics_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var metrics = new HealthMetrics();

            // Assert
            metrics.CacheMetrics.Should().BeNull();
            metrics.AverageApiResponseTimeMs.Should().Be(0);
            metrics.ActiveConnections.Should().Be(0);
            metrics.MemoryUsageBytes.Should().Be(0);
            metrics.CpuUsagePercent.Should().Be(0);
            metrics.ErrorsLastHour.Should().Be(0);
            metrics.GraphConnectionStatus.Should().BeNull();
            metrics.TeamsManagerSpecificMetrics.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void HealthMetrics_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var metrics = new HealthMetrics();
            var cacheMetrics = new GraphCacheMetrics
            {
                TotalRequests = 1000,
                CacheHits = 855  // 855/1000 = 85.5% hit rate
            };
            var specificMetrics = new Dictionary<string, object>
            {
                { "teamsCount", 150 },
                { "usersCount", 500 }
            };

            // Act
            metrics.CacheMetrics = cacheMetrics;
            metrics.AverageApiResponseTimeMs = 245.7;
            metrics.ActiveConnections = 15;
            metrics.MemoryUsageBytes = 1073741824; // 1GB
            metrics.CpuUsagePercent = 35.8;
            metrics.ErrorsLastHour = 3;
            metrics.GraphConnectionStatus = "Connected";
            metrics.TeamsManagerSpecificMetrics = specificMetrics;

            // Assert
            metrics.CacheMetrics.Should().Be(cacheMetrics);
            metrics.AverageApiResponseTimeMs.Should().Be(245.7);
            metrics.ActiveConnections.Should().Be(15);
            metrics.MemoryUsageBytes.Should().Be(1073741824);
            metrics.CpuUsagePercent.Should().Be(35.8);
            metrics.ErrorsLastHour.Should().Be(3);
            metrics.GraphConnectionStatus.Should().Be("Connected");
            metrics.TeamsManagerSpecificMetrics.Should().BeSameAs(specificMetrics);
        }

        #endregion

        #region HealthMonitoringProcessStatus Tests

        [Fact]
        public void HealthMonitoringProcessStatus_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var status = new HealthMonitoringProcessStatus();

            // Assert
            status.ProcessId.Should().Be(string.Empty);
            status.OperationType.Should().Be(string.Empty);
            status.Status.Should().Be(string.Empty);
            status.CurrentOperation.Should().Be(string.Empty);
            status.ProgressPercentage.Should().Be(0);
            status.ComponentsChecked.Should().Be(0);
            status.TotalComponents.Should().Be(0);
            status.IssuesFound.Should().Be(0);
            status.IssuesRepaired.Should().Be(0);
            status.StartedAt.Should().Be(default(DateTime));
            status.CompletedAt.Should().BeNull();
            status.CanBeCancelled.Should().BeTrue();
            status.AdditionalData.Should().BeNull();
        }

        [Fact]
        public void HealthMonitoringProcessStatus_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var status = new HealthMonitoringProcessStatus();
            var startedAt = DateTime.UtcNow.AddMinutes(-30);
            var completedAt = DateTime.UtcNow.AddMinutes(-5);
            var additionalData = new Dictionary<string, object>
            {
                { "userId", "admin-001" },
                { "priority", "High" }
            };

            // Act
            status.ProcessId = "health-check-12345";
            status.OperationType = "COMPREHENSIVE_HEALTH_CHECK";
            status.Status = "Completed";
            status.CurrentOperation = "Finalizing report";
            status.ProgressPercentage = 100.0;
            status.ComponentsChecked = 25;
            status.TotalComponents = 25;
            status.IssuesFound = 3;
            status.IssuesRepaired = 2;
            status.StartedAt = startedAt;
            status.CompletedAt = completedAt;
            status.CanBeCancelled = false;
            status.AdditionalData = additionalData;

            // Assert
            status.ProcessId.Should().Be("health-check-12345");
            status.OperationType.Should().Be("COMPREHENSIVE_HEALTH_CHECK");
            status.Status.Should().Be("Completed");
            status.CurrentOperation.Should().Be("Finalizing report");
            status.ProgressPercentage.Should().Be(100.0);
            status.ComponentsChecked.Should().Be(25);
            status.TotalComponents.Should().Be(25);
            status.IssuesFound.Should().Be(3);
            status.IssuesRepaired.Should().Be(2);
            status.StartedAt.Should().Be(startedAt);
            status.CompletedAt.Should().Be(completedAt);
            status.CanBeCancelled.Should().BeFalse();
            status.AdditionalData.Should().BeSameAs(additionalData);
        }

        #endregion

        #region Real World Scenarios Tests

        [Fact]
        public void HealthOperationResult_ComprehensiveHealthCheckScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var result = HealthOperationResult.CreateSuccess("COMPREHENSIVE_HEALTH_CHECK", 3500);

            // Dodajemy pomyślne sprawdzenia
            result.SuccessfulOperations.Add(new HealthOperationSuccess
            {
                Operation = "DATABASE_CHECK",
                Component = "Database",
                ComponentName = "Main SQL Database",
                Message = "Database is responding normally"
            });

            result.SuccessfulOperations.Add(new HealthOperationSuccess
            {
                Operation = "CACHE_CHECK",
                Component = "Cache",
                ComponentName = "Redis Cache",
                Message = "Cache hit rate is 92%"
            });

            // Dodajemy ostrzeżenie
            result.Errors.Add(new HealthOperationError
            {
                Operation = "GRAPH_API_CHECK",
                Component = "Graph API",
                ComponentName = "Microsoft Graph",
                Message = "Graph API response time is above threshold",
                Severity = HealthErrorSeverity.Warning
            });

            // Dodajemy szczegółowe sprawdzenia
            result.HealthChecks.Add(new HealthCheckDetail
            {
                ComponentName = "Database",
                Status = HealthStatus.Healthy,
                Description = "SQL Server responding in 50ms",
                DurationMs = 50
            });

            result.HealthChecks.Add(new HealthCheckDetail
            {
                ComponentName = "Graph API",
                Status = HealthStatus.Degraded,
                Description = "Graph API responding in 2500ms",
                DurationMs = 2500
            });

            // Dodajemy metryki
            result.Metrics = new HealthMetrics
            {
                AverageApiResponseTimeMs = 1275.0,
                ActiveConnections = 12,
                MemoryUsageBytes = 536870912, // 512MB
                CpuUsagePercent = 45.2,
                ErrorsLastHour = 1
            };

            // Dodajemy rekomendacje
            result.Recommendations.Add("Zoptymalizuj zapytania do Graph API");
            result.Recommendations.Add("Rozważ zwiększenie timeout dla Graph API");

            // Assert
            result.Success.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(2);
            result.Errors.Should().HaveCount(1);
            result.HealthChecks.Should().HaveCount(2);
            result.Recommendations.Should().HaveCount(2);
            result.Metrics.Should().NotBeNull();

            // Sprawdź szczegóły
            var graphError = result.Errors.First();
            graphError.Severity.Should().Be(HealthErrorSeverity.Warning);
            graphError.Component.Should().Be("Graph API");

            var dbCheck = result.HealthChecks.First(h => h.ComponentName == "Database");
            dbCheck.Status.Should().Be(HealthStatus.Healthy);
            dbCheck.DurationMs.Should().Be(50);

            var graphCheck = result.HealthChecks.First(h => h.ComponentName == "Graph API");
            graphCheck.Status.Should().Be(HealthStatus.Degraded);
            graphCheck.DurationMs.Should().Be(2500);

            // Sprawdź metryki
            result.Metrics.AverageApiResponseTimeMs.Should().Be(1275.0);
            result.Metrics.ErrorsLastHour.Should().Be(1);
        }

        [Fact]
        public void HealthOperationResult_CriticalFailureScenario_ShouldIndicateFailure()
        {
            // Arrange & Act
            var result = HealthOperationResult.CreateError("Multiple critical failures detected", "HEALTH_CHECK", 1200);

            result.Errors.Add(new HealthOperationError
            {
                Operation = "DATABASE_CHECK",
                Component = "Database",
                Message = "Cannot connect to database",
                Severity = HealthErrorSeverity.Critical,
                Exception = new TimeoutException("Connection timeout after 30 seconds")
            });

            result.Errors.Add(new HealthOperationError
            {
                Operation = "GRAPH_API_CHECK",
                Component = "Graph API",
                Message = "Graph API authentication failed",
                Severity = HealthErrorSeverity.Critical
            });

            result.HealthChecks.Add(new HealthCheckDetail
            {
                ComponentName = "Database",
                Status = HealthStatus.Unhealthy,
                Description = "Connection timeout",
                DurationMs = 30000
            });

            // Assert
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().HaveCount(2);
            result.SuccessfulOperations.Should().BeEmpty();

            // Sprawdź błędy krytyczne
            var criticalErrors = result.Errors.Where(e => e.Severity == HealthErrorSeverity.Critical).ToList();
            criticalErrors.Should().HaveCount(2);

            var dbError = criticalErrors.First(e => e.Component == "Database");
            dbError.Exception.Should().NotBeNull();
            dbError.Exception.Should().BeOfType<TimeoutException>();

            // Sprawdź niezdrowe komponenty
            var unhealthyChecks = result.HealthChecks.Where(h => h.Status == HealthStatus.Unhealthy).ToList();
            unhealthyChecks.Should().HaveCount(1);
            unhealthyChecks.First().ComponentName.Should().Be("Database");
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void HealthOperationResult_WithLargeDataSets_ShouldHandleCorrectly()
        {
            // Arrange
            var result = HealthOperationResult.CreateSuccess("BULK_HEALTH_CHECK");

            // Act - dodaj dużo sprawdzeń
            for (int i = 0; i < 100; i++)
            {
                result.HealthChecks.Add(new HealthCheckDetail
                {
                    ComponentName = $"Component-{i:D3}",
                    Status = i % 10 == 0 ? HealthStatus.Degraded : HealthStatus.Healthy,
                    Description = $"Health check for component {i}",
                    DurationMs = 50 + i
                });
            }

            // Assert
            result.HealthChecks.Should().HaveCount(100);
            var degradedCount = result.HealthChecks.Count(h => h.Status == HealthStatus.Degraded);
            degradedCount.Should().Be(10); // co 10-ty element
        }

        [Fact]
        public void HealthErrorSeverity_AllValues_ShouldBeValid()
        {
            // Assert - sprawdź wszystkie możliwe wartości severity
            var info = HealthErrorSeverity.Info;
            var warning = HealthErrorSeverity.Warning;
            var error = HealthErrorSeverity.Error;
            var critical = HealthErrorSeverity.Critical;

            info.Should().Be(HealthErrorSeverity.Info);
            warning.Should().Be(HealthErrorSeverity.Warning);
            error.Should().Be(HealthErrorSeverity.Error);
            critical.Should().Be(HealthErrorSeverity.Critical);

            // Sprawdź kolejność (ważność)
            ((int)info).Should().BeLessThan((int)warning);
            ((int)warning).Should().BeLessThan((int)error);
            ((int)error).Should().BeLessThan((int)critical);
        }

        [Fact]
        public void HealthStatus_AllValues_ShouldBeValid()
        {
            // Assert - sprawdź wszystkie możliwe wartości status
            var healthy = HealthStatus.Healthy;
            var degraded = HealthStatus.Degraded;
            var unhealthy = HealthStatus.Unhealthy;

            healthy.Should().Be(HealthStatus.Healthy);
            degraded.Should().Be(HealthStatus.Degraded);
            unhealthy.Should().Be(HealthStatus.Unhealthy);

            // Sprawdź kolejność (pogarszanie się stanu)
            ((int)healthy).Should().BeLessThan((int)degraded);
            ((int)degraded).Should().BeLessThan((int)unhealthy);
        }

        #endregion
    }
} 