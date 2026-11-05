using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Signer;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class SystemCallBalUnitPinTests
    {
        private const string Target = "0x000000000000000000000000000000000000f00d";

        private sealed class SpyStateReader : IStateReader
        {
            private readonly IStateReader _inner;
            public List<string> BalanceReads { get; } = new List<string>();

            public SpyStateReader(IStateReader inner) { _inner = inner; }

            public Task<EvmUInt256> GetBalanceAsync(byte[] address) => GetBalanceAsync(Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(address, true));

            public Task<EvmUInt256> GetBalanceAsync(string address)
            {
                BalanceReads.Add(address.ToLowerInvariant());
                return _inner.GetBalanceAsync(address);
            }

            public Task<byte[]> GetCodeAsync(byte[] address) => _inner.GetCodeAsync(address);
            public Task<byte[]> GetCodeAsync(string address) => _inner.GetCodeAsync(address);
            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => _inner.GetTransactionCountAsync(address);
            public Task<EvmUInt256> GetTransactionCountAsync(string address) => _inner.GetTransactionCountAsync(address);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task<byte[]> GetBlockHashAsync(long blockNumber) => _inner.GetBlockHashAsync(blockNumber);
        }

        private static async Task<(TransactionProcessor Processor, InMemoryStateStore Store)> ProcessorAsync()
        {
            var store = new InMemoryStateStore();
            await SystemCallBlockHarness.DeployMarkerAsync(store, Target);
            await store.SaveAccountAsync(Eip7685Constants.SystemAddress, new Account { Balance = 7, Nonce = 1 });
            var config = new ChainConfig { ChainId = 1, BlockGasLimit = 30_000_000, BaseFee = 0, Hardfork = "Prague" };
            var processor = new TransactionProcessor(
                store, new InMemoryBlockStore(), config,
                new TransactionVerificationAndRecoveryImp(), config.GetHardforkConfig());
            return (processor, store);
        }

        private static BlockContext Context() => new BlockContext
        {
            BlockNumber = 1,
            Timestamp = 1_700_000_000,
            Coinbase = "0x0000000000000000000000000000000000000001",
            GasLimit = 30_000_000,
            BaseFee = 0,
            ChainId = 1,
            Difficulty = 0,
            PrevRandao = new byte[32],
            ExcessBlobGas = 0
        };

        [Fact]
        public async Task Given_SystemCall_When_Executed_Then_BalRecordsUnitAtBlockAccessIndexBeforeBalanceRead()
        {
            var (processor, store) = await ProcessorAsync();
            var recorder = new BlockAccessListRecorder(store);
            var spy = new SpyStateReader(new StateStoreNodeDataService(store));

            await processor.ExecuteSystemCallAsync(Target, Context(), spy, recorder, blockAccessIndex: 3);

            var target = recorder.Build().Single(a => a.Address == Target);
            var slot = Assert.Single(target.StorageChanges);
            var change = Assert.Single(slot.Changes);
            Assert.Equal(3UL, change.BlockAccessIndex);
            Assert.Equal(new EvmUInt256(1), change.PostValue);
            Assert.Equal(1, spy.BalanceReads.Count(a => a == Eip7685Constants.SystemAddress.ToLowerInvariant()));
        }

        [Fact]
        public async Task Given_SystemCall_When_Executed_Then_SystemAddressInitialBalanceIsLoaded()
        {
            var (processor, store) = await ProcessorAsync();
            var spy = new SpyStateReader(new StateStoreNodeDataService(store));

            var returned = await processor.ExecuteSystemCallAsync(Target, Context(), spy);

            Assert.Empty(returned);
            Assert.Contains(Eip7685Constants.SystemAddress.ToLowerInvariant(), spy.BalanceReads);
            Assert.Equal(new EvmUInt256(1), EvmUInt256.FromBigEndian(
                (await store.GetStorageAsync(Target, 0)).PadLeft32()));
        }

        [Fact]
        public async Task Given_SystemCallWithoutAReader_When_Executed_Then_TheStoreIsReadAndWrittenAndNoRecorderIsNeeded()
        {
            var (processor, store) = await ProcessorAsync();

            await processor.ExecuteSystemCallAsync(Target, Context());

            Assert.NotNull(await store.GetStorageAsync(Target, 0));
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ExecuteSystemCallAsync("", Context()));
            await Assert.ThrowsAsync<ArgumentNullException>(() => processor.ExecuteSystemCallAsync(Target, null));
        }
    }

    internal static class PadExtensions
    {
        public static byte[] PadLeft32(this byte[] value)
        {
            var padded = new byte[32];
            Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }
    }
}
