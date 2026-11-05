using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.X402.AspNetCore;

public sealed class InMemoryPaymentReplayStore : IPaymentReplayStore
{
    public const int DefaultMaxEntries = 100_000;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _reservations = new();
    private readonly object _gate = new();
    private long _lastSweepTicks;
    private long _lastFullSweepTicks;

    public InMemoryPaymentReplayStore()
        : this(DefaultMaxEntries)
    {
    }

    public InMemoryPaymentReplayStore(int maxEntries)
    {
        if (maxEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "MaxEntries must be at least 1");

        MaxEntries = maxEntries;
    }

    public int MaxEntries { get; }

    public Task<bool> TryReserveAsync(string paymentKey, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            SweepExpired(now, ref _lastSweepTicks, TimeSpan.TicksPerMinute);

            var expiry = retention > DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now.Add(retention);

            if (_reservations.TryGetValue(paymentKey, out var existing))
            {
                if (existing > now)
                    return Task.FromResult(false);

                _reservations[paymentKey] = expiry;
                return Task.FromResult(true);
            }

            if (_reservations.Count >= MaxEntries)
            {
                SweepExpired(now, ref _lastFullSweepTicks, TimeSpan.TicksPerSecond);
                if (_reservations.Count >= MaxEntries)
                    throw new PaymentReplayStoreFullException(MaxEntries);
            }

            _reservations[paymentKey] = expiry;
            return Task.FromResult(true);
        }
    }

    public Task ReleaseAsync(string paymentKey, CancellationToken cancellationToken = default)
    {
        _reservations.TryRemove(paymentKey, out _);
        return Task.CompletedTask;
    }

    private void SweepExpired(DateTimeOffset now, ref long lastSweepTicks, long minimumIntervalTicks)
    {
        if (now.UtcTicks - lastSweepTicks < minimumIntervalTicks)
            return;

        lastSweepTicks = now.UtcTicks;
        foreach (var pair in _reservations)
        {
            if (pair.Value <= now)
                _reservations.TryRemove(pair.Key, out _);
        }
    }
}
