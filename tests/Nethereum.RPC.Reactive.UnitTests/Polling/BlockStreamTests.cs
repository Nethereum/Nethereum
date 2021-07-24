using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using Microsoft.Reactive.Testing;
using Moq;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Reactive.Polling.Streams;
using Xunit;

namespace Nethereum.RPC.Reactive.UnitTests.Polling
{
    public class BlockStreamTests : ReactiveTest
    {
        [Fact]
        public void GetBlocksWithTransaction_Start()
        {
            // The test works as following:
            // There are already 2 blocks. The method then gets called with
            // a `from` value of 1, and while doing so `latestBlock` becomes
            // 2 because another `updateStream` received a new block.
            // This means that the old stream should consist of blocks 1 and 2,
            // and for completeness we assume a new block number 3 comes afterwards.

            // Setup.
            var sched = new TestScheduler();

            var poller = new Mock<IObservable<Unit>>();
            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller.Object,
                filterService.Object,
                blockService.Object);

            blockService
                .Setup(x => x.GetBlockNumber.SendRequestAsync(null))
                .Returns(Task.FromResult(new HexBigInteger(2)));

            // Setup old blocks.
            var oldBlock = new BlockWithTransactions
            {
                BlockHash = "0x1",
                Number = new HexBigInteger(1),
                Transactions = new Transaction[0]
            };

            blockService
                .Setup(x => x.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(new BigInteger(1)), null))
                .Returns(Task.FromResult(oldBlock));

            // Setup new blocks.
            var newBlocks = new[]
            {
                new BlockWithTransactions
                {
                    BlockHash = "0x2",
                    Number = new HexBigInteger(2),
                    Transactions = new Transaction[0]
                },
                new BlockWithTransactions
                {
                    BlockHash = "0x3",
                    Number = new HexBigInteger(3),
                    Transactions = new Transaction[0]
                }
            };

            var newBlockSource = sched.CreateHotObservable(
                OnNext(100, newBlocks[0]),
                OnNext(200, newBlocks[1]));

            // Record incoming data.
            var res = sched.Start(
                () => provider.GetBlocksWithTransactions(new BlockParameter(1), newBlockSource),
                0,
                50,
                Disposed);

