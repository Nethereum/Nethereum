using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class SequencerTests : IAsyncLifetime
    {
        private const string TestPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private SequencerTestFixture _fixture = null!;
        private AppChainCore _appChain = null!;
        private string _senderAddress = null!;

        public async Task InitializeAsync()
        {
            _fixture = new SequencerTestFixture();
            _appChain = _fixture.CreateAppChain();

            _senderAddress = new EthECKey(TestPrivateKey).GetPublicAddress();
            await _fixture.StateStore.SaveAccountAsync(_senderAddress, new Account
            {
                Balance = BigInteger.Parse("1000000000000000000000"),
                Nonce = 0
            });
        }

        public Task DisposeAsync()
        {
            _fixture.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Sequencer_StartAndStop_Works()
        {
            var config = new SequencerConfig
            {
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100
            };

            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();
            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Sequencer_GetBlockNumber_ReturnsZeroInitially()
        {
            var config = new SequencerConfig();
            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();
            var blockNumber = await sequencer.GetBlockNumberAsync();
            await sequencer.StopAsync();

            Assert.Equal(BigInteger.Zero, blockNumber);
        }

        [Fact]
        public async Task Sequencer_GetLatestBlock_ReturnsGenesisBlock()
        {
            var config = new SequencerConfig();
            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();
            var block = await sequencer.GetLatestBlockAsync();
            await sequencer.StopAsync();

            Assert.NotNull(block);
            Assert.Equal(BigInteger.Zero, block.BlockNumber);
        }

        [Fact]
        public async Task Sequencer_ProduceEmptyBlock_Works()
        {
            var config = new SequencerConfig();
            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();

            var blockHash = await sequencer.ProduceBlockAsync();

            var blockNumber = await sequencer.GetBlockNumberAsync();
            await sequencer.StopAsync();

            Assert.NotNull(blockHash);
            Assert.Equal(32, blockHash.Length);
            Assert.Equal(BigInteger.One, blockNumber);
        }

        [Fact]
        public async Task Sequencer_SubmitTransaction_AddsToPool()
        {
            var config = new SequencerConfig
            {
                Policy = new PolicyConfig { Enabled = false }
            };
            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();

            var transaction = CreateSignedTransaction();
            var txHash = await sequencer.SubmitTransactionAsync(transaction);

            Assert.NotNull(txHash);
            Assert.Equal(32, txHash.Length);
            Assert.Equal(1, sequencer.TxPool.PendingCount);

            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Sequencer_SubmitAndProduce_IncludesTransaction()
        {
            var config = new SequencerConfig
            {
                Policy = new PolicyConfig { Enabled = false }
            };
            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();

            var transaction = CreateSignedTransaction();
            await sequencer.SubmitTransactionAsync(transaction);

            Assert.Equal(1, sequencer.TxPool.PendingCount);

            var blockHash = await sequencer.ProduceBlockAsync();

            Assert.Equal(0, sequencer.TxPool.PendingCount);
            Assert.NotNull(blockHash);

            var blockNumber = await sequencer.GetBlockNumberAsync();
            Assert.Equal(BigInteger.One, blockNumber);

            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Sequencer_OnDemandMode_ProducesBlockOnSubmit()
        {
            var config = new SequencerConfig
            {
                BlockTimeMs = 0,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = new PolicyConfig { Enabled = false }
            };
            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();

            var initialBlockNumber = await sequencer.GetBlockNumberAsync();
            Assert.Equal(BigInteger.Zero, initialBlockNumber);

            var transaction = CreateSignedTransaction();
            await sequencer.SubmitTransactionAsync(transaction);

            Assert.Equal(0, sequencer.TxPool.PendingCount);

            var finalBlockNumber = await sequencer.GetBlockNumberAsync();
            Assert.Equal(BigInteger.One, finalBlockNumber);

            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Sequencer_IntervalMode_DoesNotProduceBlockOnSubmit()
        {
            var config = new SequencerConfig
            {
                BlockTimeMs = 0,
                BlockProductionMode = BlockProductionMode.Interval,
                Policy = new PolicyConfig { Enabled = false }
            };
            var sequencer = new Sequencer(_appChain, config);

            await sequencer.StartAsync();

            var initialBlockNumber = await sequencer.GetBlockNumberAsync();

            var transaction = CreateSignedTransaction();
            await sequencer.SubmitTransactionAsync(transaction);

            Assert.Equal(1, sequencer.TxPool.PendingCount);

            var afterSubmitBlockNumber = await sequencer.GetBlockNumberAsync();
            Assert.Equal(initialBlockNumber, afterSubmitBlockNumber);

            await sequencer.StopAsync();
        }

        [Fact]
        public async Task Sequencer_OnDemandConfig_HasCorrectSettings()
        {
            var config = SequencerConfig.OnDemand;

            Assert.Equal(0, config.BlockTimeMs);
            Assert.Equal(BlockProductionMode.OnDemand, config.BlockProductionMode);
        }

        [Fact]
        public async Task Sequencer_DefaultConfig_UsesIntervalMode()
        {
            var config = SequencerConfig.Default;

            Assert.Equal(1000, config.BlockTimeMs);
            Assert.Equal(BlockProductionMode.Interval, config.BlockProductionMode);
        }

        private ISignedTransaction CreateSignedTransaction(int nonce = 0)
        {
            var privateKey = new EthECKey(TestPrivateKey);

            var transaction = new Transaction1559(
                chainId: _appChain.Config.ChainId,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(21000),
                receiverAddress: "0x0000000000000000000000000000000000000001",
                amount: BigInteger.Zero,
                data: null,
                accessList: null
            );

            var signature = privateKey.SignAndCalculateYParityV(transaction.RawHash);
            transaction.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            return transaction;
        }
    }
}
