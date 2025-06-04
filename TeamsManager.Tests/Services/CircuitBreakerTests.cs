using System;
using System.Threading.Tasks;
using FluentAssertions;
using TeamsManager.Core.Common;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class CircuitBreakerTests
    {
        private readonly CircuitBreaker _circuitBreaker;

        public CircuitBreakerTests()
        {
            // Konfiguracja Circuit Breaker dla testów
            _circuitBreaker = new CircuitBreaker(
                failureThreshold: 3,
                openDuration: TimeSpan.FromSeconds(1),
                samplingDuration: TimeSpan.FromSeconds(10)
            );
        }

        [Fact]
        public async Task ExecuteAsync_WhenOperationSucceeds_ShouldReturnResult()
        {
            // Arrange
            var expectedResult = "success";

            // Act
            var result = await _circuitBreaker.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                return expectedResult;
            });

            // Assert
            result.Should().Be(expectedResult);
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
            _circuitBreaker.FailureCount.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOperationFails_ShouldIncreaseFailureCount()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test failure");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _circuitBreaker.ExecuteAsync<string>(async () =>
                {
                    await Task.Delay(10);
                    throw expectedException;
                });
            });

            // Assert
            _circuitBreaker.FailureCount.Should().Be(1);
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public async Task ExecuteAsync_WhenThresholdReached_ShouldOpenCircuit()
        {
            // Arrange - cause failures to reach threshold
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(async () =>
                    {
                        await Task.Delay(10);
                        throw new InvalidOperationException("Test failure");
                    });
                }
                catch (InvalidOperationException)
                {
                    // Oczekiwane wyjątki
                }
            }

            // Act & Assert - circuit should be open
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
            {
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    await Task.Delay(10);
                    return "should not execute";
                });
            });

            _circuitBreaker.State.Should().Be(CircuitState.Open);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCircuitOpenAndTimeoutPassed_ShouldTransitionToHalfOpen()
        {
            // Arrange - open circuit
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(() => throw new Exception("Failure"));
                }
                catch { }
            }

            _circuitBreaker.State.Should().Be(CircuitState.Open);

            // Wait for open duration to pass
            await Task.Delay(TimeSpan.FromSeconds(1.1));

            // Act - first call should transition to half-open
            var result = await _circuitBreaker.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                return "half-open success";
            });

            // Assert
            result.Should().Be("half-open success");
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
            _circuitBreaker.FailureCount.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_WhenInHalfOpenAndOperationFails_ShouldReopenCircuit()
        {
            // Arrange - open circuit
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(() => throw new Exception("Failure"));
                }
                catch { }
            }

            // Wait for timeout
            await Task.Delay(TimeSpan.FromSeconds(1.1));

            // Act - fail in half-open state
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _circuitBreaker.ExecuteAsync<string>(async () =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException("Half-open failure");
                });
            });

            // Assert
            _circuitBreaker.State.Should().Be(CircuitState.Open);
        }

        [Fact]
        public async Task Reset_ShouldResetFailureCountAndCloseCircuit()
        {
            // Arrange - simulate some failures
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(() => throw new Exception());
                }
                catch { }
            }

            _circuitBreaker.FailureCount.Should().Be(2);

            // Act
            _circuitBreaker.Reset();

            // Assert
            _circuitBreaker.FailureCount.Should().Be(0);
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public async Task ExecuteAsync_WithMultipleSuccessesAfterFailures_ShouldKeepCircuitClosed()
        {
            // Arrange - cause some failures but not enough to open
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(() => throw new Exception("Failure"));
                }
                catch { }
            }

            _circuitBreaker.FailureCount.Should().Be(2);
            _circuitBreaker.State.Should().Be(CircuitState.Closed);

            // Act - succeed to reset failure count
            var result = await _circuitBreaker.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                return "success after failures";
            });

            // Assert
            result.Should().Be("success after failures");
            _circuitBreaker.FailureCount.Should().Be(0);
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public async Task StateChanged_Event_ShouldBeFiredOnStateTransitions()
        {
            // Arrange
            var stateChanges = new List<(CircuitState oldState, CircuitState newState)>();
            _circuitBreaker.StateChanged += (sender, args) =>
            {
                stateChanges.Add((args.OldState, args.NewState));
            };

            // Act - cause failures to open circuit
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(() => throw new Exception("Failure"));
                }
                catch { }
            }

            // Wait for timeout and succeed to close
            await Task.Delay(TimeSpan.FromSeconds(1.1));
            await _circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));

            // Assert
            stateChanges.Should().HaveCount(2);
            stateChanges[0].Should().Be((CircuitState.Closed, CircuitState.Open));
            stateChanges[1].Should().Be((CircuitState.HalfOpen, CircuitState.Closed));
        }

        [Fact]
        public async Task FailureRecorded_Event_ShouldBeFiredOnFailures()
        {
            // Arrange
            var failureEvents = new List<(int currentCount, int threshold)>();
            _circuitBreaker.FailureRecorded += (sender, args) =>
            {
                failureEvents.Add((args.CurrentFailureCount, args.Threshold));
            };

            // Act - cause some failures
            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(() => throw new Exception($"Failure {i}"));
                }
                catch { }
            }

            // Assert
            failureEvents.Should().HaveCount(3);
            failureEvents[0].Should().Be((1, 3));
            failureEvents[1].Should().Be((2, 3));
            failureEvents[2].Should().Be((3, 3));
        }

        [Fact]
        public async Task ExecuteAsync_ConcurrentExecution_ShouldBeSafe()
        {
            // Arrange
            var tasks = new List<Task<string>>();

            // Act - wykonaj wiele operacji równolegle
            for (int i = 0; i < 10; i++)
            {
                var taskId = i;
                tasks.Add(_circuitBreaker.ExecuteAsync(async () =>
                {
                    await Task.Delay(50);
                    return $"result-{taskId}";
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(10);
            results.Should().OnlyContain(r => r.StartsWith("result-"));
            _circuitBreaker.State.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public async Task ExecuteAsync_WithDifferentExceptionTypes_ShouldTreatAllAsFailures()
        {
            // Arrange & Act
            var exceptions = new Exception[]
            {
                new InvalidOperationException("Error 1"),
                new ArgumentException("Error 2"),
                new TimeoutException("Error 3")
            };

            for (int i = 0; i < exceptions.Length; i++)
            {
                try
                {
                    await _circuitBreaker.ExecuteAsync<string>(() => throw exceptions[i]);
                }
                catch { }
            }

            // Assert
            _circuitBreaker.FailureCount.Should().Be(3);
            _circuitBreaker.State.Should().Be(CircuitState.Open);
        }

        [Fact]
        public async Task Constructor_WithInvalidParameters_ShouldHandleGracefully()
        {
            // Arrange & Act
            var cb1 = new CircuitBreaker(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            var cb2 = new CircuitBreaker(1, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            // Assert - should not throw and should work with minimum values
            var result1 = await cb1.ExecuteAsync(() => Task.FromResult("test"));
            var result2 = await cb2.ExecuteAsync(() => Task.FromResult("test"));

            result1.Should().Be("test");
            result2.Should().Be("test");
        }
    }
} 