using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class TraceReplayBlockEnvironmentTests
    {
        private const string ReplayedSenderPrivateKey =
            "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string TracedSenderPrivateKey =
            "59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        private const string ReplayedSenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string EnvironmentContractAddress = "0x9965507D1a55bcC2695C58ba16FB37d819B0A4dc";
        private const string TracedRecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const string FeeRecipientAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";

        private static readonly BigInteger ChainId = 1337;
        private const long BlockNumber = 1;
        private const long Timestamp = 1_700_000_000;

        private const long ExcessBlobGas = 10L * AmsterdamBlobGasRule.DEFAULT_BASE_FEE_UPDATE_FRACTION;
        private const ulong SlotNumber = 424_242;
        private const long MaxFeePerBlobGas = 1_000_000_000;

        private const int SlotOfSlotNumber = 0;
        private const int SlotOfBlobBaseFee = 1;
        private const int SlotOfDifficulty = 2;

        private static readonly byte[] BlockEnvironmentRecordingCode =
            "4B5F554A6001554460025500".HexToByteArray();

        private static byte[] PrevRandao()
        {
            var mixHash = new byte[32];
            mixHash[0] = 0x5a;
            mixHash[31] = 0xa5;
            return mixHash;
        }

        private static ISignedTransaction SignedCall(string privateKey, string to, BigInteger nonce, BigInteger gasLimit)
            => TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                privateKey.HexToByteArray(), ChainId, to, 0, nonce, 0, gasLimit, ""));

        private static ISignedTransaction SignedBlobCall(string privateKey, string to, BigInteger gasLimit)
        {
            var versionedHash = new byte[32];
            versionedHash[0] = 0x01;
            versionedHash[31] = 0x07;

            return TransactionFactory.CreateTransaction(
                new TypeTransactionSigner<Transaction4844>().SignTransaction(
                    privateKey,
                    new Transaction4844(ChainId, 0, 1, 1, gasLimit, to, 0, "",
                        new List<AccessListItem>(), MaxFeePerBlobGas,
                        new List<byte[]> { versionedHash }))
                .HexToByteArray());
        }

        private static BlockHeader BlockHeaderUnderTrace() => new BlockHeader
        {
            BlockNumber = BlockNumber,
            Timestamp = Timestamp,
            GasLimit = 30_000_000,
            BaseFee = 0,
            Difficulty = 0,
            MixHash = PrevRandao(),
            ExcessBlobGas = ExcessBlobGas,
            SlotNumber = SlotNumber,
            Coinbase = FeeRecipientAddress,
            ParentHash = new byte[32]
        };

        private static async Task<EvmUInt256> RecordedByTheReplayAsync(int slot)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(
                ReplayedSenderAddress, new Account { Balance = BigInteger.Parse("1000000000000000000"), Nonce = 0 });

            var codeHash = new Sha3Keccack().CalculateHash(BlockEnvironmentRecordingCode);
            await stateStore.SaveCodeAsync(codeHash, BlockEnvironmentRecordingCode);
            await stateStore.SaveAccountAsync(
                EnvironmentContractAddress, new Account { Balance = 0, Nonce = 0, CodeHash = codeHash });

            var blockStore = new InMemoryBlockStore();
            var blockHash = new byte[32];
            blockHash[31] = 0x01;
            await blockStore.SaveAsync(BlockHeaderUnderTrace(), blockHash);

            var replayedTx = SignedBlobCall(ReplayedSenderPrivateKey, EnvironmentContractAddress, 1_000_000);
            var tracedTx = SignedCall(TracedSenderPrivateKey, TracedRecipientAddress, 0, 21_000);

            var transactionStore = new InMemoryTransactionStore(blockStore);
            await transactionStore.SaveAsync(replayedTx, blockHash, 0, BlockNumber);
            await transactionStore.SaveAsync(tracedTx, blockHash, 1, BlockNumber);

            var receiptStore = new InMemoryReceiptStore();
            await receiptStore.SaveAsync(
                new Receipt(), tracedTx.Hash, blockHash, BlockNumber, txIndex: 1,
                gasUsed: 21_000, contractAddress: null, effectiveGasPrice: 0);

            var node = new TraceOnlyChainNode(blockStore, transactionStore, receiptStore, stateStore);
            var trace = await node.TraceAsync(tracedTx.Hash.ToHex(true));

            var value = await trace.StateService.GetFromStorageWithoutRecordingAccessAsync(
                EnvironmentContractAddress, new EvmUInt256(slot));

            return value == null || value.Length == 0 ? EvmUInt256.Zero : EvmUInt256.FromBigEndian(value);
        }

        [Fact]
        public async Task Given_ABlockCarryingASlotNumber_When_ATraceReplaysAPrecedingTransaction_Then_SLOTNUM_ReadsIt()
        {
            Assert.Equal(new EvmUInt256(SlotNumber), await RecordedByTheReplayAsync(SlotOfSlotNumber));
        }

        [Fact]
        public async Task Given_ABlockCarryingExcessBlobGas_When_ATraceReplaysAPrecedingTransaction_Then_BLOBBASEFEE_IsPricedOffIt()
        {
            var expected = AmsterdamBlobGasRule.Instance.CalculateBlobBaseFee(new EvmUInt256(ExcessBlobGas));

            Assert.True(expected > EvmUInt256.One);
            Assert.Equal(expected, await RecordedByTheReplayAsync(SlotOfBlobBaseFee));
        }

        [Fact]
        public async Task Given_APostMergeBlock_When_ATraceReplaysAPrecedingTransaction_Then_DIFFICULTY_ReadsThePrevRandao()
        {
            Assert.Equal(
                EvmUInt256.FromBigEndian(PrevRandao()),
                await RecordedByTheReplayAsync(SlotOfDifficulty));
        }

        private sealed class TraceOnlyChainNode : ChainNodeBase
        {
            private static readonly ChainConfig AmsterdamConfig = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                Coinbase = FeeRecipientAddress,
                Hardfork = nameof(HardforkName.Amsterdam)
            };

            public TraceOnlyChainNode(
                IBlockStore blockStore,
                ITransactionStore transactionStore,
                IReceiptStore receiptStore,
                IStateStore stateStore)
                : base(blockStore, transactionStore, receiptStore, new InMemoryLogStore(), stateStore,
                       new InMemoryFilterStore(),
                       new TransactionProcessor(stateStore, blockStore, AmsterdamConfig,
                           new TransactionVerificationAndRecoveryImp(), AmsterdamConfig.GetHardforkConfig()),
                       new TransactionVerificationAndRecoveryImp(),
                       hardforkConfig: AmsterdamConfig.GetHardforkConfig())
            {
            }

            public override ChainConfig Config => AmsterdamConfig;

            protected override Task<IStateReader> GetNodeDataServiceAtBlockAsync(BigInteger blockNumber)
                => Task.FromResult(_nodeDataService);

            public Task<TraceExecutionResult> TraceAsync(string txHash)
                => PrepareAndExecuteTraceAsync(txHash);

            public override Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx)
                => throw new System.NotSupportedException();

            public override Task<List<ISignedTransaction>> GetPendingTransactionsAsync()
                => throw new System.NotSupportedException();
        }
    }
}
