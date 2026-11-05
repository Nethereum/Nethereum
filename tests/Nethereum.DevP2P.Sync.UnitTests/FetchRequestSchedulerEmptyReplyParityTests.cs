using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerEmptyReplyParityTests
    {
        private static readonly TimeSpan TrustedCooldownElapsed = TimeSpan.FromSeconds(21);
        private static readonly TimeSpan ParkWindow = TimeSpan.FromSeconds(2);

        private static CancellationTokenSource Within(int seconds) => new(TimeSpan.FromSeconds(seconds));

        private static Task<AccountRangeMessage> AccountRange(SnapLoopback net, byte[] root, CancellationToken ct)
            => net.Scheduler.FetchAccountRangeAsync(root, new byte[32], SnapHashRanges.FilledHash(0xff), 524_288UL, ct);

        private static Task<StorageRangesMessage> StorageRange(SnapLoopback net, byte[] root, CancellationToken ct)
            => net.Scheduler.FetchStorageRangesAsync(root, new List<byte[]> { ServedState.ContractHash }, new byte[32], SnapHashRanges.FilledHash(0xff), 524_288UL, ct);

        private static Task<ByteCodesMessage> Codes(SnapLoopback net, CancellationToken ct)
            => net.Scheduler.FetchByteCodesAsync(new List<byte[]> { ServedState.CodeHash }, 524_288UL, ct);

        private static Task<TrieNodesMessage> TrieNodes(SnapLoopback net, byte[] root, CancellationToken ct)
            => net.Scheduler.FetchTrieNodesAsync(root, ServedState.RootPath(), 524_288UL, ct);

        private static async Task<bool> ParksAsync(Task request)
            => await Task.WhenAny(request, Task.Delay(ParkWindow)) != request;

        private static async Task Abandon(Task request, CancellationTokenSource cts)
        {
            cts.Cancel();
            try { await request; } catch (OperationCanceledException) { }
        }

        private static async Task<(SnapLoopback Net, string EmptyEnode, string ServingEnode)> TrustedEmptyAndServingAsync(
            ScriptedSnapHandler empty, ScriptedSnapHandler serving, bool joinServing, FetchRequestSchedulerOptions options = null)
        {
            var net = new SnapLoopback();
            var emptyEnode = await net.ListenAsync(empty);
            var servingEnode = await net.ListenAsync(serving);
            await net.StartPoolAsync(2, options, emptyEnode);
            await net.JoinAsync(emptyEnode);
            if (joinServing) await net.JoinAsync(servingEnode);
            return (net, emptyEnode, servingEnode);
        }

        [Fact]
        public async Task Given_APeerThatAnswersAnAccountRangeWithNoAccountsAndNoProof_When_TheSameRootIsRequestedAgain_Then_ThatPeerIsNotAskedAgainAndAnotherPeerServesIt()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, servingEnode) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: false);
            await using var _ = net;
            using var cts = Within(90);

            var first = AccountRange(net, state.Root, cts.Token);
            Assert.True(await SnapLoopback.WaitUntilAsync(() => empty.Calls(SnapKind.AccountRange) == 1, TimeSpan.FromSeconds(10)));
            await net.JoinAsync(servingEnode);
            Assert.NotEmpty((await first).Accounts);
            await Task.Delay(TrustedCooldownElapsed);
            var second = await AccountRange(net, state.Root, cts.Token);

            Assert.NotEmpty(second.Accounts);
            Assert.Equal(1, empty.Calls(SnapKind.AccountRange));
            Assert.Equal(2, serving.Calls(SnapKind.AccountRange));
        }

        [Fact]
        public async Task Given_APeerThatAnsweredAnAccountRangeEmpty_When_TheTargetRootChanges_Then_ThatPeerIsAskedExactlyOnceMore()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);

            await AccountRange(net, state.Root, cts.Token);
            net.Scheduler.OnTargetRootChanged();
            var second = await AccountRange(net, state.Root, cts.Token);

            Assert.NotEmpty(second.Accounts);
            Assert.Equal(2, empty.Calls(SnapKind.AccountRange));
            Assert.Equal(2, serving.Calls(SnapKind.AccountRange));
        }

        [Fact]
        public async Task Given_APeerThatAnswersABytecodeRequestWithNoCodes_When_BytecodesAreRequestedAgain_Then_ThatPeerIsNotAskedAgainUntilTheTargetRootChanges()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);

            var first = await Codes(net, cts.Token);
            var second = await Codes(net, cts.Token);

            Assert.Equal(ServedState.Code, Assert.Single(first.Codes));
            Assert.Equal(ServedState.Code, Assert.Single(second.Codes));
            Assert.Equal(1, empty.Calls(SnapKind.ByteCodes));
            Assert.Equal(2, serving.Calls(SnapKind.ByteCodes));
        }

        [Fact]
        public async Task Given_APeerThatAnsweredBytecodesEmpty_When_TheTargetRootChanges_Then_ThatPeerIsAskedExactlyOnceMore()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);

            await Codes(net, cts.Token);
            net.Scheduler.OnTargetRootChanged();
            var second = await Codes(net, cts.Token);

            Assert.Single(second.Codes);
            Assert.Equal(2, empty.Calls(SnapKind.ByteCodes));
        }

        [Fact]
        public async Task Given_AnAccountRangeWithNoAccountsButAValidAbsenceProof_When_Received_Then_ThePeerIsNotMarkedStatelessAndTheRangeCompletes()
        {
            var state = new ServedState();
            var serving = ScriptedSnapHandler.Serving(state);
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(serving);
            await net.StartPoolAsync(1);
            var peer = await net.JoinAsync(enode);
            using var cts = Within(20);
            var beyondEveryAccount = SnapHashRanges.FilledHash(0xff);

            var first = await net.Scheduler.FetchAccountRangeAsync(state.Root, beyondEveryAccount, beyondEveryAccount, 524_288UL, cts.Token);
            var second = await net.Scheduler.FetchAccountRangeAsync(state.Root, beyondEveryAccount, beyondEveryAccount, 524_288UL, cts.Token);

            Assert.Empty(first.Accounts);
            Assert.NotEmpty(first.Proof);
            Assert.NotEmpty(second.Proof);
            Assert.Equal(2, serving.Calls(SnapKind.AccountRange));
            Assert.False(net.Scheduler.IsSnapStateQuarantined(peer.Id));
        }

        [Fact]
        public async Task Given_APeerThatAnswersEmptyAccountAndStorageRangesAtManyRoots_When_EachRootIsRequested_Then_ThePeerIsNeitherQuarantinedNorDropped()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange, SnapKind.StorageRange);
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(empty);
            await net.StartPoolAsync(1);
            var peer = await net.JoinAsync(enode);

            for (byte r = 1; r <= 3; r++)
            {
                var accountRoot = SnapHashRanges.FilledHash(r);
                var storageRoot = SnapHashRanges.FilledHash((byte)(0x10 + r));
                await AnsweredEmptyThenAbandonAsync(token => AccountRange(net, accountRoot, token), () => empty.Calls(SnapKind.AccountRange) == r);
                await AnsweredEmptyThenAbandonAsync(token => StorageRange(net, storageRoot, token), () => empty.Calls(SnapKind.StorageRange) == r);
            }
            await Task.Delay(500);

            Assert.Equal(3, empty.Calls(SnapKind.AccountRange));
            Assert.Equal(3, empty.Calls(SnapKind.StorageRange));
            Assert.False(net.Scheduler.IsSnapStateQuarantined(peer.Id));
            Assert.Equal(0, net.Scheduler.GetSnapQuarantineCyclesForTest(peer.Id));
            Assert.Contains(net.Pool.ActivePeers, p => p.Id == peer.Id);
        }

        private static async Task AnsweredEmptyThenAbandonAsync(Func<CancellationToken, Task> request, Func<bool> answered)
        {
            using var cts = Within(30);
            var pending = request(cts.Token);
            Assert.True(await SnapLoopback.WaitUntilAsync(answered, TimeSpan.FromSeconds(10)), "the peer was not asked");
            await Abandon(pending, cts);
        }

        [Fact]
        public async Task Given_EverySnapPeerIsStatelessForARoot_When_AnAccountRangeAtThatRootIsRequested_Then_NoRequestIsSentUntilAPeerJoinsAndOnlyTheNewPeerIsAsked()
        {
            var state = new ServedState();
            var first = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange);
            var second = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange);
            var serving = ScriptedSnapHandler.Serving(state);
            await using var net = new SnapLoopback();
            var firstEnode = await net.ListenAsync(first);
            var secondEnode = await net.ListenAsync(second);
            var servingEnode = await net.ListenAsync(serving);
            await net.StartPoolAsync(3, null, firstEnode, secondEnode);
            await net.JoinAsync(firstEnode);
            await net.JoinAsync(secondEnode);
            using var cts = Within(90);

            var request = AccountRange(net, state.Root, cts.Token);
            Assert.True(await SnapLoopback.WaitUntilAsync(() => first.Calls(SnapKind.AccountRange) + second.Calls(SnapKind.AccountRange) == 2, TimeSpan.FromSeconds(10)));
            Assert.True(await ParksAsync(request), "with every snap peer stateless the account-range request did not park");
            await Task.Delay(TrustedCooldownElapsed);
            var retried = AccountRange(net, state.Root, cts.Token);
            Assert.True(await ParksAsync(retried), "a retried account-range request at the stateless root did not park");
            Assert.Equal(1, first.Calls(SnapKind.AccountRange));
            Assert.Equal(1, second.Calls(SnapKind.AccountRange));
            await net.JoinAsync(servingEnode);
            var served = await request;
            var servedRetry = await retried;

            Assert.NotEmpty(served.Accounts);
            Assert.NotEmpty(servedRetry.Accounts);
            Assert.Equal(2, serving.Calls(SnapKind.AccountRange));
            Assert.Equal(1, first.Calls(SnapKind.AccountRange));
            Assert.Equal(1, second.Calls(SnapKind.AccountRange));
        }

        [Fact]
        public async Task Given_EverySnapPeerIsStatelessForARoot_When_TheTargetRootChanges_Then_EachPeerIsAskedOnceAtTheNewRoot()
        {
            var state = new ServedState();
            var first = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange);
            var second = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange);
            await using var net = new SnapLoopback();
            var firstEnode = await net.ListenAsync(first);
            var secondEnode = await net.ListenAsync(second);
            await net.StartPoolAsync(2);
            await net.JoinAsync(firstEnode);
            await net.JoinAsync(secondEnode);

            await AnsweredEmptyThenAbandonAsync(token => AccountRange(net, state.Root, token),
                () => first.Calls(SnapKind.AccountRange) + second.Calls(SnapKind.AccountRange) == 2);
            net.Scheduler.OnTargetRootChanged();
            using var cts = Within(30);
            var atNewRoot = AccountRange(net, state.OtherRoot, cts.Token);
            Assert.True(await SnapLoopback.WaitUntilAsync(
                () => first.Calls(SnapKind.AccountRange, state.OtherRoot) + second.Calls(SnapKind.AccountRange, state.OtherRoot) == 2, TimeSpan.FromSeconds(10)));
            Assert.True(await ParksAsync(atNewRoot));
            await Abandon(atNewRoot, cts);

            Assert.Equal(1, first.Calls(SnapKind.AccountRange, state.OtherRoot));
            Assert.Equal(1, second.Calls(SnapKind.AccountRange, state.OtherRoot));
        }

        [Fact]
        public async Task Given_NoSnapCapablePeerIsConnected_When_TrieNodesAreRequested_Then_TheClaimParksAndTheFirstJoiningPeerServesIt()
        {
            var state = new ServedState();
            var serving = ScriptedSnapHandler.Serving(state);
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(serving);
            await net.StartPoolAsync(1);
            using var cts = Within(60);

            var request = TrieNodes(net, state.Root, cts.Token);
            Assert.True(await ParksAsync(request), "with no snap peer the trie-node request did not park");
            await net.JoinAsync(enode);

            Assert.Single((await request).Nodes);
            Assert.Equal(1, serving.Calls(SnapKind.TrieNodes));
        }

        [Fact]
        public async Task Given_NoSnapCapablePeerIsConnected_When_AnAccountRangeIsRequested_Then_TheClaimParksAndTheFirstJoiningPeerServesIt()
        {
            var state = new ServedState();
            var serving = ScriptedSnapHandler.Serving(state);
            await using var net = new SnapLoopback();
            var enode = await net.ListenAsync(serving);
            await net.StartPoolAsync(1);
            using var cts = Within(60);

            var request = AccountRange(net, state.Root, cts.Token);
            Assert.True(await ParksAsync(request), "with no snap peer the account-range request did not park");
            await net.JoinAsync(enode);

            Assert.NotEmpty((await request).Accounts);
        }

        [Fact]
        public async Task Given_DefaultSchedulerOptions_When_SixteenConcurrentTrieNodeFetchesAtOneRootTargetAPeerThatAnswersEmpty_Then_ThatPeerReceivesExactlyOneRequest()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.TrieNodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true, new FetchRequestSchedulerOptions());
            await using var _ = net;
            using var cts = Within(60);

            var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => TrieNodes(net, state.Root, cts.Token)));

            Assert.All(results, r => Assert.Single(r.Nodes));
            Assert.Equal(1, empty.Calls(SnapKind.TrieNodes));
        }

        [Fact]
        public async Task Given_APeerMarkedByAnEmptyBytecodeReply_When_ThatNodeDisconnectsAndReconnects_Then_TheReconnectedPeerIsAskedOnce()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, emptyEnode, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);
            await Codes(net, cts.Token);
            var before = net.Pool.ActivePeers.Single(p => p.Enode == emptyEnode);

            await net.Pool.DropAsync(before.Id, "test reconnect", CancellationToken.None);
            Assert.True(await SnapLoopback.WaitUntilAsync(
                () => net.Pool.ActivePeers.OfType<Peering.SyncPeerSession>().Any(p => ((IEthPeer)p).Enode == emptyEnode && p.Id != before.Id && p.SupportsSnap),
                TimeSpan.FromSeconds(20)), "the trusted node did not reconnect");
            await Codes(net, cts.Token);
            await Codes(net, cts.Token);

            Assert.Equal(2, empty.Calls(SnapKind.ByteCodes));
        }

        [Fact]
        public async Task Given_ATrustedPeerThatAnswersEveryBytecodeRequestEmpty_When_Phase2WritesAPagesBytecodes_Then_AnotherPeerServesThemAndNothingIsDeferred()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            var stateStore = new InMemoryStateStore();
            var log = new RecordingLogger<SnapSyncClient>();
            var client = new SnapSyncClient(new SchedulerSnapPeer(net.Scheduler),
                new TrieSnapSyncSink(new InMemoryContentNodeStore(), stateStore, (ISnapFlatStateWriter)null), logger: log)
            {
                BytecodeDeadEndNoProgressRounds = 3,
                AccountConcurrency = 1,
                LargeContractConcurrency = 1,
            };
            using var cts = Within(120);

            await client.SyncStateAsync(state.Root, null, null, cts.Token);

            Assert.Equal(ServedState.Code, await stateStore.GetCodeAsync(ServedState.CodeHash));
            Assert.Equal(0, log.Count("bytecode_obligation.deferred_to_heal"));
            Assert.Equal(1, empty.Calls(SnapKind.ByteCodes));
        }

        [Fact]
        public async Task Given_APeerThatAnsweredABytecodeRequestEmptyAtTheCurrentRoot_When_AnAccountRangeStorageRangeOrTrieNodesAreRequestedAtThatRoot_Then_ThePeerIsNotAsked()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);
            await AccountRange(net, state.Root, cts.Token);
            await Codes(net, cts.Token);

            Assert.NotEmpty((await AccountRange(net, state.Root, cts.Token)).Accounts);
            Assert.NotEmpty((await StorageRange(net, state.Root, cts.Token)).Slots);
            Assert.Single((await TrieNodes(net, state.Root, cts.Token)).Nodes);

            Assert.Equal(1, empty.Calls(SnapKind.AccountRange));
            Assert.Equal(1, empty.Calls(SnapKind.ByteCodes));
            Assert.Equal(0, empty.Calls(SnapKind.StorageRange));
            Assert.Equal(0, empty.Calls(SnapKind.TrieNodes));
        }

        [Fact]
        public async Task Given_APeerStatelessAtTheCurrentRootByAnEmptyTrieNodeReply_When_BytecodesAreRequested_Then_ThePeerIsNotAsked()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.TrieNodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);
            await TrieNodes(net, state.Root, cts.Token);

            Assert.Single((await Codes(net, cts.Token)).Codes);

            Assert.Equal(1, empty.Calls(SnapKind.TrieNodes));
            Assert.Equal(0, empty.Calls(SnapKind.ByteCodes));
        }

        [Fact]
        public async Task Given_APeerMarkedByAnEmptyBytecodeReply_When_TheTargetRootChanges_Then_ItIsAskedForEveryKindAgain()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);
            await AccountRange(net, state.Root, cts.Token);
            await Codes(net, cts.Token);

            net.Scheduler.OnTargetRootChanged();
            await AccountRange(net, state.Root, cts.Token);
            await StorageRange(net, state.Root, cts.Token);
            await TrieNodes(net, state.Root, cts.Token);
            await Codes(net, cts.Token);

            Assert.Equal(2, empty.Calls(SnapKind.AccountRange));
            Assert.Equal(1, empty.Calls(SnapKind.StorageRange));
            Assert.Equal(1, empty.Calls(SnapKind.TrieNodes));
            Assert.Equal(2, empty.Calls(SnapKind.ByteCodes));
        }

        [Fact]
        public async Task Given_ABytecodeMarkAtRootR_When_RequestsMoveToRootSWithoutARootChangeCall_Then_ThePeerIsAskedAtS()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);
            await AccountRange(net, state.Root, cts.Token);
            await Codes(net, cts.Token);

            await AccountRange(net, state.OtherRoot, cts.Token);
            await Codes(net, cts.Token);

            Assert.Equal(1, empty.Calls(SnapKind.AccountRange, state.OtherRoot));
            Assert.Equal(2, empty.Calls(SnapKind.ByteCodes));
        }

        [Fact]
        public async Task Given_APeerThatAnswersEmpty_When_TheReplyIsHandled_Then_NoWarnIsLoggedAndTheTimeoutStreakIsUnchanged()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.AccountRange, SnapKind.TrieNodes, SnapKind.ByteCodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, emptyEnode, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            var peer = net.Pool.ActivePeers.Single(p => p.Enode == emptyEnode);
            using var cts = Within(60);

            await AccountRange(net, state.Root, cts.Token);
            await TrieNodes(net, state.OtherRoot, cts.Token);
            net.Scheduler.OnTargetRootChanged();
            await Codes(net, cts.Token);

            Assert.Equal(3, net.SchedulerLog.Count(LogLevel.Information, "snap.peer.stateless"));
            Assert.Equal(0, net.SchedulerLog.Count(LogLevel.Warning, "snap.peer.error"));
            Assert.Equal(0, net.Scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Snap));
            Assert.False(net.Scheduler.IsSnapStateQuarantined(peer.Id));
        }

        [Fact]
        public async Task Given_APeerStatelessForRootR_When_ItAnswersEmptyAtRootS_Then_ItIsNotAskedAtEitherRoot()
        {
            var state = new ServedState();
            var empty = ScriptedSnapHandler.EmptyFor(state, SnapKind.TrieNodes);
            var serving = ScriptedSnapHandler.Serving(state);
            var (net, _, _) = await TrustedEmptyAndServingAsync(empty, serving, joinServing: true);
            await using var _ = net;
            using var cts = Within(60);

            await TrieNodes(net, state.Root, cts.Token);
            await TrieNodes(net, state.OtherRoot, cts.Token);
            await TrieNodes(net, state.Root, cts.Token);
            await TrieNodes(net, state.OtherRoot, cts.Token);

            Assert.Equal(1, empty.Calls(SnapKind.TrieNodes, state.Root));
            Assert.Equal(1, empty.Calls(SnapKind.TrieNodes, state.OtherRoot));
        }

        private const ulong NetworkHead = 10;
        private static readonly TimeSpan BehindCooldownElapsed = TimeSpan.FromSeconds(7);

        private static async Task<bool> BenchedAfterRepeatedTimeoutsAsync(ulong headPeerBlock)
        {
            var state = new ServedState();
            var silent = ScriptedSnapHandler.Serving(state);
            silent.BeforeServing = (kind, ct) => kind == SnapKind.AccountRange ? Task.Delay(TimeSpan.FromSeconds(5), ct) : Task.CompletedTask;
            await using var net = new SnapLoopback();
            var headEnode = await net.ListenAsync(ScriptedSnapHandler.Serving(state), headBlock: headPeerBlock, serveSnap: false);
            var silentEnode = await net.ListenAsync(silent);
            await net.StartPoolAsync(2, new FetchRequestSchedulerOptions(PerRequestTimeout: TimeSpan.FromMilliseconds(500)));
            var head = await net.JoinAsync(headEnode, snap: false);
            var peer = await net.JoinAsync(silentEnode);
            Assert.Equal(headPeerBlock, head.PeerLatestBlock);
            Assert.Equal(0UL, peer.PeerLatestBlock);

            for (var timeout = 1; timeout <= 3; timeout++)
                await AnsweredEmptyThenAbandonAsync(token => AccountRange(net, state.Root, token),
                    () => net.Scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Snap) == timeout % 3);
            Assert.True(net.Scheduler.IsSnapStateQuarantined(peer.Id), "three timeouts did not bench the peer");
            await Task.Delay(BehindCooldownElapsed);
            return net.Scheduler.IsSnapStateQuarantined(peer.Id);
        }

        [Fact]
        public async Task Given_APeerBehindTheHead_When_ItTimesOutRepeatedly_Then_ItIsBenchedForTheBehindCooldown()
            => Assert.False(await BenchedAfterRepeatedTimeoutsAsync(headPeerBlock: NetworkHead));

        [Fact]
        public async Task Given_APeerAtTheHead_When_ItTimesOutRepeatedly_Then_ItStaysBenchedPastTheBehindCooldown()
            => Assert.True(await BenchedAfterRepeatedTimeoutsAsync(headPeerBlock: 0));
    }
}
