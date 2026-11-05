using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class MissingStorageNodeStateRootTests
    {
        private const string Contract = "0x00000000000000000000000000000000000000ca";

        private static async Task<(InMemoryStateStore state, InMemoryContentNodeStore nodes, byte[] root)> CommittedContractAsync()
        {
            var state = new InMemoryStateStore();
            var nodes = new InMemoryContentNodeStore();
            await state.SaveAccountAsync(Contract, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 1, 2, 3 } });
            for (var slot = 1; slot <= 60; slot++)
                await state.SaveStorageAsync(Contract, slot, Enumerable.Repeat((byte)slot, 32).ToArray());
            var root = await new IncrementalStateRootCalculator(state, nodes).ComputeStateRootAsync();
            return (state, nodes, root);
        }

        private static byte[] StorageRootOf(InMemoryContentNodeStore nodes, byte[] root)
        {
            var leaf = PatriciaTrie.LoadFromStorage(root, nodes).Get(StateKeys.AccountKey(Contract));
            return AccountEncoder.Current.Decode(leaf).StateRoot;
        }

        private static InMemoryContentNodeStore CopyWithout(InMemoryContentNodeStore source, byte[] omitted)
        {
            var copy = new InMemoryContentNodeStore();
            foreach (var entry in source.Storage.Where(e => !ByteUtil.AreEqual(e.Key, omitted)))
                copy.Put(entry.Key, entry.Value);
            return copy;
        }

        private static byte[] AnInnerStorageNode(InMemoryContentNodeStore nodes, byte[] storageRoot)
        {
            var branch = (RLPCollection)RLP.RLP.Decode(nodes.Get(storageRoot));
            return Enumerable.Range(0, 16).Select(i => branch[i].RLPData).First(c => c is { Length: 32 });
        }

        private static async Task<byte[]> RewriteEverySlotAndComputeRootAsync(InMemoryStateStore state, InMemoryContentNodeStore nodes, byte[] root)
        {
            var calculator = new IncrementalStateRootCalculator(state, nodes);
            await calculator.ComputeStateRootAsync(root);
            for (var slot = 1; slot <= 60; slot++)
                await state.SaveStorageAsync(Contract, slot, new byte[] { 0x99, (byte)slot });
            return await calculator.ComputeStateRootAsync();
        }

        private static bool ContainsMissingNode(Exception exception)
            => exception switch
            {
                MissingTrieNodeException => true,
                AggregateException aggregate => aggregate.InnerExceptions.Any(ContainsMissingNode),
                _ => exception.InnerException != null && ContainsMissingNode(exception.InnerException)
            };

        [Fact]
        public async Task Given_a_storage_trie_missing_an_inner_node_When_the_state_root_is_computed_Then_it_throws_rather_than_returning_a_root()
        {
            var (state, nodes, root) = await CommittedContractAsync();
            var storageRoot = StorageRootOf(nodes, root);
            var holed = CopyWithout(nodes, AnInnerStorageNode(nodes, storageRoot));

            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => RewriteEverySlotAndComputeRootAsync(state, holed, root));

            Assert.True(ContainsMissingNode(thrown), thrown.ToString());
        }

        [Fact]
        public async Task Given_the_same_storage_trie_with_every_node_present_When_the_state_root_is_computed_Then_a_root_is_returned()
        {
            var (state, nodes, root) = await CommittedContractAsync();

            var recomputed = await RewriteEverySlotAndComputeRootAsync(state, nodes, root);

            Assert.NotEqual(root, recomputed);
            Assert.Equal(32, recomputed.Length);
        }
    }
}
