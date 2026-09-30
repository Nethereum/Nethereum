using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.X402.AspNetCore;

public sealed class InMemoryPaymentReplayStore : IPaymentReplayStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _reservations = new();
    private long _lastSweepTicks;

    public Task<bool> TryReserveAsync(string paymentKey, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        SweepExpired(now);

        var expiry = now.Add(retention);

        if (_reservations.TryAdd(paymentKey, expiry))
            return Task.FromResult(true);

        var existing = _reservations.GetOrAdd(paymentKey, expiry);
        if (existing > now)
            return Task.FromResult(false);

        _reservations[paymentKey] = expiry;
        return Task.FromResult(true);
    }

    private void SweepExpired(DateTimeOffset now)
    {
        var last = Interlocked.Read(ref _lastSweepTicks);
        if (now.UtcTicks - last < TimeSpan.TicksPerMinute)
            return;
        if (Interlocked.CompareExchange(ref _lastSweepTicks, now.UtcTicks, last) != last)
            return;

        foreach (var pair in _reservations)
        {
            if (pair.Value <= now)
                _reservations.TryRemove(pair.Key, out _);
        }
    }
}
