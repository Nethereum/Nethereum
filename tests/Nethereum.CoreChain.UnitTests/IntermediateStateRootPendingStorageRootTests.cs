using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class IntermediateStateRootPendingStorageRootTests
    {
        private const string Contract = "0x00000000000000000000000000000000000000fe";

        private static readonly byte[] SlotValue = Enumerable.Repeat((byte)0x2a, 32).ToArray();
        private static readonly byte[] Zero = new byte[32];

        private sealed class Block
        {
            public InMemoryStateStore State { get; } = new InMemoryStateStore();
            public InMemoryContentNodeStore Nodes { get; } = new InMemoryContentNodeStore();
            public IncrementalStateRootCalculator Calculator { get; private set; }
            public byte[] ParentRoot { get; private set; }

            public static async Task<Block> OnParentWithStorageAsync(params int[] slots)
            {
                var block = new Block();
                await block.State.SaveAccountAsync(Contract, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 1, 2, 3 } });
                foreach (var slot in slots)
                    await block.State.SaveStorageAsync(Contract, slot, SlotValue);
                block.ParentRoot = await new IncrementalStateRootCalculator(block.State, block.Nodes).ComputeStateRootAsync();
                block.Calculator = new IncrementalStateRootCalculator(block.State, block.Nodes);
                return block;
            }

            public async Task<byte[]> StorageRootAsync() => (await State.GetAccountAsync(Contract)).StateRoot;
        }

        private static async Task<byte[]> RootOfFreshStateWithStorageAsync(params int[] slots)
        {
            var fresh = await Block.OnParentWithStorageAsync(slots);
            return fresh.ParentRoot;
        }

        [Fact]
        public async Task Given_pre_byzantium_intermediate_roots_When_a_slot_is_set_then_cleared_within_the_block_Then_the_flat_storage_root_is_the_empty_trie_root()
        {
            var block = await Block.OnParentWithStorageAsync();

            await block.State.SaveStorageAsync(Contract, 1, SlotValue);
            await block.Calculator.ComputeStateRootWithoutPersistAsync(block.ParentRoot);
            await block.State.SaveStorageAsync(Contract, 1, Zero);
            var blockRoot = await block.Calculator.ComputeStateRootWithoutPersistAsync(null);
            await block.Calculator.PersistPendingStateAsync();

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, await block.StorageRootAsync());
            Assert.Equal(block.ParentRoot, blockRoot);
        }

        [Fact]
        public async Task Given_post_byzantium_no_intermediate_roots_When_a_slot_is_set_then_cleared_within_the_block_Then_the_flat_storage_root_is_the_empty_trie_root()
        {
            var block = await Block.OnParentWithStorageAsync();

            await block.State.SaveStorageAsync(Contract, 1, SlotValue);
            await block.State.SaveStorageAsync(Contract, 1, Zero);
            var blockRoot = await block.Calculator.ComputeStateRootWithoutPersistAsync(block.ParentRoot);
            await block.Calculator.PersistPendingStateAsync();

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, await block.StorageRootAsync());
            Assert.Equal(block.ParentRoot, blockRoot);
        }

        [Fact]
        public async Task Given_pre_byzantium_intermediate_roots_When_the_only_slot_is_cleared_then_another_is_set_Then_the_root_is_that_of_the_remaining_storage()
        {
            var block = await Block.OnParentWithStorageAsync(1);

            await block.State.SaveStorageAsync(Contract, 1, Zero);
            await block.Calculator.ComputeStateRootWithoutPersistAsync(block.ParentRoot);
            await block.State.SaveStorageAsync(Contract, 2, SlotValue);
            var blockRoot = await block.Calculator.ComputeStateRootWithoutPersistAsync(null);
            await block.Calculator.PersistPendingStateAsync();

            Assert.Equal(await RootOfFreshStateWithStorageAsync(2), blockRoot);
        }

        [Fact]
        public async Task Given_post_byzantium_no_intermediate_roots_When_the_only_slot_is_cleared_then_another_is_set_Then_the_root_is_that_of_the_remaining_storage()
        {
            var block = await Block.OnParentWithStorageAsync(1);

            await block.State.SaveStorageAsync(Contract, 1, Zero);
            await block.State.SaveStorageAsync(Contract, 2, SlotValue);
            var blockRoot = await block.Calculator.ComputeStateRootWithoutPersistAsync(block.ParentRoot);
            await block.Calculator.PersistPendingStateAsync();

            Assert.Equal(await RootOfFreshStateWithStorageAsync(2), blockRoot);
        }
    }
}
