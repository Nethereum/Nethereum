using System;
using System.Reactive.Linq;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Streams;

namespace Nethereum.RPC.Reactive.Polling.Streams
{
    public sealed class TransactionStreamProvider : ITransactionStreamProvider
    {
        private readonly IBlockStreamProvider _blockStreamProvider;

        public TransactionStreamProvider(
            IBlockStreamProvider blockStreamProvider) =>
            _blockStreamProvider = blockStreamProvider;

        public IObservable<Transaction> GetTransactions() => _blockStreamProvider
            .GetBlocksWithTransactions()
            .SelectMany(block => block.Transactions)
            .Publish()
            .RefCount();

        public IObservable<Transaction> GetTransactions(BlockParameter start) => _blockStreamProvider
            .GetBlocksWithTransactions(start)
            .SelectMany(block => block.Transactions)
            .Publish()
            .RefCount();

        public IObservable<Transaction> GetTransactions(BlockParameter start, BlockParameter end) => _blockStreamProvider
            .GetBlocksWithTransactions(start, end)
            .SelectMany(block => block.Transactions)
            .Publish()
            .RefCount();
    }
}