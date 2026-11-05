using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S4.Round3
{
    public class BlockProducerBareStoreFaultTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string UnwiredKzgPrecompile = "0x000000000000000000000000000000000000000a";
        private static readonly BigInteger ChainId = 1337;

        private sealed class FailedExecutionCounter : ILogger<BlockExecutor>
        {
            public int Failures;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (exception != null) Failures++;
            }
        }

        private static ISignedTransaction Signed(string to, BigInteger nonce) =>
            TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, to, BigInteger.Zero, nonce, 2_000_000_000, 500_000, "00"));

        [Fact]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_AProducerOverAStateStoreThatCannotBeReverted_When_ATransactionFaultsTheHost_Then_TheBlockFailsAndNoRetryRuns()
        {
            var blockStore = new InMemoryBlockStore();
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Prague);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = BigInteger.Parse("1000000000000000000000"), Nonce = 0 });

            var config = new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            var counter = new FailedExecutionCounter();
            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(Nethereum.EVM.HardforkName.Prague),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore,
                logger: counter);
            var producer = new BlockProducer(
                engine,
                blockStore, new InMemoryTransactionStore(blockStore), new InMemoryReceiptStore(), new InMemoryLogStore(), stateStore,
                trieNodeStore, stateRootCalculator);
            var options = new BlockProductionOptions
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = SenderAddress,
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                Difficulty = 1,
                ChainId = ChainId
            };

            var batch = new List<ISignedTransaction> { Signed("0x0000000000000000000000000000000000000001", 0), Signed(UnwiredKzgPrecompile, 1) };

            await Assert.ThrowsAsync<InvalidOperationException>(() => producer.ProduceBlockAsync(batch, options));

            Assert.Equal(1, counter.Failures);
            Assert.Null(await blockStore.GetLatestAsync());
        }
    }
}
