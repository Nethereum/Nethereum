using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class BufferedFlatStateStoreAbsentAccountTests
    {
        private const string Created = "0x00000000000000000000000000000000000000c1";
        private const string Existing = "0x00000000000000000000000000000000000000e1";

        private sealed class RecordingWipeStore : InMemoryPathNodeStore, IContractStorageWipeable
        {
            public List<byte[]> Wiped { get; } = new();

            public void DeleteRange(byte[] owner) => Wiped.Add(owner);
        }

        private static async Task<(BufferedFlatStateStore Buffered, HistoricalStateStore Historical)> StoreWithExistingAccountAsync()
        {
            var inner = new InMemoryStateStore();
            await inner.SaveAccountAsync(Existing, new Account { Nonce = 1, Balance = 5 });
            await inner.SaveStorageAsync(Existing, BigInteger.One, new byte[] { 0x07 });
            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();
            return (buffered, new HistoricalStateStore(buffered));
        }

        private static async Task CreateStoreAndDestroyAsync(IStateStore state, string address)
        {
            Assert.Null(await state.GetAccountAsync(address));
            await state.SaveAccountAsync(address, new Account { Nonce = 1, Balance = 1 });
            await state.SaveStorageAsync(address, BigInteger.One, new byte[] { 0x09 });
            await state.ClearStorageAsync(address);
            await state.DeleteAccountAsync(address);
        }

        private static async Task DestroyAsync(IStateStore state, string address)
        {
            Assert.NotNull(await state.GetAccountAsync(address));
            await state.ClearStorageAsync(address);
            await state.DeleteAccountAsync(address);
        }

        [Fact]
        public async Task Given_an_account_absent_from_the_base_When_it_is_created_and_destroyed_in_the_block_Then_the_flushed_batch_deletes_and_clears_nothing()
        {
            var (buffered, historical) = await StoreWithExistingAccountAsync();

            await CreateStoreAndDestroyAsync(historical, Created);
            var batch = await buffered.CaptureBufferAsync();

            Assert.Empty(batch.DeletedAccountEvmAddresses);
            Assert.Empty(batch.ClearedStorageEvmAddresses);
            Assert.Empty(batch.NonZeroStorageEvm);
            Assert.Empty(batch.AccountsEvm);
        }

        [Fact]
        public async Task Given_an_account_present_in_the_base_When_it_is_destroyed_in_the_block_Then_the_flushed_batch_deletes_and_clears_it()
        {
            var (buffered, historical) = await StoreWithExistingAccountAsync();

            await DestroyAsync(historical, Existing);
            var batch = await buffered.CaptureBufferAsync();

            Assert.Equal(new[] { EvmAddress.FromHex(Existing) }, batch.DeletedAccountEvmAddresses);
            Assert.Equal(new[] { EvmAddress.FromHex(Existing) }, batch.ClearedStorageEvmAddresses);
        }

        [Fact]
        public async Task Given_an_account_absent_from_the_base_When_it_is_created_with_storage_Then_reading_all_its_storage_returns_the_buffered_slots()
        {
            var (_, historical) = await StoreWithExistingAccountAsync();

            Assert.Null(await historical.GetAccountAsync(Created));
            await historical.SaveAccountAsync(Created, new Account { Nonce = 1 });
            await historical.SaveStorageAsync(Created, BigInteger.One, new byte[] { 0x09 });

            var slots = await historical.GetAllStorageAsync(Created);

            Assert.Single(slots);
            Assert.Equal(new byte[] { 0x09 }, slots[StateKeys.StorageSlotKey(BigInteger.One)]);
        }

        [Fact]
        public async Task Given_an_account_absent_from_the_base_When_its_root_is_computed_after_it_was_created_and_destroyed_Then_no_storage_subtree_is_wiped()
        {
            var (_, historical) = await StoreWithExistingAccountAsync();
            var nodes = new RecordingWipeStore();
            using var calculator = new IncrementalStateRootCalculator(historical, nodes);
            await calculator.ComputeStateRootAsync();

            await CreateStoreAndDestroyAsync(historical, Created);
            await calculator.ComputeStateRootAsync();

            Assert.Empty(nodes.Wiped);
        }

        [Fact]
        public async Task Given_an_account_present_in_the_base_When_its_root_is_computed_after_it_was_destroyed_Then_its_storage_subtree_is_wiped()
        {
            var (_, historical) = await StoreWithExistingAccountAsync();
            var nodes = new RecordingWipeStore();
            using var calculator = new IncrementalStateRootCalculator(historical, nodes);
            await calculator.ComputeStateRootAsync();

            await DestroyAsync(historical, Existing);
            await calculator.ComputeStateRootAsync();

            Assert.Single(nodes.Wiped);
        }
    }
}
