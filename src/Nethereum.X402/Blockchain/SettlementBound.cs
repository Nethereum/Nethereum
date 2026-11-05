using System;
using System.Threading;

namespace Nethereum.X402.Blockchain;

internal sealed class SettlementBound : CancellationTokenSource
{
    private int _started;

    public SettlementBound(TimeSpan duration)
    {
        Duration = duration;
    }

    public TimeSpan Duration { get; }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            CancelAfter(Duration);
    }
}
