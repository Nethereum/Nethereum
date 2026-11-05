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
    public class SnapCertifyHandsDamageToTheHealTests
    {

        private static CancellationTokenSource Within(int seconds) => new(TimeSpan.FromSeconds(seconds));

        private static byte[] Fill(byte b) => Enumerable.Repeat(b, 32).ToArray();

        private static BlockHeader Header(int number, byte[] root)
            => new() { BlockNumber = number, StateRoot = root, ParentHash = Fill(0x07) };

        private static Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> Answers(BlockHeader header, byte[] hash)
            => (_, _) => Task.FromResult<(BlockHeader, byte[])?>((header, hash));
        [Fact]
        public async Task Given_ACertifySweepThatNamesMissingCode_When_CertifyRuns_Then_TheCodeIsHealedThroughThePhase3HealAndCertifyPassesOnTheSecondAttempt()
        {
            var state = new ServedState();
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            await net.JoinAsync(enode);
            var header = Header(600, state.Root);
            var hash = Fill(0x02);
            using var bundle = new ScriptedReconcileBundle(Array.Empty<(byte[], byte[])>(), unresolvableCalls: 0);
            bundle.OnReconcile = call =>
            {
                if (call == 1) bundle.MissingCode.Add(ServedState.CodeHash);
            };
            using var cts = Within(120);

            var committed = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, net.Scheduler, new SnapBootstrapper.RollingPivot(header, hash), Answers(header, hash),
                null, new RecordingLogger<SnapCertifyHandsDamageToTheHealTests>(), cts.Token);

            Assert.Equal(state.Root, committed.Header.StateRoot);
            Assert.Equal(2, bundle.ReconcileCalls);
            Assert.Empty(bundle.MissingCode);
            Assert.Equal(ServedState.Code, await bundle.State.GetCodeAsync(ServedState.CodeHash));
        }

        [Fact]
        public async Task Given_CertifyDamageAndAHealThatMustRepivot_When_CertifyRuns_Then_OnlyThePhase3HealCycleLoopRecyclesAndTheCertifierHasNoRecycleOfItsOwn()
        {
            var state = new ServedState();
            var log = new RecordingLogger<SnapCertifyHandsDamageToTheHealTests>();
            var emptyAtRoot = ScriptedSnapHandler.EmptyAt(state, state.Root);
            var servedAtRoot = 0;
            var servesTwiceAtRoot = new ScriptedSnapHandler(state.Handler(),
                (kind, root) => kind == SnapKind.TrieNodes && root != null && ByteUtil.AreEqual(root, state.Root) && Interlocked.Increment(ref servedAtRoot) > 2);
            await using var net = new SnapLoopback();
            var firstEnode = await net.ListenAsync(emptyAtRoot);
            var secondEnode = await net.ListenAsync(servesTwiceAtRoot);
            await net.StartPoolAsync(2);
            await net.JoinAsync(firstEnode);
            await net.JoinAsync(secondEnode);
            var unserved = Header(600, state.Root);
            var served = Header(610, state.OtherRoot);
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> advancesOnceWaiting = (_, _) =>
                Task.FromResult<(BlockHeader, byte[])?>(log.Count("snap.phase3.waiting_for_pivot") == 0 ? (unserved, Fill(0x02)) : (served, Fill(0x03)));
            using var bundle = new ScriptedReconcileBundle(new[] { (ServedState.ContractHash, state.StorageRoot) }, unresolvableCalls: 1);
            using var cts = Within(400);

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, net.Scheduler, new SnapBootstrapper.RollingPivot(unserved, Fill(0x02)), advancesOnceWaiting,
                null, log, cts.Token);

            Assert.Equal(state.OtherRoot, finalPivot.Header.StateRoot);
            Assert.Equal(0, log.Count("snap.flat.reheal.recycle"));
            Assert.True(log.Count("snap.phase3.recycle") >= 1, "the heal never re-pivoted through the Phase3Heal cycle loop");
        }
    }
}
