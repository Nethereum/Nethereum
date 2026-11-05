using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;


namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TrieHealerSeedStorageAccountPathTests
    {
        private static CancellationTokenSource Within(int seconds) => new(TimeSpan.FromSeconds(seconds));

        [Fact]
        public async Task Given_SeedStorageHealForAnAccount_When_HealAsyncRuns_Then_TheAccountsStorageIsHealedThroughTheAccountPath()
        {
            var state = new ServedState();
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            await net.JoinAsync(enode);
            var store = new InMemoryContentNodeStore();
            var healer = new TrieHealer(net.Scheduler, store, codeStore: new InMemoryStateStore());
            using var cts = Within(60);

            var result = await healer.HealAsync(
                state.Root, seedStorageHeal: new[] { (ServedState.ContractHash, state.StorageRoot) }, ct: cts.Token);

            Assert.True(result.Matched);
            Assert.True(new HashHealNodeSink(store).HasNode(true, ServedState.ContractHash, Array.Empty<byte>(), state.StorageRoot));
        }
    }
}
