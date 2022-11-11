using System;
using System.Reactive;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Polling.Streams;

namespace Nethereum.RPC.Reactive.Polling
{
    public static partial class Polling
    {
        public static IObservable<BlockWithTransactionHashes> GetBlocksWithTransactionHashes(this IEthApiService eth,
            IObservable<Unit> poller = null) => new BlockStreamProvider(
                poller ?? DefaultPoller,
                eth.Filters,
                eth.Blocks)
            .GetBlocksWithTransactionHashes();

        public static IObservable<BlockWithTransactionHashes> GetBlocksWithTransactionHashes(this IEthApiService eth,
            BlockParameter start,
            IObservable<Unit> poller = null) => new BlockStreamProvider(
                poller ?? DefaultPoller,
                eth.Filters,
                eth.Blocks)
            .GetBlocksWithTransactionHashes(start);

        public static IObservable<BlockWithTransactionHashes> GetBlocksWithTransactionHashes(this IEthApiService eth,
            BlockParameter start,
            BlockParameter end,
            IObservable<Unit> poller = null) => new BlockStreamProvider(
                poller ?? DefaultPoller,
                eth.Filters,
                eth.Blocks)
            .GetBlocksWithTransactionHashes(start, end);

        public static IObservable<BlockWithTransactions> GetBlocksWithTransactions(this IEthApiService eth,
            IObservable<Unit> poller = null) => new BlockStreamProvider(
                poller ?? DefaultPoller,
                eth.Filters,
                eth.Blocks)
            .GetBlocksWithTransactions();

        public static IObservable<BlockWithTransactions> GetBlocksWithTransactions(this IEthApiService eth,
            BlockParameter start,
            IObservable<Unit> poller = null) => new BlockStreamProvider(
                poller ?? DefaultPoller,
                eth.Filters,
                eth.Blocks)
            .GetBlocksWithTransactions(start);

        public static IObservable<BlockWithTransactions> GetBlocksWithTransactions(this IEthApiService eth,
            BlockParameter start,
            BlockParameter end,
            IObservable<Unit> poller = null) => new BlockStreamProvider(
                poller ?? DefaultPoller,
                eth.Filters,
                eth.Blocks)
            .GetBlocksWithTransactions(start, end);
    }
}