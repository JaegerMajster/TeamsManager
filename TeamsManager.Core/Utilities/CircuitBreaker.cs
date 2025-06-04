using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsManager.Core.Utilities
{
    public class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly TimeSpan _samplingDuration;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitState _state = CircuitState.Closed;
        private DateTime _openedAt;

        public CircuitBreaker(int failureThreshold, TimeSpan openDuration, TimeSpan samplingDuration)
        {
            _failureThreshold = failureThreshold;
            _openDuration = openDuration;
            _samplingDuration = samplingDuration;
        }

        public CircuitState State => _state;

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_state == CircuitState.Open)
                {
                    if (DateTime.UtcNow - _openedAt < _openDuration)
                    {
                        throw new CircuitBreakerOpenException($"Circuit breaker is open. Will retry after {_openedAt.Add(_openDuration):HH:mm:ss}");
                    }
                    _state = CircuitState.HalfOpen;
                }

                try
                {
                    var result = await operation();
                    OnSuccess();
                    return result;
                }
                catch (Exception)
                {
                    OnFailure();
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void OnSuccess()
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }

        private void OnFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }

        public void Reset()
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }
} 