            res.Messages.AssertEqual(
                OnNext(50, oldBlock),
                OnNext(100, newBlocks[0]),
                OnNext(200, newBlocks[1]));
        }

        [Fact]
        public void GetBlocksWithTransaction_StartEndOldAndNew()
        {
            // Setup.
            var sched = new TestScheduler();

            var poller = new Mock<IObservable<Unit>>();
            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller.Object,
                filterService.Object,
                blockService.Object);

            // Setup old blocks (first should be ignored).
            var oldBlocks = new List<BlockWithTransactions>();
            for (var i = 0; i < 4; i++)
            {
                oldBlocks.Add(new BlockWithTransactions
                {
                    BlockHash = "0x" + i,
                    Number = new HexBigInteger(i),
                    Transactions = new Transaction[0]
                });

                blockService
                    .Setup(x => x.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(new BigInteger(i)), null))
                    .Returns(Task.FromResult(oldBlocks[i]));
            }

            blockService
                .Setup(x => x.GetBlockNumber.SendRequestAsync(null))
                .Returns(Task.FromResult(new HexBigInteger(oldBlocks.Last().Number)));

            // Setup new blocks (last one should be ignored).
            var newBlocks = new[]
            {
                new BlockWithTransactions
                {
                    BlockHash = "0x4",
                    Number = new HexBigInteger(4),
                    Transactions = new Transaction[0]
                },
                new BlockWithTransactions
                {
                    BlockHash = "0x5",
                    Number = new HexBigInteger(5),
                    Transactions = new Transaction[0]
                }
            };

            var newBlockSource = sched.CreateHotObservable(
                OnNext(100, newBlocks[0]),
                OnNext(100, newBlocks[1]));

            // Record incoming data.
            var res = sched.Start(
                () => provider.GetBlocksWithTransactions(
                    new BlockParameter(1),
                    new BlockParameter(4),
                    newBlockSource),
                0,
                50,
                Disposed);

            res.Messages.AssertEqual(
                OnNext(50, oldBlocks[1]),
                OnNext(50, oldBlocks[2]),
                OnNext(100, newBlocks[0]),
                OnCompleted<BlockWithTransactions>(100));
        }

        [Fact]
        public void GetBlocksWithTransaction_StartEndOldOnly()
        {
            // Setup.
            var sched = new TestScheduler();

            var poller = new Mock<IObservable<Unit>>();
            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller.Object,
                filterService.Object,
                blockService.Object);

            // Setup old blocks (first should be ignored).
            var oldBlocks = new List<BlockWithTransactions>();
            for (var i = 0; i < 4; i++)
            {
                oldBlocks.Add(new BlockWithTransactions
                {
                    BlockHash = "0x" + i,
                    Number = new HexBigInteger(i),
                    Transactions = new Transaction[0]
                });

                blockService
                    .Setup(x => x.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(new BigInteger(i)), null))
                    .Returns(Task.FromResult(oldBlocks[i]));
            }

            blockService
                .Setup(x => x.GetBlockNumber.SendRequestAsync(null))
                .Returns(Task.FromResult(new HexBigInteger(oldBlocks.Last().Number)));

            // Setup new blocks (should all be ignored).
            var newBlocks = new[]
            {
                new BlockWithTransactions
                {
                    BlockHash = "0x4",
                    Number = new HexBigInteger(4),
                    Transactions = new Transaction[0]
                },
                new BlockWithTransactions
                {
                    BlockHash = "0x5",
                    Number = new HexBigInteger(5),
                    Transactions = new Transaction[0]
                }
            };

            var newBlockSource = sched.CreateHotObservable(
                OnNext(100, newBlocks[0]),
                OnNext(100, newBlocks[1]));

            // Record incoming data.
            var res = sched.Start(
                () => provider.GetBlocksWithTransactions(
                    new BlockParameter(1),
                    new BlockParameter(3),
                    newBlockSource),
                0,
                50,
                Disposed);

            res.Messages.AssertEqual(
                OnNext(50, oldBlocks[1]),
                OnNext(50, oldBlocks[2]),
                OnCompleted<BlockWithTransactions>(50));
        }

        [Fact]
        public void GetBlocksWithTransactionHashes()
        {
            // Setup.
            var sched = new TestScheduler();
            var poller = sched.CreateColdObservable(
                OnNext(100, Unit.Default),
                OnNext(200, Unit.Default));

            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller,
                filterService.Object,
                blockService.Object);

            var filterId = new HexBigInteger(1337);

            filterService
                .Setup(x => x.NewBlockFilter.SendRequestAsync(null))
                .Returns(Task.FromResult(filterId));

            filterService
                .Setup(x => x.UninstallFilter.SendRequestAsync(filterId, null))
                .Returns(Task.FromResult(true));

            // Setup incoming blocks.
            var expectedBlocks = new[]
            {
                new[]
                {
                    new BlockWithTransactionHashes
                    {
                        BlockHash = "0x1"
                    }
                },
                new[]
                {
                    new BlockWithTransactionHashes
                    {
                        BlockHash = "0x2"
                    },
                    new BlockWithTransactionHashes
                    {
                        BlockHash = "0x3"
                    }
                }
            };

            filterService.SetupSequence(x => x.GetFilterChangesForBlockOrTransaction.SendRequestAsync(filterId, null))
                .Returns(Task.FromResult(new[]
                {
                    expectedBlocks[0][0].BlockHash
                }))
                .Returns(Task.FromResult(new[]
                {
                    expectedBlocks[1][0].BlockHash, expectedBlocks[1][1].BlockHash
                }));

            blockService.Setup(x => x.GetBlockWithTransactionsHashesByHash.SendRequestAsync(expectedBlocks[0][0].BlockHash, null))
                .Returns(Task.FromResult(expectedBlocks[0][0]));
            blockService.Setup(x => x.GetBlockWithTransactionsHashesByHash.SendRequestAsync(expectedBlocks[1][0].BlockHash, null))
                .Returns(Task.FromResult(expectedBlocks[1][0]));
            blockService.Setup(x => x.GetBlockWithTransactionsHashesByHash.SendRequestAsync(expectedBlocks[1][1].BlockHash, null))
                .Returns(Task.FromResult(expectedBlocks[1][1]));

            // Record incoming data.
            var res = sched.Start(() => provider.GetBlocksWithTransactionHashes());

            res.Messages.AssertEqual(
                OnNext(100 + Subscribed, expectedBlocks[0][0]),
                OnNext(200 + Subscribed, expectedBlocks[1][0]),
                OnNext(200 + Subscribed, expectedBlocks[1][1]));

            filterService.Verify(x => x.NewBlockFilter.SendRequestAsync(null), Times.Once);
            filterService.Verify(x => x.UninstallFilter.SendRequestAsync(filterId, null), Times.Once);
        }

        [Fact]
        public void GetBlocksWithTransactionHashes_Start()
        {
            // The test works as following:
            // There are already 2 blocks. The method then gets called with
            // a `from` value of 1, and while doing so `latestBlock` becomes
            // 2 because another `updateStream` received a new block.
            // This means that the old stream should consist of blocks 1 and 2,
            // and for completeness we assume a new block number 3 comes afterwards.

            // Setup.
            var sched = new TestScheduler();

            var poller = new Mock<IObservable<Unit>>();
            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller.Object,
                filterService.Object,
                blockService.Object);

            blockService
                .Setup(x => x.GetBlockNumber.SendRequestAsync(null))
                .Returns(Task.FromResult(new HexBigInteger(2)));

            // Setup old blocks.
            var oldBlock = new BlockWithTransactionHashes
            {
                BlockHash = "0x1",
                Number = new HexBigInteger(1)
            };

            blockService
                .Setup(x => x.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new HexBigInteger(new BigInteger(1)), null))
                .Returns(Task.FromResult(oldBlock));

            // Setup new blocks.
            var newBlocks = new[]
            {
                new BlockWithTransactionHashes
                {
                    BlockHash = "0x2",
                    Number = new HexBigInteger(2)
                },
                new BlockWithTransactionHashes
                {
                    BlockHash = "0x3",
                    Number = new HexBigInteger(3)
                }
            };

            var newBlockSource = sched.CreateHotObservable(
                OnNext(100, newBlocks[0]),
                OnNext(200, newBlocks[1]));

            // Record incoming data.
            var res = sched.Start(
                () => provider.GetBlocksWithTransactionHashes(new BlockParameter(1), newBlockSource),
                0,
                50,
                Disposed);

            res.Messages.AssertEqual(
                OnNext(50, oldBlock),
                OnNext(100, newBlocks[0]),
                OnNext(200, newBlocks[1]));
        }

        [Fact]
        public void GetBlocksWithTransactionHashes_StartEndOldAndNew()
        {
            // Setup.
            var sched = new TestScheduler();

            var poller = new Mock<IObservable<Unit>>();
            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller.Object,
                filterService.Object,
                blockService.Object);

            // Setup old blocks (first should be ignored).
            var oldBlocks = new List<BlockWithTransactionHashes>();
            for (var i = 0; i < 4; i++)
            {
                oldBlocks.Add(new BlockWithTransactionHashes
                {
                    BlockHash = "0x" + i,
                    Number = new HexBigInteger(i)
                });

                blockService
                    .Setup(x => x.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new HexBigInteger(new BigInteger(i)), null))
                    .Returns(Task.FromResult(oldBlocks[i]));
            }

            blockService
                .Setup(x => x.GetBlockNumber.SendRequestAsync(null))
                .Returns(Task.FromResult(new HexBigInteger(oldBlocks.Last().Number)));

            // Setup new blocks (last one should be ignored).
            var newBlocks = new[]
            {
                new BlockWithTransactionHashes
                {
                    BlockHash = "0x4",
                    Number = new HexBigInteger(4)
                },
                new BlockWithTransactionHashes
                {
                    BlockHash = "0x5",
                    Number = new HexBigInteger(5)
                }
            };

            var newBlockSource = sched.CreateHotObservable(
                OnNext(100, newBlocks[0]),
                OnNext(100, newBlocks[1]));

            // Record incoming data.
            var res = sched.Start(
                () => provider.GetBlocksWithTransactionHashes(
                    new BlockParameter(1),
                    new BlockParameter(4),
                    newBlockSource),
                0,
                50,
                Disposed);

            res.Messages.AssertEqual(
                OnNext(50, oldBlocks[1]),
                OnNext(50, oldBlocks[2]),
                OnNext(100, newBlocks[0]),
                OnCompleted<BlockWithTransactionHashes>(100));
        }

        [Fact]
        public void GetBlocksWithTransactionHashes_StartEndOldOnly()
        {
            // Setup.
            var sched = new TestScheduler();

            var poller = new Mock<IObservable<Unit>>();
            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller.Object,
                filterService.Object,
                blockService.Object);

            // Setup old blocks (first should be ignored).
            var oldBlocks = new List<BlockWithTransactionHashes>();
            for (var i = 0; i < 4; i++)
            {
                oldBlocks.Add(new BlockWithTransactionHashes
                {
                    BlockHash = "0x" + i,
                    Number = new HexBigInteger(i)
                });

                blockService
                    .Setup(x => x.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new HexBigInteger(new BigInteger(i)), null))
                    .Returns(Task.FromResult(oldBlocks[i]));
            }

            blockService
                .Setup(x => x.GetBlockNumber.SendRequestAsync(null))
                .Returns(Task.FromResult(new HexBigInteger(oldBlocks.Last().Number)));

            // Setup new blocks (should all be ignored).
            var newBlocks = new[]
            {
                new BlockWithTransactionHashes
                {
                    BlockHash = "0x4",
                    Number = new HexBigInteger(4)
                },
                new BlockWithTransactionHashes
                {
                    BlockHash = "0x5",
                    Number = new HexBigInteger(5)
                }
            };

            var newBlockSource = sched.CreateHotObservable(
                OnNext(100, newBlocks[0]),
                OnNext(100, newBlocks[1]));

            // Record incoming data.
            var res = sched.Start(
                () => provider.GetBlocksWithTransactionHashes(
                    new BlockParameter(1),
                    new BlockParameter(3),
                    newBlockSource),
                0,
                50,
                Disposed);

            res.Messages.AssertEqual(
                OnNext(50, oldBlocks[1]),
                OnNext(50, oldBlocks[2]),
                OnCompleted<BlockWithTransactionHashes>(50));
        }

        [Fact]
        public void GetBlocksWithTransactions()
        {
            // Setup.
            var sched = new TestScheduler();
            var poller = sched.CreateColdObservable(
                OnNext(100, Unit.Default),
                OnNext(200, Unit.Default));

            var filterService = new Mock<IEthApiFilterService>();
            var blockService = new Mock<IEthApiBlockService>();

            var provider = new BlockStreamProvider(
                poller,
                filterService.Object,
                blockService.Object);

            var filterId = new HexBigInteger(1337);

            filterService
                .Setup(x => x.NewBlockFilter.SendRequestAsync(null))
                .Returns(Task.FromResult(filterId));

            filterService
                .Setup(x => x.UninstallFilter.SendRequestAsync(filterId, null))
                .Returns(Task.FromResult(true));

            // Setup incoming blocks.
            var expectedBlocks = new[]
            {
                new[]
                {
                    new BlockWithTransactions
                    {
                        BlockHash = "0x1",
                        Transactions = new Transaction[1]
                    }
                },
                new[]
                {
                    new BlockWithTransactions
                    {
                        BlockHash = "0x2",
                        Transactions = new Transaction[1]
                    },
                    new BlockWithTransactions
                    {
                        BlockHash = "0x3",
                        Transactions = new Transaction[1]
                    }
                }
            };

            filterService.SetupSequence(x => x.GetFilterChangesForBlockOrTransaction.SendRequestAsync(filterId, null))
                .Returns(Task.FromResult(new[]
                {
                    expectedBlocks[0][0].BlockHash
                }))
                .Returns(Task.FromResult(new[]
                {
                    expectedBlocks[1][0].BlockHash, expectedBlocks[1][1].BlockHash
                }));

            blockService.Setup(x => x.GetBlockWithTransactionsByHash.SendRequestAsync(expectedBlocks[0][0].BlockHash, null))
                .Returns(Task.FromResult(expectedBlocks[0][0]));
            blockService.Setup(x => x.GetBlockWithTransactionsByHash.SendRequestAsync(expectedBlocks[1][0].BlockHash, null))
                .Returns(Task.FromResult(expectedBlocks[1][0]));
            blockService.Setup(x => x.GetBlockWithTransactionsByHash.SendRequestAsync(expectedBlocks[1][1].BlockHash, null))
                .Returns(Task.FromResult(expectedBlocks[1][1]));

            // Record incoming data.
            var res = sched.Start(() => provider.GetBlocksWithTransactions());

            res.Messages.AssertEqual(
                OnNext(100 + Subscribed, expectedBlocks[0][0]),
                OnNext(200 + Subscribed, expectedBlocks[1][0]),
                OnNext(200 + Subscribed, expectedBlocks[1][1]));

            filterService.Verify(x => x.NewBlockFilter.SendRequestAsync(null), Times.Once);
            filterService.Verify(x => x.UninstallFilter.SendRequestAsync(filterId, null), Times.Once);
        }
    }
}