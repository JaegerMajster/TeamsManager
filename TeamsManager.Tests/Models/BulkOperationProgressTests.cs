using System;
using System.Collections.Generic;
using FluentAssertions;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla modelu BulkOperationProgress
    /// Testuje funkcjonalność raportowania postępu operacji masowych
    /// </summary>
    public class BulkOperationProgressTests
    {
        #region Constructor and Basic Properties Tests

        [Fact]
        public void BulkOperationProgress_WhenCreated_ShouldHaveDefaultValues()
        {
            // Act
            var progress = new BulkOperationProgress();

            // Assert
            progress.TotalOperations.Should().Be(0);
            progress.CompletedOperations.Should().Be(0);
            progress.SuccessfulOperations.Should().Be(0);
            progress.FailedOperations.Should().Be(0);
            progress.CurrentOperation.Should().BeNull();
            progress.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            progress.AdditionalInfo.Should().BeEmpty();
        }

        [Fact]
        public void BulkOperationProgress_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var progress = new BulkOperationProgress();
            var testDateTime = DateTime.UtcNow.AddMinutes(-10);

            // Act
            progress.TotalOperations = 100;
            progress.CompletedOperations = 50;
            progress.SuccessfulOperations = 45;
            progress.FailedOperations = 5;
            progress.CurrentOperation = "Creating team";
            progress.StartTime = testDateTime;

            // Assert
            progress.TotalOperations.Should().Be(100);
            progress.CompletedOperations.Should().Be(50);
            progress.SuccessfulOperations.Should().Be(45);
            progress.FailedOperations.Should().Be(5);
            progress.CurrentOperation.Should().Be("Creating team");
            progress.StartTime.Should().Be(testDateTime);
        }

        #endregion

        #region Percentage Calculations Tests

        [Fact]
        public void PercentageComplete_WhenNoOperations_ShouldReturnZero()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 0,
                CompletedOperations = 0
            };

            // Act & Assert
            progress.PercentageComplete.Should().Be(0.0);
        }

        [Fact]
        public void PercentageComplete_ShouldCalculateCorrectPercentage()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 25
            };

            // Act & Assert
            progress.PercentageComplete.Should().Be(25.0);
        }

        [Fact]
        public void PercentageComplete_WhenFullyCompleted_ShouldReturn100()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 50
            };

            // Act & Assert
            progress.PercentageComplete.Should().Be(100.0);
        }

        [Fact]
        public void PercentageComplete_WhenOverCompleted_ShouldReturnOver100()
        {
            // Arrange - edge case where completed > total
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 60
            };

            // Act & Assert
            progress.PercentageComplete.Should().Be(120.0);
        }

        [Fact]
        public void SuccessRate_WhenNoCompletedOperations_ShouldReturnZero()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                CompletedOperations = 0,
                SuccessfulOperations = 0
            };

            // Act & Assert
            progress.SuccessRate.Should().Be(0.0);
        }

        [Fact]
        public void SuccessRate_ShouldCalculateCorrectPercentage()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                CompletedOperations = 20,
                SuccessfulOperations = 15
            };

            // Act & Assert
            progress.SuccessRate.Should().Be(75.0); // 15/20 = 75%
        }

        [Fact]
        public void SuccessRate_WhenAllSuccessful_ShouldReturn100()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                CompletedOperations = 30,
                SuccessfulOperations = 30
            };

            // Act & Assert
            progress.SuccessRate.Should().Be(100.0);
        }

        [Fact]
        public void SuccessRate_WhenNoSuccesses_ShouldReturnZero()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                CompletedOperations = 10,
                SuccessfulOperations = 0
            };

            // Act & Assert
            progress.SuccessRate.Should().Be(0.0);
        }

        #endregion

        #region Estimated End Time Tests

        [Fact]
        public void EstimatedEndTime_WhenNoOperationsCompleted_ShouldReturnNull()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 0
            };

            // Act & Assert
            progress.EstimatedEndTime.Should().BeNull();
        }

        [Fact]
        public void EstimatedEndTime_WhenNoTotalOperations_ShouldReturnNull()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 0,
                CompletedOperations = 5
            };

            // Act & Assert
            progress.EstimatedEndTime.Should().BeNull();
        }

        [Fact]
        public void EstimatedEndTime_ShouldCalculateBasedOnProgress()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddMinutes(-10); // Started 10 minutes ago
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 25, // 25% completed in 10 minutes
                StartTime = startTime
            };

            // Act
            var estimatedEnd = progress.EstimatedEndTime;

            // Assert
            estimatedEnd.Should().NotBeNull();
            // 25% took 10 minutes, so remaining 75% should take ~30 minutes
            // Total estimated time: 40 minutes from start
            var expectedEnd = startTime.AddMinutes(40);
            estimatedEnd.Should().BeCloseTo(expectedEnd, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public void EstimatedEndTime_WhenAllCompleted_ShouldReturnPastTime()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddMinutes(-5);
            var progress = new BulkOperationProgress
            {
                TotalOperations = 10,
                CompletedOperations = 10,
                StartTime = startTime
            };

            // Act
            var estimatedEnd = progress.EstimatedEndTime;

            // Assert
            estimatedEnd.Should().NotBeNull();
            estimatedEnd.Should().BeBefore(DateTime.UtcNow.AddMinutes(1));
        }

        #endregion

        #region Status Properties Tests

        [Fact]
        public void IsCompleted_WhenCompletedEqualsTotal_ShouldReturnTrue()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 50
            };

            // Act & Assert
            progress.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void IsCompleted_WhenCompletedExceedsTotal_ShouldReturnTrue()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 55
            };

            // Act & Assert
            progress.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void IsCompleted_WhenNotCompleted_ShouldReturnFalse()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 25
            };

            // Act & Assert
            progress.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void IsInProgress_WhenSomeCompleted_ShouldReturnTrue()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 25
            };

            // Act & Assert
            progress.IsInProgress.Should().BeTrue();
        }

        [Fact]
        public void IsInProgress_WhenNotStarted_ShouldReturnFalse()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 0
            };

            // Act & Assert
            progress.IsInProgress.Should().BeFalse();
        }

        [Fact]
        public void IsInProgress_WhenCompleted_ShouldReturnFalse()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 50
            };

            // Act & Assert
            progress.IsInProgress.Should().BeFalse();
        }

        #endregion

        #region Status Message Tests

        [Fact]
        public void StatusMessage_WhenCompleted_ShouldReturnCompletionMessage()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 100,
                SuccessfulOperations = 95
            };

            // Act
            var message = progress.StatusMessage;

            // Assert
            message.Should().Be("Ukończono: 95/100 operacji zakończonych sukcesem");
        }

        [Fact]
        public void StatusMessage_WhenInProgress_ShouldReturnProgressMessage()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 25
            };

            // Act
            var message = progress.StatusMessage;

            // Assert
            message.Should().Be("W trakcie: 25/100 (25.0%)");
        }

        [Fact]
        public void StatusMessage_WhenNotStarted_ShouldReturnWaitingMessage()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 0
            };

            // Act
            var message = progress.StatusMessage;

            // Assert
            message.Should().Be("Oczekuje: 100 operacji do wykonania");
        }

        [Fact]
        public void StatusMessage_WithZeroOperations_ShouldReturnWaitingMessage()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = 0,
                CompletedOperations = 0
            };

            // Act
            var message = progress.StatusMessage;

            // Assert
            message.Should().Be("Oczekuje: 0 operacji do wykonania");
        }

        #endregion

        #region Additional Info Tests

        [Fact]
        public void AdditionalInfo_ShouldAllowAddingCustomData()
        {
            // Arrange
            var progress = new BulkOperationProgress();
            var key = "CustomKey";
            var value = "CustomValue";

            // Act
            progress.AdditionalInfo[key] = value;

            // Assert
            progress.AdditionalInfo.Should().ContainKey(key);
            progress.AdditionalInfo[key].Should().Be(value);
        }

        [Fact]
        public void AdditionalInfo_ShouldSupportMultipleDataTypes()
        {
            // Arrange
            var progress = new BulkOperationProgress();

            // Act
            progress.AdditionalInfo["StringValue"] = "Test";
            progress.AdditionalInfo["IntValue"] = 42;
            progress.AdditionalInfo["DateValue"] = DateTime.UtcNow;
            progress.AdditionalInfo["BoolValue"] = true;

            // Assert
            progress.AdditionalInfo.Should().HaveCount(4);
            progress.AdditionalInfo["StringValue"].Should().Be("Test");
            progress.AdditionalInfo["IntValue"].Should().Be(42);
            progress.AdditionalInfo["DateValue"].Should().BeOfType<DateTime>();
            progress.AdditionalInfo["BoolValue"].Should().Be(true);
        }

        #endregion

        #region Edge Cases and Error Handling Tests

        [Fact]
        public void BulkOperationProgress_WithNegativeValues_ShouldHandleGracefully()
        {
            // Arrange
            var progress = new BulkOperationProgress
            {
                TotalOperations = -10,
                CompletedOperations = -5,
                SuccessfulOperations = -3,
                FailedOperations = -2
            };

            // Act & Assert - Should not throw exceptions
            var percentageComplete = progress.PercentageComplete;
            var successRate = progress.SuccessRate;
            var isCompleted = progress.IsCompleted;
            var isInProgress = progress.IsInProgress;
            var statusMessage = progress.StatusMessage;

            // Verify calculations work with negative values
            percentageComplete.Should().Be(50.0); // -5/-10 = 50%
            successRate.Should().Be(60.0); // -3/-5 = 60%
            isCompleted.Should().BeTrue(); // -5 >= -10
            isInProgress.Should().BeFalse(); // completed
        }

        [Fact]
        public void BulkOperationProgress_WithInconsistentData_ShouldHandleGracefully()
        {
            // Arrange - successful + failed != completed
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 50,
                SuccessfulOperations = 30,
                FailedOperations = 15 // 30 + 15 = 45, not 50
            };

            // Act & Assert - Should not throw exceptions
            var percentageComplete = progress.PercentageComplete;
            var successRate = progress.SuccessRate;
            var statusMessage = progress.StatusMessage;

            percentageComplete.Should().Be(50.0);
            successRate.Should().Be(60.0); // 30/50
            statusMessage.Should().Contain("W trakcie");
        }

        [Fact]
        public void EstimatedEndTime_WithVeryRecentStart_ShouldHandleGracefully()
        {
            // Arrange - started just now
            var progress = new BulkOperationProgress
            {
                TotalOperations = 100,
                CompletedOperations = 1,
                StartTime = DateTime.UtcNow.AddMilliseconds(-10) // 10ms ago
            };

            // Act
            var estimatedEnd = progress.EstimatedEndTime;

            // Assert - Should not be null and should be reasonable
            estimatedEnd.Should().NotBeNull();
            estimatedEnd.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public void StartTime_ShouldBeSetOnCreation()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var progress = new BulkOperationProgress();
            var afterCreation = DateTime.UtcNow;

            // Assert
            // Use OnOrAfter to handle the case where timing is exact
            progress.StartTime.Should().BeOnOrAfter(beforeCreation);
            progress.StartTime.Should().BeOnOrBefore(afterCreation);
        }

        #endregion

        #region Real-world Scenarios Tests

        [Fact]
        public void BulkOperationProgress_TypicalProgressScenario_ShouldWorkCorrectly()
        {
            // Arrange - Simulate a typical bulk operation progress
            var progress = new BulkOperationProgress
            {
                TotalOperations = 200,
                StartTime = DateTime.UtcNow.AddMinutes(-15)
            };

            // Act - Simulate progress updates
            progress.CompletedOperations = 50;
            progress.SuccessfulOperations = 45;
            progress.FailedOperations = 5;
            progress.CurrentOperation = "Creating team 'Mathematics 2024'";

            // Assert
            progress.PercentageComplete.Should().Be(25.0);
            progress.SuccessRate.Should().Be(90.0);
            progress.IsInProgress.Should().BeTrue();
            progress.IsCompleted.Should().BeFalse();
            progress.StatusMessage.Should().Contain("W trakcie: 50/200 (25.0%)");
            progress.EstimatedEndTime.Should().NotBeNull();
            progress.EstimatedEndTime.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public void BulkOperationProgress_CompletedScenario_ShouldWorkCorrectly()
        {
            // Arrange - Simulate completed operation
            var progress = new BulkOperationProgress
            {
                TotalOperations = 50,
                CompletedOperations = 50,
                SuccessfulOperations = 48,
                FailedOperations = 2,
                StartTime = DateTime.UtcNow.AddMinutes(-30)
            };

            // Act & Assert
            progress.PercentageComplete.Should().Be(100.0);
            progress.SuccessRate.Should().Be(96.0);
            progress.IsInProgress.Should().BeFalse();
            progress.IsCompleted.Should().BeTrue();
            progress.StatusMessage.Should().Be("Ukończono: 48/50 operacji zakończonych sukcesem");
        }

        #endregion
    }
} 