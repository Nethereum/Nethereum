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
    public class PendingTransactionStreamTests : ReactiveTest
    {
        [Fact]
        public void GetBlocksWithTransactionHashes()
        {
            // Setup.
            var sched = new TestScheduler();
            var poller = sched.CreateColdObservable(
                OnNext(100, Unit.Default),
                OnNext(200, Unit.Default));

            var filterService = new Mock<IEthApiFilterService>();
            var transactionService = new Mock<IEthApiTransactionsService>();

            var provider = new PendingTransactionStreamProvider(
                poller,
                filterService.Object,
                transactionService.Object);

            var filterId = new HexBigInteger(1337);

            filterService
                .Setup(x => x.NewPendingTransactionFilter.SendRequestAsync(null))
                .Returns(Task.FromResult(filterId));

            filterService
                .Setup(x => x.UninstallFilter.SendRequestAsync(filterId, null))
                .Returns(Task.FromResult(true));

            // Setup incoming pending transactions.
            var expectedTransactions = new[]
            {
                new[]
                {
                    new Transaction
                    {
                        TransactionHash = "0x1"
                    }
                },
                new[]
                {
                    new Transaction
                    {
                        TransactionHash = "0x2"
                    },
                    new Transaction
                    {
                        TransactionHash = "0x3"
                    }
                }
            };

            filterService.SetupSequence(x => x.GetFilterChangesForBlockOrTransaction.SendRequestAsync(filterId, null))
                .Returns(Task.FromResult(new[]
                {
                    expectedTransactions[0][0].TransactionHash
                }))
                .Returns(Task.FromResult(new[]
                {
                    expectedTransactions[1][0].TransactionHash, expectedTransactions[1][1].TransactionHash
                }));

            transactionService.Setup(x => x.GetTransactionByHash.SendRequestAsync(expectedTransactions[0][0].TransactionHash, null))
                .Returns(Task.FromResult(expectedTransactions[0][0]));
            transactionService.Setup(x => x.GetTransactionByHash.SendRequestAsync(expectedTransactions[1][0].TransactionHash, null))
                .Returns(Task.FromResult(expectedTransactions[1][0]));
            transactionService.Setup(x => x.GetTransactionByHash.SendRequestAsync(expectedTransactions[1][1].TransactionHash, null))
                .Returns(Task.FromResult(expectedTransactions[1][1]));

            // Record incoming data.
            var res = sched.Start(() => provider.GetPendingTransactions());

            res.Messages.AssertEqual(
                OnNext(100 + Subscribed, expectedTransactions[0][0]),
                OnNext(200 + Subscribed, expectedTransactions[1][0]),
                OnNext(200 + Subscribed, expectedTransactions[1][1]));

            filterService.Verify(x => x.NewPendingTransactionFilter.SendRequestAsync(null), Times.Once);
            filterService.Verify(x => x.UninstallFilter.SendRequestAsync(filterId, null), Times.Once);
        }
    }
}