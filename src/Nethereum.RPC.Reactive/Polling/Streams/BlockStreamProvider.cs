using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Reactive.Streams;

namespace Nethereum.RPC.Reactive.Polling.Streams
{
    public sealed class BlockStreamProvider : IBlockStreamProvider
    {
        private readonly IEthApiBlockService BlockService;
        private readonly IEthApiFilterService FilterService;
        private readonly IObservable<Unit> Poller;

        public BlockStreamProvider(
            IObservable<Unit> poller,
            IEthApiFilterService filterService,
            IEthApiBlockService blockService)
        {
            Poller = poller;
            FilterService = filterService;
            BlockService = blockService;
        }

        public IObservable<BlockWithTransactionHashes> GetBlocksWithTransactionHashes() => GetBlocks(BlockService.GetBlockWithTransactionsHashesByHash.SendRequestAsync);
        public IObservable<BlockWithTransactions> GetBlocksWithTransactions() => GetBlocks(BlockService.GetBlockWithTransactionsByHash.SendRequestAsync);

        private IObservable<TBlock> GetBlocks<TBlock>(Func<string, object, Task<TBlock>> blockProvider) where TBlock : Block => ObservableExtensions
            .Using(
                async () => new DisposableFilter(await FilterService.NewBlockFilter.SendRequestAsync(), FilterService.UninstallFilter),
                filter => Observable
                    .FromAsync(ct => FilterService.GetFilterChangesForBlockOrTransaction.SendRequestAsync(filter.ID))
                    .SelectMany(blockHashes => blockHashes
                        .Select(hash => Observable.FromAsync(ct => blockProvider(hash, null)))
                        .ToObservable())
                    .SelectMany(x => x)
                    .Poll(Poller))
            .Publish()
            .RefCount();

        private IObservable<TBlock> GetBlocks<TBlock>(
            BlockParameter start,
            BlockParameter end,
            IObservable<TBlock> blockSource,
            Func<HexBigInteger, object, Task<TBlock>> blockProvider) where TBlock : Block =>
            Observable
                .Create<TBlock>(async o =>
                {
                    // Already start looking for updates to make up for the time
                    // that we're using to fetch the old stream.
                    var updateStream = blockSource.Replay();
                    var updateStreamDisposable = updateStream.Connect();

                    var latestBlock = await BlockService.GetBlockNumber.SendRequestAsync();

                    // If we don't defer, we have to fetch the entire old stream before we are able
                    // to detect and respond whether the `updateStream` is required or not.
                    var oldStream = Observable.Defer(() =>
                        EnumerableExtensions
                            .Range(start.BlockNumber, latestBlock.Value - start.BlockNumber)
                            .Select(block => Observable
                                .FromAsync(ct => blockProvider(new HexBigInteger(block), null)))
                            .Merge());

                    // If we were given an 'end' parameter that is smaller or equal to the latest
                    // block, we're really just done here and can safely return.
                    var bHasEndBlock = end.ParameterType == BlockParameter.BlockParameterType.blockNumber;
                    var bOldStreamOnly = bHasEndBlock && end.BlockNumber.Value <= latestBlock.Value;
                    if (bOldStreamOnly)
                    {
                        updateStreamDisposable.Dispose();
                        return oldStream.Subscribe(o);
                    }

                    // Due to all the asynchonous operations, the updateStream may contain blocks
                    // that are also in the old stream we're about to retrieve.
                    var filteredUpdateStream = updateStream.Where(block => block.Number >= latestBlock.Value);

                    // In case that we're given an end parameter, we have to end the updateStream
                    // in case we've reached it.
                    if (bHasEndBlock)
                        filteredUpdateStream = filteredUpdateStream.TakeUntil(block => block.Number <= end.BlockNumber.Value);

                    // Combine the stream of older blocks with the updated (and filtered) stream.
                    var finalStream = oldStream
                        .Concat(filteredUpdateStream)
                        .Subscribe(o);

                    return new CompositeDisposable
                    {
                        updateStreamDisposable,
                        finalStream
                    };
                })
                .Publish()
                .RefCount();

        public IObservable<BlockWithTransactionHashes> GetBlocksWithTransactionHashes(
            BlockParameter start,
            IObservable<BlockWithTransactionHashes> newBlockSource = null)
            => GetBlocks(
                start,
                BlockParameter.CreateLatest(),
                newBlockSource ?? GetBlocksWithTransactionHashes(),
                BlockService.GetBlockWithTransactionsHashesByNumber.SendRequestAsync);

        public IObservable<BlockWithTransactionHashes> GetBlocksWithTransactionHashes(
            BlockParameter start,
            BlockParameter end,
            IObservable<BlockWithTransactionHashes> newBlockSource = null)
            => GetBlocks(
                start,
                end,
                newBlockSource ?? GetBlocksWithTransactionHashes(),
                BlockService.GetBlockWithTransactionsHashesByNumber.SendRequestAsync);

        public IObservable<BlockWithTransactions> GetBlocksWithTransactions(
            BlockParameter start,
            IObservable<BlockWithTransactions> newBlockSource = null)
            => GetBlocks(
                start,
                BlockParameter.CreateLatest(),
                newBlockSource ?? GetBlocksWithTransactions(),
                BlockService.GetBlockWithTransactionsByNumber.SendRequestAsync);

        public IObservable<BlockWithTransactions> GetBlocksWithTransactions(
            BlockParameter start,
            BlockParameter end,
            IObservable<BlockWithTransactions> newBlockSource = null)
            => GetBlocks(
                start,
                end,
                newBlockSource ?? GetBlocksWithTransactions(),
                BlockService.GetBlockWithTransactionsByNumber.SendRequestAsync);
    }
}