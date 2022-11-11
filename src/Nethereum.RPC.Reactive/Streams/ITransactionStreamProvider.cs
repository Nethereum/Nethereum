using System;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Reactive.Streams
{
    public interface ITransactionStreamProvider
    {
        IObservable<Transaction> GetTransactions();

        IObservable<Transaction> GetTransactions(
            BlockParameter start);

        IObservable<Transaction> GetTransactions(
            BlockParameter start,
            BlockParameter end);
    }
}