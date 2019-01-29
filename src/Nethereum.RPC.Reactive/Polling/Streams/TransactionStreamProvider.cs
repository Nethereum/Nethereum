using System;
using System.Reactive.Linq;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Streams;

namespace Nethereum.RPC.Reactive.Polling.Streams
{
    public sealed class TransactionStreamProvider : ITransactionStreamProvider
    {
        private readonly IBlockStreamProvider BlockStreamProvider;

        public TransactionStreamProvider(
            IBlockStreamProvider blockStreamProvider) =>
            BlockStreamProvider = blockStreamProvider;

        public IObservable<Transaction> GetTransactions() => BlockStreamProvider
            .GetBlocksWithTransactions()
            .SelectMany(block => block.Transactions)
            .Publish()
            .RefCount();

        public IObservable<Transaction> GetTransactions(BlockParameter start) => BlockStreamProvider
            .GetBlocksWithTransactions(start)
            .SelectMany(block => block.Transactions)
            .Publish()
            .RefCount();

        public IObservable<Transaction> GetTransactions(BlockParameter start, BlockParameter end) => BlockStreamProvider
            .GetBlocksWithTransactions(start, end)
            .SelectMany(block => block.Transactions)
            .Publish()
            .RefCount();
    }
}