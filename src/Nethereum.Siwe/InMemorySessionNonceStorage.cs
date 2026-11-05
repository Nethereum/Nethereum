using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nethereum.Siwe.Core;

namespace Nethereum.Siwe;

public class InMemorySessionNonceStorage : ISessionStorage
{
    public static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, KeyValuePair<DateTime, SiweMessage>> _messages = new();
    private readonly TimeSpan _sessionLifetime;
    private readonly long _pruneIntervalTicks;
    private long _nextPruneTicks;

    public InMemorySessionNonceStorage() : this(DefaultSessionLifetime)
    {
    }

    public InMemorySessionNonceStorage(TimeSpan sessionLifetime)
    {
        _sessionLifetime = sessionLifetime;
        _pruneIntervalTicks = Math.Min(TimeSpan.FromMinutes(1).Ticks, sessionLifetime.Ticks);
    }

    public void AddOrUpdate(SiweMessage siweMessage)
    {
        var now = DateTime.UtcNow;
        var nextPrune = Interlocked.Read(ref _nextPruneTicks);
        if (now.Ticks >= nextPrune &&
            Interlocked.CompareExchange(ref _nextPruneTicks, now.Ticks + _pruneIntervalTicks, nextPrune) == nextPrune)
        {
            foreach (var entry in _messages)
            {
                if (entry.Value.Key <= now) _messages.TryRemove(entry.Key, out _);
            }
        }

        var entryValue = new KeyValuePair<DateTime, SiweMessage>(GetExpiry(siweMessage, now), siweMessage);
        _messages.AddOrUpdate(siweMessage.Nonce, entryValue,
            (string nonce, KeyValuePair<DateTime, SiweMessage> old) => entryValue);
    }

    private DateTime GetExpiry(SiweMessage siweMessage, DateTime now)
    {
        if (!string.IsNullOrEmpty(siweMessage.ExpirationTime))
        {
            try
            {
                return siweMessage.GetExpirationTimeAsDateTime();
            }
            catch (FormatException)
            {
            }
        }

        return now.Add(_sessionLifetime);
    }

    public SiweMessage GetSiweMessage(SiweMessage siweMesage)
    {
        return _messages.TryGetValue(siweMesage.Nonce, out var stored) ? stored.Value : null;
    }

    public void Remove(SiweMessage siweMessage)
    {
        if (!_messages.TryRemove(siweMessage.Nonce, out _))
        {
            throw new Exception("Could not remove siwe message, try again");
        }
    }

    public void Remove(string nonce)
    {
        if (!_messages.TryRemove(nonce, out _))
        {
            throw new Exception("Could not remove siwe message, try again");
        }
    }
}