using System;

namespace Nethereum.DevP2P.Sync.Common
{
    public sealed class ProgressStallDetector
    {
        private readonly TimeSpan _threshold;
        private ulong _lastValue;
        private DateTimeOffset _lastAdvancedAt;
        private bool _seeded;

        public ProgressStallDetector(TimeSpan threshold) => _threshold = threshold;

        public bool Observe(ulong value, DateTimeOffset now)
        {
            if (!_seeded || value != _lastValue)
            {
                _seeded = true;
                _lastValue = value;
                _lastAdvancedAt = now;
                return false;
            }
            return now - _lastAdvancedAt >= _threshold;
        }

        public TimeSpan StalledFor(DateTimeOffset now) => _seeded ? now - _lastAdvancedAt : TimeSpan.Zero;
    }
}
