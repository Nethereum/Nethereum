using System;
using System.Reactive;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Polling.Streams;

namespace Nethereum.RPC.Reactive.Polling
{
    public static partial class Polling
    {
        public static IObservable<Transaction> GetPendingTransactions(this IEthApiService eth,
            IObservable<Unit> poller = null) => new PendingTransactionStreamProvider(
                poller ?? DefaultPoller,
                eth.Filters,
                eth.Transactions)
            .GetPendingTransactions();
    }
}