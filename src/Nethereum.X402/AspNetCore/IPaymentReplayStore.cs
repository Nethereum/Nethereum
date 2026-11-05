using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.X402.AspNetCore;

public interface IPaymentReplayStore
{
    Task<bool> TryReserveAsync(string paymentKey, TimeSpan retention, CancellationToken cancellationToken = default);

    Task ReleaseAsync(string paymentKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
