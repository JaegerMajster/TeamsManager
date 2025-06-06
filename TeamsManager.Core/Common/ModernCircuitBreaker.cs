using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TeamsManager.Core.Common
{
    /// <summary>
    /// Nowoczesny Circuit Breaker kompatybilny z Microsoft.Extensions.Http.Resilience
    /// Współpracuje z HTTP Resilience zamiast zastępować go
    /// </summary>
    public class ModernCircuitBreaker
    {
        private readonly ILogger<ModernCircuitBreaker> _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitState _state = CircuitState.Closed;
        private DateTime _openedAt;

        // Eventy dla monitorowania
        public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;
        public event EventHandler<CircuitBreakerFailureEventArgs>? FailureRecorded;

        public ModernCircuitBreaker(
            int failureThreshold = 5, 
            TimeSpan? openDuration = null,
            ILogger<ModernCircuitBreaker>? logger = null)
        {
            _failureThreshold = failureThreshold;
            _openDuration = openDuration ?? TimeSpan.FromMinutes(1);
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ModernCircuitBreaker>.Instance;
        }

        public CircuitState State => _state;
        public int FailureCount => _failureCount;
        public bool IsOpen => _state == CircuitState.Open;
        public bool IsClosed => _state == CircuitState.Closed;
        public bool IsHalfOpen => _state == CircuitState.HalfOpen;

        /// <summary>
        /// Wykonuje operację z dodatkową ochroną Circuit Breaker (współpracuje z HTTP Resilience)
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string? operationName = null)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_state == CircuitState.Open)
                {
                    if (DateTime.UtcNow - _openedAt < _openDuration)
                    {
                        _logger.LogWarning("Circuit breaker is open for operation: {OperationName}. Will retry after {RetryTime}",
                            operationName ?? "Unknown", _openedAt.Add(_openDuration));
                        throw new CircuitBreakerOpenException($"Circuit breaker is open. Will retry after {_openedAt.Add(_openDuration):HH:mm:ss}");
                    }
                    
                    var oldState = _state;
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation("Circuit breaker transitioning from Open to HalfOpen for operation: {OperationName}", operationName ?? "Unknown");
                    StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
                }

                try
                {
                    var result = await operation();
                    OnSuccess(operationName);
                    return result;
                }
                catch (Exception ex)
                {
                    OnFailure(ex, operationName);
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Rejestruje sukces operacji
        /// </summary>
        public void RecordSuccess(string? operationName = null)
        {
            OnSuccess(operationName);
        }

        /// <summary>
        /// Rejestruje niepowodzenie operacji
        /// </summary>
        public void RecordFailure(Exception? exception = null, string? operationName = null)
        {
            OnFailure(exception, operationName);
        }

        private void OnSuccess(string? operationName = null)
        {
            var oldState = _state;
            _failureCount = 0;
            _state = CircuitState.Closed;
            
            if (oldState != _state)
            {
                _logger.LogInformation("Circuit breaker closed successfully for operation: {OperationName}", operationName ?? "Unknown");
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
            }
        }

        private void OnFailure(Exception? exception = null, string? operationName = null)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            
            _logger.LogWarning(exception, "Circuit breaker recorded failure #{FailureCount} for operation: {OperationName}", 
                _failureCount, operationName ?? "Unknown");
            
            FailureRecorded?.Invoke(this, new CircuitBreakerFailureEventArgs(_failureCount, _failureThreshold));

            if (_failureCount >= _failureThreshold)
            {
                var oldState = _state;
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
                
                if (oldState != _state)
                {
                    _logger.LogError("Circuit breaker opened due to {FailureCount} failures for operation: {OperationName}. Will remain open until {OpenUntil}",
                        _failureCount, operationName ?? "Unknown", _openedAt.Add(_openDuration));
                    StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
                }
            }
        }

        public void Reset()
        {
            var oldState = _state;
            _failureCount = 0;
            _state = CircuitState.Closed;
            
            _logger.LogInformation("Circuit breaker manually reset");
            
            if (oldState != _state)
            {
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
            }
        }

        /// <summary>
        /// Zwraca informacje o stanie Circuit Breaker
        /// </summary>
        public CircuitBreakerStats GetStats()
        {
            return new CircuitBreakerStats
            {
                State = _state,
                FailureCount = _failureCount,
                FailureThreshold = _failureThreshold,
                LastFailureTime = _lastFailureTime,
                OpenedAt = _openedAt,
                WillRetryAt = _state == CircuitState.Open ? _openedAt.Add(_openDuration) : null
            };
        }
    }

    /// <summary>
    /// Statystyki Circuit Breaker
    /// </summary>
    public class CircuitBreakerStats
    {
        public CircuitState State { get; set; }
        public int FailureCount { get; set; }
        public int FailureThreshold { get; set; }
        public DateTime LastFailureTime { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? WillRetryAt { get; set; }
    }
} 