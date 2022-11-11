using System;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Reactive.Streams
{
    public interface IPendingTransactionStreamProvider
    {
        IObservable<Transaction> GetPendingTransactions();
    }
}