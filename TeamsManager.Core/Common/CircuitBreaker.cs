using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsManager.Core.Common
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

        // Nowe eventy
        public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;
        public event EventHandler<CircuitBreakerFailureEventArgs>? FailureRecorded;

        public CircuitBreaker(int failureThreshold, TimeSpan openDuration, TimeSpan samplingDuration)
        {
            _failureThreshold = failureThreshold;
            _openDuration = openDuration;
            _samplingDuration = samplingDuration;
        }

        public CircuitState State => _state;
        public int FailureCount => _failureCount;

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
                    
                    var oldState = _state;
                    _state = CircuitState.HalfOpen;
                    StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
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
            var oldState = _state;
            _failureCount = 0;
            _state = CircuitState.Closed;
            
            if (oldState != _state)
            {
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
            }
        }

        private void OnFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            
            FailureRecorded?.Invoke(this, new CircuitBreakerFailureEventArgs(_failureCount, _failureThreshold));

            if (_failureCount >= _failureThreshold)
            {
                var oldState = _state;
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
                
                if (oldState != _state)
                {
                    StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
                }
            }
        }

        public void Reset()
        {
            var oldState = _state;
            _failureCount = 0;
            _state = CircuitState.Closed;
            
            if (oldState != _state)
            {
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(oldState, _state));
            }
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

    public class CircuitBreakerStateChangedEventArgs : EventArgs
    {
        public CircuitState OldState { get; }
        public CircuitState NewState { get; }
        public DateTime Timestamp { get; }
        
        public CircuitBreakerStateChangedEventArgs(CircuitState oldState, CircuitState newState)
        {
            OldState = oldState;
            NewState = newState;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class CircuitBreakerFailureEventArgs : EventArgs
    {
        public int CurrentFailureCount { get; }
        public int Threshold { get; }
        public DateTime Timestamp { get; }
        
        public CircuitBreakerFailureEventArgs(int currentFailureCount, int threshold)
        {
            CurrentFailureCount = currentFailureCount;
            Threshold = threshold;
            Timestamp = DateTime.UtcNow;
        }
    }
} 