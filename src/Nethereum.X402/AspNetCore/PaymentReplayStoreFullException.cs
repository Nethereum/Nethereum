using System;

namespace Nethereum.X402.AspNetCore;

public sealed class PaymentReplayStoreFullException : InvalidOperationException
{
    public PaymentReplayStoreFullException(int maxEntries)
        : base($"The payment replay store holds its maximum of {maxEntries} live reservations.")
    {
        MaxEntries = maxEntries;
    }

    public int MaxEntries { get; }
}
