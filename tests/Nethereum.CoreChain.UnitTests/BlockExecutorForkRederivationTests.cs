using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockExecutorForkRederivationTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecordDifficultyContractAddress = "0x000000000000000000000000000000DeaDBeef";
        private const long BlockGasLimit = 30_000_000;

        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();

        private static readonly byte[] RecordDifficultyCode = { 0x44, 0x60, 0x00, 0x55, 0x00 };

        private static ISignedTransaction CallRecordDifficultyContract() =>
            TransactionFactory.CreateTransaction(Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, RecordDifficultyContractAddress, 0, 0, 0, 100_000, ""));

        private static byte[] ThirtyTwoByteWordFor(long value)
        {
            var word = new byte[32];
            var magnitude = new BigInteger(value).ToByteArray(isUnsigned: true, isBigEndian: true);
            magnitude.CopyTo(word, word.Length - magnitude.Length);
            return word;
        }

        private sealed class Stack
        {
            public InMemoryStateStore StateStore = null!;
            public BlockExecutor Engine = null!;
            public BlockHeader Header = null!;
        }

        private static async Task<Stack> BuildAsync(
            HardforkName resolvedFork, string? chainConfigHardfork, BlockHeader header)
        {
            var stateStore = new InMemoryStateStore();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, resolvedFork);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });

            var codeHash = Sha3Keccack.Current.CalculateHash(RecordDifficultyCode);
            await stateStore.SaveCodeAsync(codeHash, RecordDifficultyCode);
            await stateStore.SaveAccountAsync(RecordDifficultyContractAddress, new Account { Balance = 0, Nonce = 0, CodeHash = codeHash });

            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = BlockGasLimit,
                BaseFee = 0
            };
            config.ForkSchedule.Hardfork = chainConfigHardfork;

            var trieNodeStore = new InMemoryContentNodeStore();

            return new Stack
            {
                StateStore = stateStore,
                Engine = new BlockExecutor(
                    stateStore,
                    new InMemoryBlockStore(),
                    new FixedChainActivations(resolvedFork),
                    chainConfigFactory: _ => config,
                    hardforkConfigFactory: config.ConfigForFork,
                    stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                    rewardPolicy: NoRewardPolicy.Instance,
                    trieNodeStore: trieNodeStore),
                Header = header
            };
        }

        private static BlockHeader HeaderWith(BigInteger difficulty, byte[] mixHash) => new BlockHeader
        {
            BlockNumber = 1,
            Timestamp = 1_700_000_000,
            GasLimit = BlockGasLimit,
            BaseFee = 0,
            Coinbase = SenderAddress,
            ParentHash = new byte[32],
            Difficulty = difficulty,
            MixHash = mixHash
        };

        private static async Task<BigInteger> RecordedDifficultyAsync(Stack stack)
        {
            var result = await stack.Engine.ExecuteAsync(
                stack.Header, new[] { new TxEntry(CallRecordDifficultyContract()) },
                uncles: null, withdrawals: null, options: new BlockExecutionOptions());

            Assert.Null(result.Exception);
            Assert.Single(result.Receipts);

            var stored = await stack.StateStore.GetStorageAsync(RecordDifficultyContractAddress, BigInteger.Zero);
            return stored == null ? BigInteger.Zero : new BigInteger(stored, isUnsigned: true, isBigEndian: true);
        }

        [Fact]
        public async Task Given_ChainConfigWithNullHardforkString_When_BlockExecuted_Then_UsesResolvedForkAndSucceeds()
        {
            var mixHash = ThirtyTwoByteWordFor(7);
            var stack = await BuildAsync(
                HardforkName.Prague, chainConfigHardfork: null, HeaderWith(difficulty: 999_999, mixHash));

            var recordedDifficulty = await RecordedDifficultyAsync(stack);

            Assert.Equal(7, recordedDifficulty);
        }

        [Fact]
        public async Task Given_ChainConfigHardforkDisagreesWithResolvedFork_When_BlockExecuted_Then_ResolvedForkWins()
        {
            var mixHash = ThirtyTwoByteWordFor(42);
            var stack = await BuildAsync(
                HardforkName.London, chainConfigHardfork: "prague", HeaderWith(difficulty: 999_999, mixHash));

            var recordedDifficulty = await RecordedDifficultyAsync(stack);

            Assert.Equal(999_999, recordedDifficulty);
        }

        [Fact]
        public async Task Given_PreMergeBlock_When_BuildBlockContext_Then_DifficultyFromHeader()
        {
            var mixHash = ThirtyTwoByteWordFor(123);
            var stack = await BuildAsync(
                HardforkName.London, chainConfigHardfork: "london", HeaderWith(difficulty: 123_456, mixHash));

            var recordedDifficulty = await RecordedDifficultyAsync(stack);

            Assert.Equal(123_456, recordedDifficulty);
        }

        [Fact]
        public async Task Given_PostMergeBlock_When_BuildBlockContext_Then_PrevRandaoAsDifficulty()
        {
            var mixHash = ThirtyTwoByteWordFor(55);
            var stack = await BuildAsync(
                HardforkName.Prague, chainConfigHardfork: "prague", HeaderWith(difficulty: 123_456, mixHash));

            var recordedDifficulty = await RecordedDifficultyAsync(stack);

            Assert.Equal(55, recordedDifficulty);
        }
    }
}
