using System;

namespace Nethereum.TokenServices.ERC20.Pricing.Resilience
{
    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    public class CircuitBreaker
    {
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromSeconds(30);

        private int _consecutiveFailures;
        private DateTime _lastFailureTime;
        private CircuitState _state = CircuitState.Closed;
        private readonly object _lock = new object();

        public CircuitState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

        public int ConsecutiveFailures
        {
            get
            {
                lock (_lock)
                {
                    return _consecutiveFailures;
                }
            }
        }

        public static CircuitBreaker Default => new CircuitBreaker();

        public static CircuitBreaker Disabled => new CircuitBreaker { FailureThreshold = int.MaxValue };

        public bool AllowRequest()
        {
            lock (_lock)
            {
                switch (_state)
                {
                    case CircuitState.Closed:
                        return true;

                    case CircuitState.Open:
                        if (DateTime.UtcNow - _lastFailureTime > CooldownPeriod)
                        {
                            _state = CircuitState.HalfOpen;
                            return true;
                        }
                        return false;

                    case CircuitState.HalfOpen:
                        return true;

                    default:
                        return true;
                }
            }
        }

        public void RecordSuccess()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _state = CircuitState.Closed;
            }
        }

        public void RecordFailure()
        {
            lock (_lock)
            {
                _consecutiveFailures++;
                _lastFailureTime = DateTime.UtcNow;

                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Open;
                }
                else if (_consecutiveFailures >= FailureThreshold)
                {
                    _state = CircuitState.Open;
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _state = CircuitState.Closed;
            }
        }
    }
}
