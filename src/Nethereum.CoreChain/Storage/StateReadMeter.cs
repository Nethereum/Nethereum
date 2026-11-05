using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateReadMeterHost
    {
        bool TryAttachReadMeter(StateReadMeter meter);
        void DetachReadMeter(StateReadMeter meter);
    }

    public sealed class StateReadMeter
    {
        private static readonly AsyncLocal<bool> Suppressed = new AsyncLocal<bool>();

        public static bool ReadsSuppressed => Suppressed.Value;

        public static IDisposable SuppressReads()
        {
            var previous = Suppressed.Value;
            Suppressed.Value = true;
            return new Restore(previous);
        }

        private sealed class Restore : IDisposable
        {
            private readonly bool _previous;
            public Restore(bool previous) { _previous = previous; }
            public void Dispose() => Suppressed.Value = _previous;
        }

        private readonly ConcurrentDictionary<EvmAddress, byte> _accounts = new();
        private readonly ConcurrentDictionary<(EvmAddress Address, BigInteger Slot), byte> _slots = new();
        private readonly ConcurrentDictionary<string, byte> _code = new();
        private long _accountTicks;
        private long _storageTicks;
        private long _codeTicks;
        private int _codeLoadBytes;
        private int _codeUpdated;
        private int _codeUpdateBytes;

        public TimeSpan AccountReads => ToTimeSpan(Interlocked.Read(ref _accountTicks));
        public TimeSpan StorageReads => ToTimeSpan(Interlocked.Read(ref _storageTicks));
        public TimeSpan CodeReads => ToTimeSpan(Interlocked.Read(ref _codeTicks));
        public int CodeLoadBytes => Volatile.Read(ref _codeLoadBytes);
        public int CodeUpdated => Volatile.Read(ref _codeUpdated);
        public int CodeUpdateBytes => Volatile.Read(ref _codeUpdateBytes);

        public int AccountLoaded => _accounts.Count;
        public int StorageLoaded => _slots.Count;
        public int CodeLoaded => _code.Count;

        internal void AddAccountRead(long ticks, EvmAddress address)
        {
            if (ReadsSuppressed) return;
            Interlocked.Add(ref _accountTicks, ticks);
            _accounts.TryAdd(address, 0);
        }

        internal void AddStorageRead(long ticks, EvmAddress address, BigInteger slot)
        {
            if (ReadsSuppressed) return;
            Interlocked.Add(ref _storageTicks, ticks);
            _slots.TryAdd((address, slot), 0);
        }

        internal void AddCodeRead(long ticks, byte[] codeHash, byte[] code)
        {
            if (ReadsSuppressed) return;
            Interlocked.Add(ref _codeTicks, ticks);
            if (code == null || code.Length == 0 || codeHash == null) return;
            if (_code.TryAdd(Convert.ToHexString(codeHash), 0)) Interlocked.Add(ref _codeLoadBytes, code.Length);
        }

        internal void AddCodeWrite(byte[] code)
        {
            if (ReadsSuppressed) return;
            if (code == null || code.Length == 0) return;
            Interlocked.Increment(ref _codeUpdated);
            Interlocked.Add(ref _codeUpdateBytes, code.Length);
        }

        private static TimeSpan ToTimeSpan(long ticks)
            => TimeSpan.FromTicks((long)(ticks * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
    }
}
