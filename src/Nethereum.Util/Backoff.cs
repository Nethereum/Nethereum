using System;

namespace Nethereum.Util
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "util-backoff", "Backoff")]
    public sealed class Backoff
    {
        private readonly int _initialMs;

        public Backoff(int initialMs)
        {
            if (initialMs <= 0) throw new ArgumentOutOfRangeException(nameof(initialMs));
            _initialMs = initialMs;
            Current = initialMs;
        }

        public int Current { get; private set; }

        public void Advance(int capMs)
        {
            Current = Math.Min(Current * 2, capMs);
        }

        public void Reset()
        {
            Current = _initialMs;
        }
    }
}
