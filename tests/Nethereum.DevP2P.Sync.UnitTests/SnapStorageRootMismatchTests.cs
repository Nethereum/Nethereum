using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapStorageRootMismatchTests
    {
        private static readonly byte[] Owner = Enumerable.Repeat((byte)0x70, 32).ToArray();
        private static readonly byte[] WrongRoot = Enumerable.Repeat((byte)0xEE, 32).ToArray();

        private static async Task WriteSlotsAndEndAsync(Nethereum.DevP2P.Sync.Abstractions.ISnapSyncSink sink)
        {
            var scope = await sink.BeginAccountStorageAsync(Owner, WrongRoot, CancellationToken.None);
            for (byte i = 1; i <= 3; i++)
                await scope.WriteSlotAsync(Enumerable.Repeat(i, 32).ToArray(), Nethereum.RLP.RLP.EncodeElement(new[] { i }), CancellationToken.None);
            await scope.EndAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Given_ATrieSinkStorageScopeWhoseCompletedRootDiffersFromTheExpectedRoot_When_ItCompletes_Then_ItDoesNotThrow()
        {
            var sink = new TrieSnapSyncSink(new InMemoryContentNodeStore(), new InMemoryStateStore(), flatWriter: null);

            await WriteSlotsAndEndAsync(sink);
        }

        [Fact]
        public async Task Given_AnInMemorySinkStorageScopeWhoseCompletedRootDiffersFromTheExpectedRoot_When_ItCompletes_Then_ItDoesNotThrow()
        {
            await WriteSlotsAndEndAsync(new InMemorySnapSyncSink());
        }

        [Fact]
        public void Given_ACompletedRootThatDiffersFromTheExpectedRoot_When_TheTaskSetDecides_Then_TheAccountIsMarkedNeedsHealAndAMatchIsNot()
        {
            var taskSet = new SnapTaskSet(1);
            var root = Enumerable.Repeat((byte)0x11, 32).ToArray();

            Assert.False(taskSet.MarkNeedHealIfRootMismatch(Owner, root, (byte[])root.Clone()));
            Assert.Empty(taskSet.AccountsNeedingHeal);
            Assert.True(taskSet.MarkNeedHealIfRootMismatch(Owner, root, WrongRoot));
            Assert.Single(taskSet.AccountsNeedingHeal);
            Assert.True(ByteUtil.AreEqual(Owner, taskSet.AccountsNeedingHeal[0]));
        }
    }
}
