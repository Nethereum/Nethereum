using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class CallTargetCodeReadTests
    {
        private const HardforkName Fork = HardforkName.Prague;
        private const string CallerAddress = "0x00000000000000000000000000000000000000c1";
        private const string CalleeAddress = "0x00000000000000000000000000000000000000d1";
        private const int Calls = 100;
        private const byte Staticcall = 0xfa;
        private const byte Delegatecall = 0xf4;

        private static readonly ChainConfig Config = new ChainConfig
        {
            ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0
        };

        private sealed class CodeReadCountingStore : IStateStore
        {
            private readonly IStateStore _inner;
            private readonly Dictionary<string, int> _reads = new Dictionary<string, int>();

            public CodeReadCountingStore(IStateStore inner) { _inner = inner; }

            public int ReadsOf(byte[] codeHash)
            {
                lock (_reads) return _reads.GetValueOrDefault(codeHash.ToHex());
            }

            public Task<byte[]> GetCodeAsync(byte[] codeHash)
            {
                lock (_reads) _reads[codeHash.ToHex()] = _reads.GetValueOrDefault(codeHash.ToHex()) + 1;
                return _inner.GetCodeAsync(codeHash);
            }

            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
        }

        private static byte[] PushHeavyCallee()
        {
            var code = new List<byte>();
            for (var i = 0; i < 900; i++)
            {
                code.Add(0x76);
                code.AddRange(Enumerable.Repeat((byte)0x11, 23));
                code.Add(0x50);
            }
            code.Add(0x00);
            return code.ToArray();
        }

        private static byte[] CallerOf(byte callOpcode)
        {
            var code = new List<byte>();
            for (var i = 0; i < Calls; i++)
            {
                code.AddRange(new byte[] { 0x5f, 0x5f, 0x5f, 0x5f, 0x73 });
                code.AddRange(CalleeAddress.HexToByteArray());
                code.AddRange(new byte[] { 0x61, 0x20, 0x00, callOpcode, 0x50 });
            }
            code.Add(0x00);
            return code.ToArray();
        }

        private static ISignedTransaction CallToCaller() =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    BlockPipelineHarness.PrivateKey.HexToByteArray(), BlockPipelineHarness.ChainId, CallerAddress, 0, 0,
                    BlockPipelineHarness.AboveInitialBaseFeeGasPrice, 3_000_000, ""));

        private static async Task SeedAsync(IStateStore state, byte[] callerCode, byte[] calleeCode)
        {
            await state.SaveAccountAsync(
                BlockPipelineHarness.SenderAddress, new Account { Balance = 1_000_000_000_000_000_000, Nonce = 0 });
            var callerHash = Sha3Keccack.Current.CalculateHash(callerCode);
            var calleeHash = Sha3Keccack.Current.CalculateHash(calleeCode);
            await state.SaveCodeAsync(callerHash, callerCode);
            await state.SaveCodeAsync(calleeHash, calleeCode);
            await state.SaveAccountAsync(CallerAddress, new Account { Balance = 0, Nonce = 1, CodeHash = callerHash });
            await state.SaveAccountAsync(CalleeAddress, new Account { Balance = 0, Nonce = 1, CodeHash = calleeHash });
        }

        [Theory]
        [InlineData(Staticcall)]
        [InlineData(Delegatecall)]
        public async Task Given_ATransactionThatCallsOneContractRepeatedlyWithoutValue_When_TheBlockExecutes_Then_TheCalleeCodeIsReadFromTheStateStoreOnce(byte callOpcode)
        {
            var callerCode = CallerOf(callOpcode);
            var calleeCode = PushHeavyCallee();
            var producer = await BlockPipelineHarness.CreateAsync(Fork);
            await SeedAsync(producer.StateStore, callerCode, calleeCode);
            var transactions = new List<ISignedTransaction> { CallToCaller() };
            var produced = await producer.ProduceAsync(transactions);

            using var bundle = InMemoryChainStoreBundle.Open();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, Fork);
            await SeedAsync(bundle.State, callerCode, calleeCode);
            await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, Fork, 30_000_000);
            var counting = new CodeReadCountingStore(bundle.State);
            using var stack = ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(Fork), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance, StateOverride = counting });

            var result = await stack.Importer.ImportAsync(produced.Header, transactions, null, null, CancellationToken.None);

            Assert.True(result.RootMatches, result.DescribeRejection());
            Assert.Equal(1, counting.ReadsOf(Sha3Keccack.Current.CalculateHash(calleeCode)));
        }
    }
}
