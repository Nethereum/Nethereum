using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperStatelessMarkTests
    {
        private const string FetchFailed = "fetch failed";
        private const string WaitingForPivot = "snap.phase3.waiting_for_pivot";
        private const int ServedTrieNodeRepliesAtRoot = 2;
        private static readonly TimeSpan ZeroPeerWindow = TimeSpan.FromSeconds(10);

        private static CancellationTokenSource Within(int seconds) => new(TimeSpan.FromSeconds(seconds));

        private static byte[] Fill(byte b) => Enumerable.Repeat(b, 32).ToArray();

        private static BlockHeader Header(int number, byte[] root) => new() { BlockNumber = number, StateRoot = root };

        private static Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> Answers(BlockHeader header)
            => (_, _) => Task.FromResult<(BlockHeader, byte[])?>((header, Fill(0x02)));

        private static Task<AccountRangeMessage> AccountRange(SnapLoopback net, byte[] root, CancellationToken ct)
            => net.Scheduler.FetchAccountRangeAsync(root, new byte[32], SnapHashRanges.FilledHash(0xff), 524_288UL, ct);

        private static Task<ByteCodesMessage> Codes(SnapLoopback net, CancellationToken ct)
            => net.Scheduler.FetchByteCodesAsync(new List<byte[]> { ServedState.CodeHash }, 524_288UL, ct);

        private static ScriptedReconcileBundle HoleAtTheContract(int unresolvableCalls)
            => new(new[] { (ServedState.ContractHash, new ServedState().StorageRoot) }, unresolvableCalls);

        private static async Task StartTheNextBootstrapAttemptAsync(SnapLoopback net, byte[] root)
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapBootstrapper.RunAsync(
                InMemoryChainStoreBundle.Open(), new SchedulerSnapPeer(net.Scheduler), Header(100, root), Fill(0x01),
                new RecordingLogger<SnapBootstrapperStatelessMarkTests>(), new SnapRunOptions { Scheduler = net.Scheduler }, cancelled.Token));
        }

        private static async Task<(SnapLoopback Net, string FirstEnode, string SecondEnode)> TrustedFirstAsync(
            HandlerPair handlers, bool joinSecond, FetchRequestSchedulerOptions options = null)
        {
            var net = new SnapLoopback();
            var first = await net.ListenAsync(handlers.First);
            var second = await net.ListenAsync(handlers.Second);
            await net.StartPoolAsync(2, options, first);
            await net.JoinAsync(first);
            if (joinSecond) await net.JoinAsync(second);
            return (net, first, second);
        }

        private sealed record HandlerPair(ScriptedSnapHandler First, ScriptedSnapHandler Second);

        [Fact]
        public async Task Given_APeerMarkedDuringOneBootstrapAttempt_When_TheNextAttemptStarts_Then_ThatPeerIsAskedAgain()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedFirstAsync(new(empty, serving), joinSecond: true);
            await using var _ = net;
            using var cts = Within(60);
            await Codes(net, cts.Token);
            await AccountRange(net, state.Root, cts.Token);

            await StartTheNextBootstrapAttemptAsync(net, state.Root);
            await Codes(net, cts.Token);
            await AccountRange(net, state.Root, cts.Token);

            Assert.Equal(2, empty.Calls(SnapKind.ByteCodes));
            Assert.Equal(2, empty.Calls(SnapKind.AccountRange));
        }

        [Fact]
        public async Task Given_APeerBenchedForAnInvalidProofDuringOneBootstrapAttempt_When_TheNextAttemptStarts_Then_ThePeerIsStillBenchedUntilItsCooldownEnds()
        {
            var state = new ServedState();
            var benched = ScriptedSnapHandler.Serving(state);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, benchedEnode, _) = await TrustedFirstAsync(new(benched, serving), joinSecond: true);
            await using var _ = net;
            var peer = net.Pool.ActivePeers.Single(p => p.Enode == benchedEnode);
            using var cts = Within(60);
            var verifications = 0;
            await net.Scheduler.FetchAccountRangeAsync(state.Root, new byte[32], SnapHashRanges.FilledHash(0xff), 524_288UL,
                _ => Interlocked.Increment(ref verifications) > 1, cts.Token);
            Assert.True(net.Scheduler.IsSnapStateQuarantined(peer.Id));

            await StartTheNextBootstrapAttemptAsync(net, state.Root);
            await AccountRange(net, state.Root, cts.Token);

            Assert.True(net.Scheduler.IsSnapStateQuarantined(peer.Id));
            Assert.Equal(1, benched.Calls(SnapKind.AccountRange));
            Assert.Equal(2, serving.Calls(SnapKind.AccountRange));
        }

        [Fact]
        public async Task Given_APeerBenchedForRepeatedTimeoutsDuringOneBootstrapAttempt_When_TheNextAttemptStarts_Then_ThePeerIsStillBenched()
        {
            var state = new ServedState();
            var silent = ScriptedSnapHandler.Serving(state);
            silent.BeforeServing = (kind, ct) => kind == SnapKind.AccountRange ? Task.Delay(TimeSpan.FromSeconds(5), ct) : Task.CompletedTask;
            var serving = ScriptedSnapHandler.Serving(state);
            await using var net = new SnapLoopback();
            var silentEnode = await net.ListenAsync(silent);
            var servingEnode = await net.ListenAsync(serving);
            await net.StartPoolAsync(2, new FetchRequestSchedulerOptions(PerRequestTimeout: TimeSpan.FromMilliseconds(500)));
            var peer = await net.JoinAsync(silentEnode);
            for (var timeout = 1; timeout <= 3; timeout++)
            {
                using var attempt = Within(30);
                var pending = AccountRange(net, state.Root, attempt.Token);
                Assert.True(await SnapLoopback.WaitUntilAsync(
                    () => net.Scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Snap) == timeout % 3, TimeSpan.FromSeconds(10)));
                attempt.Cancel();
                try { await pending; } catch (OperationCanceledException) { }
            }
            Assert.True(net.Scheduler.IsSnapStateQuarantined(peer.Id));

            await StartTheNextBootstrapAttemptAsync(net, state.Root);
            await net.JoinAsync(servingEnode);
            using var cts = Within(30);
            await AccountRange(net, state.Root, cts.Token);

            Assert.True(net.Scheduler.IsSnapStateQuarantined(peer.Id));
            Assert.Equal(1, serving.Calls(SnapKind.AccountRange));
        }

        [Fact]
        public async Task Given_APeerMarkedDuringHeal_When_AFlatRepairRehealStartsAtTheSameRoot_Then_ThePeerIsAskedForTrieNodes()
        {
            var state = new ServedState();
            var codeless = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedFirstAsync(new(codeless, serving), joinSecond: true);
            await using var _ = net;
            using var cts = Within(120);
            await AccountRange(net, state.Root, cts.Token);
            await Codes(net, cts.Token);
            var header = Header(600, state.Root);

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                HoleAtTheContract(unresolvableCalls: 1), net.Scheduler, new SnapBootstrapper.RollingPivot(header, Fill(0x02)), Answers(header),
                null, new RecordingLogger<SnapBootstrapperStatelessMarkTests>(), cts.Token);

            Assert.Equal(state.Root, finalPivot.Header.StateRoot);
            Assert.True(codeless.Calls(SnapKind.TrieNodes, state.Root) > 0, "the peer marked before the reheal was never asked for trie nodes by it");
        }

        [Fact]
        public async Task Given_APeerThatAnswersEmptyCodeDuringAWholeTrieReheal_When_TheNextCertifyAttemptRehealsAgain_Then_ThePeerIsAskedForTrieNodesAgain()
        {
            var state = new ServedState();
            var codeless = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedFirstAsync(new(codeless, serving), joinSecond: true);
            await using var _ = net;
            using var cts = Within(180);
            var header = Header(600, state.Root);
            var bundle = HoleAtTheContract(unresolvableCalls: 2);

            await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, net.Scheduler, new SnapBootstrapper.RollingPivot(header, Fill(0x02)), Answers(header),
                null, new RecordingLogger<SnapBootstrapperStatelessMarkTests>(), cts.Token);

            var sequence = codeless.Sequence.ToList();
            var firstCode = sequence.IndexOf(SnapKind.ByteCodes);
            Assert.Equal(3, bundle.ReconcileCalls);
            Assert.True(firstCode >= 0, "the whole-trie reheal never asked the codeless peer for code");
            Assert.Contains(SnapKind.TrieNodes, sequence.Skip(firstCode + 1));
        }

        [Fact]
        public async Task Given_EveryPeerMarkedWithinOneReheal_When_ATryBanksNothing_Then_TheMarksAreKeptAndTheRehealWaitsInsteadOfReAskingEachTry()
        {
            var state = new ServedState();
            var log = new RecordingLogger<SnapBootstrapperStatelessMarkTests>();
            var emptyAtRoot = ScriptedSnapHandler.EmptyAt(state, state.Root);
            var servedAtRoot = 0;
            var servesTwiceAtRoot = new ScriptedSnapHandler(state.Handler(),
                (kind, root) => kind == SnapKind.TrieNodes && root != null && ByteUtil.AreEqual(root, state.Root) && Interlocked.Increment(ref servedAtRoot) > ServedTrieNodeRepliesAtRoot);
            await using var net = new SnapLoopback();
            var firstEnode = await net.ListenAsync(emptyAtRoot);
            var secondEnode = await net.ListenAsync(servesTwiceAtRoot);
            await net.StartPoolAsync(2);
            await net.JoinAsync(firstEnode);
            await net.JoinAsync(secondEnode);
            var unserved = Header(600, state.Root);
            var served = Header(610, state.OtherRoot);
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> advancesOnceWaiting = (_, _) =>
                Task.FromResult<(BlockHeader, byte[])?>(log.Count(WaitingForPivot) == 0 ? (unserved, Fill(0x02)) : (served, Fill(0x03)));
            using var cts = Within(400);

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                HoleAtTheContract(unresolvableCalls: 1), net.Scheduler, new SnapBootstrapper.RollingPivot(unserved, Fill(0x02)), advancesOnceWaiting,
                null, log, cts.Token);

            Assert.Equal(state.OtherRoot, finalPivot.Header.StateRoot);
            Assert.True(log.Count($"root 0x{state.Root.ToHex()} and continuing heal") >= 1, "no second reheal try ran at the same root, so per-try marks were never exercised");
            Assert.Equal(1, emptyAtRoot.Calls(SnapKind.TrieNodes, state.Root));
            Assert.Equal(ServedTrieNodeRepliesAtRoot + 1, servesTwiceAtRoot.Calls(SnapKind.TrieNodes, state.Root));
        }

        [Fact]
        public async Task Given_NoSnapPeerConnected_When_AContinuousHealCycleRuns_Then_ItParksAndProceedsWhenASnapPeerJoins()
        {
            var state = new ServedState();
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            var log = new RecordingLogger<TrieHealer>();
            var healer = new TrieHealer(net.Scheduler, new InMemoryContentNodeStore(), log, codeStore: new InMemoryStateStore());
            using var cts = Within(120);

            var heal = healer.HealAsync(state.Root, ct: cts.Token);
            await Task.Delay(ZeroPeerWindow);
            await net.JoinAsync(enode);
            var result = await heal;

            Assert.True(result.Matched);
            Assert.Equal(0, log.Count(FetchFailed));
        }

        [Fact]
        public async Task Given_NoSnapPeerConnected_When_TheHealFetchesCode_Then_ItParksAndWritesTheCodeWhenAPeerJoins()
        {
            var state = new ServedState();
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            var codeStore = new InMemoryStateStore();
            var healer = new TrieHealer(net.Scheduler, new InMemoryContentNodeStore(), codeStore: codeStore);
            using var cts = Within(120);

            var heal = healer.HealAsync(state.Root, seedCodeHeal: new[] { ServedState.CodeHash }, ct: cts.Token);
            await Task.Delay(ZeroPeerWindow);
            await net.JoinAsync(enode);
            await heal;

            Assert.Equal(ServedState.Code, await codeStore.GetCodeAsync(ServedState.CodeHash));
        }

        [Fact]
        public async Task Given_NoSnapPeerConnected_When_TheSweepBytecodeFetchRuns_Then_ItWaitsForAPeerAndWritesTheCodesInsteadOfSurfacingABootstrapRetry()
        {
            var state = new ServedState();
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            var bundle = HoleAtTheContract(unresolvableCalls: 0);
            bundle.MissingCode.Add(ServedState.CodeHash);
            using var cts = Within(120);

            var fetch = SnapBootstrapper.FetchMissingBytecodeAsync(bundle, net.Scheduler, null, state.Root,
                new RecordingLogger<SnapBootstrapperStatelessMarkTests>(), cts.Token);
            await Task.Delay(ZeroPeerWindow);
            await net.JoinAsync(enode);
            await fetch;

            Assert.Equal(ServedState.Code, await bundle.State.GetCodeAsync(ServedState.CodeHash));
            Assert.Empty(bundle.GetPersistedMissingCode());
        }

        [Fact]
        public async Task Given_NoSnapPeerConnected_When_Phase2FetchesAPagesBytecodes_Then_ItParksInsteadOfBurningNoProgressRounds()
        {
            var state = new ServedState();
            var codeAsked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var departing = ScriptedSnapHandler.Serving(state);
            departing.BeforeServing = async (kind, ct) =>
            {
                if (kind != SnapKind.ByteCodes) return;
                codeAsked.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            };
            await using var net = new SnapLoopback();
            var departingEnode = await net.ListenAsync(departing);
            var joiningEnode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(2);
            await net.JoinAsync(departingEnode);
            var stateStore = new InMemoryStateStore();
            var log = new RecordingLogger<SnapSyncClient>();
            var client = new SnapSyncClient(new SchedulerSnapPeer(net.Scheduler),
                new TrieSnapSyncSink(new InMemoryContentNodeStore(), stateStore, (Nethereum.CoreChain.Storage.ISnapFlatStateWriter)null), logger: log)
            {
                BytecodeDeadEndNoProgressRounds = 2,
            };
            using var cts = Within(120);

            var sync = client.SyncStateAsync(state.Root, null, null, cts.Token);
            await codeAsked.Task.WaitAsync(cts.Token);
            await net.StopListenerAsync(departingEnode);
            Assert.True(await SnapLoopback.WaitUntilAsync(() => net.Pool.ActivePeers.Count == 0, TimeSpan.FromSeconds(20)), "the departing peer never left");
            await Task.Delay(ZeroPeerWindow);
            await net.JoinAsync(joiningEnode);
            await sync;

            Assert.Equal(ServedState.Code, await stateStore.GetCodeAsync(ServedState.CodeHash));
            Assert.Equal(0, log.Count("bytecode_obligation.deferred_to_heal"));
        }

        [Fact]
        public async Task Given_NoSnapPeerConnected_When_TheFlatRepairHealRuns_Then_ItWaitsForAPeerInsteadOfSurfacingABootstrapRetry()
        {
            var state = new ServedState();
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            var log = new RecordingLogger<SnapBootstrapperStatelessMarkTests>();
            var header = Header(600, state.Root);
            var bundle = HoleAtTheContract(unresolvableCalls: 1);
            using var cts = Within(180);

            var repair = SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, net.Scheduler, new SnapBootstrapper.RollingPivot(header, Fill(0x02)), Answers(header),
                null, log, cts.Token);
            await Task.Delay(ZeroPeerWindow);
            await net.JoinAsync(enode);
            var finalPivot = await repair;

            Assert.Equal(state.Root, finalPivot.Header.StateRoot);
            Assert.Equal(2, bundle.ReconcileCalls);
            Assert.Equal(0, log.Count(FetchFailed));
            Assert.Equal(0, log.Count(WaitingForPivot));
        }

        [Fact]
        public async Task Given_NoSnapPeerConnected_When_TheBytecodeBackstopRuns_Then_ItWaitsForAPeerAndHealsTheDeferredCodes()
        {
            var state = new ServedState();
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(ScriptedSnapHandler.Serving(state));
            await net.StartPoolAsync(1);
            using var bundle = new ScriptedReconcileBundle(System.Array.Empty<(byte[], byte[])>(), unresolvableCalls: 0);
            bundle.Metadata.SaveDeferredHealCodeBlob(NeedsHealCodeCodec.Encode(new List<byte[]> { ServedState.CodeHash }));
            var log = new RecordingLogger<SnapBootstrapperStatelessMarkTests>();
            using var cts = Within(180);

            var backstop = SnapBootstrapper.FetchMissingBytecodeAsync(bundle, net.Scheduler, null, state.Root, log, cts.Token);
            await Task.Delay(ZeroPeerWindow);
            await net.JoinAsync(enode);
            await backstop;

            Assert.Equal(ServedState.Code, await bundle.State.GetCodeAsync(ServedState.CodeHash));
            Assert.Null(bundle.Metadata.GetDeferredHealCodeBlob());
            Assert.True(log.Count("Heal round") <= 2, $"the backstop heal spun {log.Count("Heal round")} rounds with no peer instead of parking");
        }

    }
}
