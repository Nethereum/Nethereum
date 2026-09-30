using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockAccessListRecorderFaultHandlingTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();

        private static ISignedTransaction CreateSignedTransaction(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, RecipientAddress, 100, nonce, 1, 21_000, ""));

        private static ISignedTransaction CreateRevertingCreateTransaction(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, "", 0, nonce, 1, 100_000, "0x60006000fd"));

        private static ChainConfig BuildChainConfig() =>
            new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0, Hardfork = "Prague" };

        private static BlockContext BuildBlockContext() => new BlockContext
        {
            BlockNumber = 1,
            Timestamp = 1_700_000_000,
            Coinbase = SenderAddress,
            GasLimit = 30_000_000,
            BaseFee = 0,
            ChainId = ChainId,
            Difficulty = 0,
            PrevRandao = new byte[32],
            ExcessBlobGas = 0
        };

        private static EvmUInt256 MalformedNonce()
        {
            var bytes = new byte[32];
            bytes[0] = 1;
            return EvmUInt256.FromBigEndian(bytes);
        }

        private sealed class PostCommitMalformedNonceStateStore : IStateStore
        {
            private readonly IStateStore _inner;
            private readonly string _targetAddress;
            private readonly bool _malformedImmediately;
            private bool _committed;

            public PostCommitMalformedNonceStateStore(IStateStore inner, string targetAddress, bool malformedImmediately)
            {
                _inner = inner;
                _targetAddress = targetAddress;
                _malformedImmediately = malformedImmediately;
            }

            public async Task<Account> GetAccountAsync(string address)
            {
                var real = await _inner.GetAccountAsync(address).ConfigureAwait(false);
                if (!string.Equals(address, _targetAddress, StringComparison.OrdinalIgnoreCase)) return real;
                if (!_malformedImmediately && !_committed) return real;

                return new Account
                {
                    Nonce = MalformedNonce(),
                    Balance = real?.Balance ?? EvmUInt256.Zero,
                    CodeHash = real?.CodeHash ?? DefaultValues.EMPTY_DATA_HASH,
                    StateRoot = real?.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH
                };
            }

            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<Nethereum.CoreChain.Storage.IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(Nethereum.CoreChain.Storage.IStateSnapshot snapshot)
            {
                _committed = true;
                return _inner.CommitSnapshotAsync(snapshot);
            }
            public Task RevertSnapshotAsync(Nethereum.CoreChain.Storage.IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
        }


        [Fact]
        public async Task Given_FaultPartwayThroughRecordLoop_When_SecondAccountFaults_Then_NoEntriesApplied()
        {
            var innerStore = new InMemoryStateStore();
            await innerStore.SaveAccountAsync(SenderAddress, new Account { Balance = 100, Nonce = 0 });
            await innerStore.SaveAccountAsync(RecipientAddress, new Account { Balance = 100, Nonce = 0 });

            var faultStore = new PostCommitMalformedNonceStateStore(innerStore, RecipientAddress, malformedImmediately: false);
            var recorder = new BlockAccessListRecorder(faultStore);

            var executionState = new ExecutionStateService(new StateStoreNodeDataService(innerStore, new InMemoryBlockStore()));
            executionState.CreateOrGetAccountExecutionState(SenderAddress);
            executionState.CreateOrGetAccountExecutionState(RecipientAddress);

            recorder.BeginUnit(1);
            await recorder.PrepareUnitOfWorkAsync(executionState);

            await innerStore.SaveAccountAsync(SenderAddress, new Account { Balance = 50, Nonce = 1 });
            await innerStore.SaveAccountAsync(RecipientAddress, new Account { Balance = 150, Nonce = 0 });
            await faultStore.CommitSnapshotAsync(await faultStore.CreateSnapshotAsync());

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => recorder.RecordUnitOfWorkAsync(executionState));

            var built = recorder.Build();
            Assert.Empty(built);
        }


        [Fact]
        public async Task Given_FaultDuringPrepare_When_TransactionExecutes_Then_ThrowsHostFault_NotRevertedReceipt()
        {
            var innerStore = new InMemoryStateStore();
            await innerStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            var faultStore = new PostCommitMalformedNonceStateStore(innerStore, RecipientAddress, malformedImmediately: true);

            var config = BuildChainConfig();
            var txProcessor = new TransactionProcessor(
                faultStore, new InMemoryBlockStore(), config,
                new TransactionVerificationAndRecoveryImp(), config.GetHardforkConfig());
            var recorder = new BlockAccessListRecorder(faultStore);
            recorder.BeginUnit(1);

            var tx = CreateSignedTransaction(nonce: 0);

            var ex = await Assert.ThrowsAsync<EvmHostException>(() =>
                txProcessor.ExecuteTransactionAsync(
                    tx, BuildBlockContext(), txIndex: 0, cumulativeGasUsed: 0,
                    balRecorder: recorder, blockAccessIndex: 1));

            Assert.Contains("preparing", ex.Message);
            Assert.Empty(recorder.Build());
        }

        [Fact]
        public async Task Given_FaultDuringRecord_When_TransactionExecutes_Then_ThrowsHostFault_NotRevertedReceipt()
        {
            var innerStore = new InMemoryStateStore();
            await innerStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            var faultStore = new PostCommitMalformedNonceStateStore(innerStore, RecipientAddress, malformedImmediately: false);

            var config = BuildChainConfig();
            var txProcessor = new TransactionProcessor(
                faultStore, new InMemoryBlockStore(), config,
                new TransactionVerificationAndRecoveryImp(), config.GetHardforkConfig());
            var recorder = new BlockAccessListRecorder(faultStore);
            recorder.BeginUnit(1);

            var tx = CreateSignedTransaction(nonce: 0);

            var ex = await Assert.ThrowsAsync<EvmHostException>(() =>
                txProcessor.ExecuteTransactionAsync(
                    tx, BuildBlockContext(), txIndex: 0, cumulativeGasUsed: 0,
                    balRecorder: recorder, blockAccessIndex: 1));

            Assert.Contains("recording", ex.Message);
            Assert.Empty(recorder.Build());
        }

        [Fact]
        public async Task Given_GenuineEvmRevert_When_TransactionExecutes_Then_ReturnsRevertedReceipt_NotThrown()
        {
            var stateStore = new InMemoryStateStore();
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });

            var config = BuildChainConfig();
            var txProcessor = new TransactionProcessor(
                stateStore, new InMemoryBlockStore(), config,
                new TransactionVerificationAndRecoveryImp(), config.GetHardforkConfig());
            var recorder = new BlockAccessListRecorder(stateStore);
            recorder.BeginUnit(1);

            var tx = CreateRevertingCreateTransaction(nonce: 0);

            var result = await txProcessor.ExecuteTransactionAsync(
                tx, BuildBlockContext(), txIndex: 0, cumulativeGasUsed: 0,
                balRecorder: recorder, blockAccessIndex: 1);

            Assert.False(result.Success);
            Assert.False(result.Skipped);
            Assert.NotNull(result.Receipt);

            var built = recorder.Build();
            Assert.Contains(built, a => a.Address == SenderAddress.ToLowerInvariant());
        }
    }
}
