using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class SnapAccountRangeConsumer
    {
        private readonly ConsumerContext _ctx;
        private readonly SnapSyncClient _client;
        private readonly int _consumerIdx;
        private readonly SnapTaskSet _taskSet;
        private readonly ConsumerState _state = new ConsumerState();

        private AccountEncoder _accountDecoder;

        public SnapAccountRangeConsumer(ConsumerContext ctx, int consumerIdx)
        {
            _ctx = ctx;
            _client = ctx.Client;
            _consumerIdx = consumerIdx;
            _taskSet = ctx.TaskSet;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            _accountDecoder = new AccountEncoder();
            _state.ReqId = (ulong)(_consumerIdx + 1) * 1_000_000UL;
            _state.FetchFailures = 0;
            _state.SameStageFailures = 0;
            var idle = new ConsumerIdleWatch(_ctx, _consumerIdx);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                idle.NarratePeriodicTaskSetState();

                await _client.WaitWhileStateBackpressuredAsync(_consumerIdx, ct).ConfigureAwait(false);

                var lease = _taskSet.LeaseNext();
                if (lease == null)
                {
                    if (await idle.OnIdleAsync(ct).ConfigureAwait(false) == ConsumerIdleWatch.IdleOutcome.AllDone)
                        return;
                    continue;
                }
                idle.Reset();

                await DispatchAsync(lease, ct).ConfigureAwait(false);
            }
        }

        private async Task DispatchAsync(SnapFragment lease, CancellationToken ct)
        {
            if (lease is SnapFragment.StorageSubtask storageFrag)
            {
                await ProcessStorageSubtaskLeaseAsync(storageFrag, ct).ConfigureAwait(false);
                return;
            }
            if (lease is SnapFragment.SmallStorageBatch smallBatchFrag)
            {
                await ProcessSmallStorageBatchLeaseAsync(smallBatchFrag, ct).ConfigureAwait(false);
                return;
            }
            if (lease is SnapFragment.Bytecode bytecodeFrag)
            {
                await ProcessBytecodeLeaseAsync(bytecodeFrag, ct).ConfigureAwait(false);
                return;
            }
            if (lease is not SnapFragment.AccountRange frag)
                throw new NotSupportedException($"snap.consumer.unsupported_fragment kind={lease.GetType().Name}");
            await new AccountRangeLease(_ctx, _consumerIdx, _state, _accountDecoder, frag).RunAsync(ct).ConfigureAwait(false);
        }

        private async Task ProcessStorageSubtaskLeaseAsync(SnapFragment.StorageSubtask storageFrag, CancellationToken ct)
        {
            var storageStateRoot = _ctx.AttemptRoot;
            var page = new AccountWorkerResult();
            try
            {
                await new CursoredStorageSubtask(_client, _taskSet, _ctx.StorageNodeStore, _ctx.Frozen)
                    .RunAsync(storageFrag, storageStateRoot, page, ct)
                    .ConfigureAwait(false);
            }
            catch
            {
                _taskSet.Revert(storageFrag);
                throw;
            }
            _ctx.State.PublishAccountDeltas(page);
            _ctx.Checkpointer.MaybeCheckpoint(page.StorageBytesDelta);
        }

        private async Task ProcessSmallStorageBatchLeaseAsync(SnapFragment.SmallStorageBatch frag, CancellationToken ct)
        {
            var stateRoot = _ctx.AttemptRoot;
            var page = new AccountWorkerResult();

            var accounts = new List<(byte[] Hash, byte[] Root)>(frag.Items.Length);
            foreach (var item in frag.Items) accounts.Add((item.AccountHash, item.StorageRoot));

            try
            {
                var fullyProcessed = await new BatchStorageFetch(
                        _client,
                        StorageFetchContext.Create(_ctx, _state, page, frag.TaskIndex, NoProductiveSignal, NoFailureSignal))
                    .RunAsync(stateRoot, accounts, ct)
                    .ConfigureAwait(false);

                if (!fullyProcessed)
                {
                    _taskSet.Revert(frag);
                    return;
                }

                var outcomes = new List<SmallStorageOutcome>(frag.Items.Length);
                foreach (var item in frag.Items)
                    outcomes.Add(new SmallStorageOutcome(item.AccountHash, SmallStorageResult.Done, null));

                _taskSet.CompleteSmallBatch(frag, outcomes, stateRoot);
                _ctx.State.PublishAccountDeltas(page);
            }
            catch
            {
                _taskSet.Revert(frag);
                throw;
            }
        }

        private async Task ProcessBytecodeLeaseAsync(SnapFragment.Bytecode frag, CancellationToken ct)
        {
            try
            {
                await AccountRangeLease.FetchAndPublishBytecodesAsync(
                    _ctx, _state, new List<byte[]>(frag.Hashes), NoProductiveSignal, ct).ConfigureAwait(false);
            }
            catch
            {
                _taskSet.Revert(frag);
                throw;
            }
        }

        private static void NoProductiveSignal() { }

        private static void NoFailureSignal() { }
    }
}
