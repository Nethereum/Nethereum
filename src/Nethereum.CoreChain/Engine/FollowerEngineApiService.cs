using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;

namespace Nethereum.CoreChain.Engine
{
    public sealed class FollowerEngineApiService : IEngineApiService
    {
        private const int UnknownPayloadCode = -38001;
        private const int UnsupportedForkCode = -38005;
        private const int TooDeepReorgCode = -38006;
        private const int GenericServerErrorCode = -32000;

        private readonly IChainStoreBundle _bundle;
        private readonly AsyncGate _gate;
        private readonly ChainConfig _config;
        private readonly FollowerOptions _options;
        private readonly EnginePayloadStash _stash;
        private readonly EngineHeaderResolver _resolver;
        private readonly IEngineForkchoiceSink _sink;
        private readonly ILogger _logger;
        private readonly InvalidAncestry _invalid = new InvalidAncestry();

        public FollowerEngineApiService(
            IChainStoreBundle bundle,
            AsyncGate gate,
            ChainConfig config,
            FollowerOptions options,
            EnginePayloadStash stash,
            EngineHeaderResolver resolver,
            IEngineForkchoiceSink sink,
            ILogger logger = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _stash = stash ?? throw new ArgumentNullException(nameof(stash));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _logger = logger ?? NullLogger.Instance;
        }

        public Task<PayloadStatusV1> NewPayloadAsync(
            ExecutionPayloadV3 payload, string parentBeaconBlockRoot, CancellationToken ct = default) =>
            NewPayloadCoreAsync(payload, parentBeaconBlockRoot, requestsHash: null, ct);

        public Task<PayloadStatusV1> NewPayloadV4Async(
            ExecutionPayloadV3 payload, string[] executionRequests, string parentBeaconBlockRoot, CancellationToken ct = default) =>
            NewPayloadCoreAsync(payload, parentBeaconBlockRoot, EnginePayloadDecoder.RequestsHashFor(executionRequests), ct);

        public Task<PayloadStatusV1> NewPayloadV5Async(
            ExecutionPayloadV4 payload, string[] executionRequests, string parentBeaconBlockRoot, CancellationToken ct = default) =>
            throw new RpcException(UnsupportedForkCode, "Unsupported fork: engine_newPayloadV5 is not served by the follower");

        public Task<ForkchoiceUpdatedResponseV1> ForkchoiceUpdatedAsync(
            ForkchoiceStateV1 state, PayloadAttributesV3 attributes,
            IReadOnlyList<ISignedTransaction> pendingTransactions = null, CancellationToken ct = default) =>
            ForkchoiceCoreAsync(state, attributes != null, ct);

        public Task<ForkchoiceUpdatedResponseV1> ForkchoiceUpdatedV4Async(
            ForkchoiceStateV1 state, PayloadAttributesV4 attributes,
            IReadOnlyList<ISignedTransaction> pendingTransactions = null, CancellationToken ct = default) =>
            ForkchoiceCoreAsync(state, attributes != null, ct);

        public Task<ExecutionPayloadV3> GetPayloadAsync(string payloadId, CancellationToken ct = default) =>
            throw new RpcException(UnknownPayloadCode, "Unknown payload: the follower does not build payloads");

        public Task<GetPayloadV4Response> GetPayloadV4Async(string payloadId, CancellationToken ct = default) =>
            throw new RpcException(UnknownPayloadCode, "Unknown payload: the follower does not build payloads");

        public Task<GetPayloadV6Response> GetPayloadV6Async(string payloadId, CancellationToken ct = default) =>
            throw new RpcException(UnknownPayloadCode, "Unknown payload: the follower does not build payloads");

        private async Task<PayloadStatusV1> NewPayloadCoreAsync(
            ExecutionPayloadV3 payload, string parentBeaconBlockRoot, byte[] requestsHash, CancellationToken ct)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            DecodedEnginePayload decoded;
            try
            {
                decoded = EnginePayloadDecoder.Decode(
                    payload, parentBeaconBlockRoot, requestsHash, declaredBlockAccessListRlp: null, slotNumber: null, _config);
            }
            catch (InvalidEnginePayloadException ex)
            {
                return Invalid(null, ex.Message);
            }
            var header = decoded.Header;

            byte[] blockHash;
            try
            {
                blockHash = BlockHashCalculator.ForFork(header, decoded.Fork);
            }
            catch (Exception ex)
            {
                return Invalid(null, "cannot compute the block hash: " + ex.Message);
            }

            if (!ByteUtil.AreEqual(blockHash, decoded.DeclaredBlockHash))
            {
                return new PayloadStatusV1
                {
                    Status = EnginePayloadStatus.InvalidBlockHash,
                    ValidationError = $"block hash mismatch: computed {blockHash.ToHex(true)}, declared {decoded.DeclaredBlockHash.ToHex(true)}"
                };
            }

            var block = new BlockBundle(
                header, decoded.Transactions, new List<BlockHeader>(), decoded.Withdrawals ?? new List<Withdrawal>(), blockHash);
            var number = (ulong)header.BlockNumber;

            if (await IsExecutedCanonicalAsync(number, blockHash).ConfigureAwait(false))
                return Valid(blockHash);

            if (_invalid.TryGet(blockHash, out var knownInvalidLatest) || _invalid.TryGet(header.ParentHash, out knownInvalidLatest))
            {
                _invalid.Add(blockHash, knownInvalidLatest);
                return Invalid(knownInvalidLatest, "the payload or its ancestor was already found invalid");
            }

            var parent = await FindHeaderAsync(header.ParentHash).ConfigureAwait(false);
            if (parent == null)
            {
                _stash.Put(block);
                return Syncing();
            }

            if (header.Timestamp <= parent.Timestamp)
                return Invalid(header.ParentHash, "invalid timestamp: not after the parent's");

            if (_gate.Executor == null || !_gate.HasHead)
            {
                _stash.Put(block);
                return Syncing();
            }

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (await IsExecutedCanonicalAsync(number, blockHash).ConfigureAwait(false))
                    return Valid(blockHash);

                if (!ByteUtil.AreEqual(_gate.HeadHash, header.ParentHash))
                {
                    _stash.Put(block);
                    return new PayloadStatusV1 { Status = EnginePayloadStatus.Accepted };
                }

                var failure = await ExecuteAndRecordAsync(block, ct).ConfigureAwait(false);
                if (failure != null)
                    return Invalid(header.ParentHash, failure);
                return Valid(blockHash);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<ForkchoiceUpdatedResponseV1> ForkchoiceCoreAsync(
            ForkchoiceStateV1 state, bool hasPayloadAttributes, CancellationToken ct)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var headHash = state.HeadBlockHash.HexToByteArray();
            if (IsZero(headHash)) return Respond(Invalid(null, "forkchoice head is the zero hash"));

            if (_invalid.TryGet(headHash, out var invalidLatest))
                return Respond(Invalid(invalidLatest, "the forkchoice head was already found invalid"));

            if (hasPayloadAttributes)
                throw new RpcException(-38003, "Invalid payload attributes: the follower does not build payloads");

            var finalized = await ResolveOptionalAsync(state.FinalizedBlockHash, ct).ConfigureAwait(false);
            var status = await ApplyHeadAsync(headHash, finalized, ct).ConfigureAwait(false);

            if (finalized != null) _stash.PruneBelow((ulong)finalized.Header.BlockNumber);
            return Respond(status);
        }

        private async Task<PayloadStatusV1> ApplyHeadAsync(byte[] headHash, EngineForkchoiceHead finalized, CancellationToken ct)
        {
            if (_gate.HasHead && ByteUtil.AreEqual(_gate.HeadHash, headHash))
            {
                await PublishAsync(headHash, finalized, ct).ConfigureAwait(false);
                return Valid(headHash);
            }

            if (_stash.TryGet(headHash, out var stashedHead) && _gate.HasHead && _gate.Executor != null)
            {
                var outcome = await AdoptStashedBranchAsync(stashedHead, ct).ConfigureAwait(false);
                if (outcome != null)
                {
                    if (outcome.Status == EnginePayloadStatus.Valid) await PublishAsync(headHash, finalized, ct).ConfigureAwait(false);
                    return outcome;
                }
            }
            else if (_gate.HasHead && await IsExecutedCanonicalAsync(await NumberOfAsync(headHash).ConfigureAwait(false), headHash).ConfigureAwait(false))
            {
                return Valid(headHash);
            }

            await PublishAsync(headHash, finalized, ct).ConfigureAwait(false);
            return Syncing();
        }

        private async Task<PayloadStatusV1> AdoptStashedBranchAsync(BlockBundle head, CancellationToken ct)
        {
            var branch = new List<BlockBundle> { head };
            var cursor = head;
            while (true)
            {
                var parentHash = cursor.Header.ParentHash;
                var parentNumber = (ulong)cursor.Header.BlockNumber - 1;
                if (await IsExecutedCanonicalAsync(parentNumber, parentHash).ConfigureAwait(false) && parentNumber <= _gate.HeadNumber)
                    break;
                if (!_stash.TryGet(parentHash, out cursor)) return null;
                branch.Insert(0, cursor);
            }

            var ancestorNumber = (ulong)branch[0].Header.BlockNumber - 1;
            var ancestorHash = branch[0].Header.ParentHash;

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_gate.HeadNumber > ancestorNumber)
                {
                    var depth = _gate.HeadNumber - ancestorNumber;
                    if (depth > _options.MaxReorgDepth)
                        throw new RpcException(TooDeepReorgCode, $"Too deep reorg: {depth} blocks");

                    await RewindGateToAsync(ancestorNumber, ancestorHash, ct).ConfigureAwait(false);
                }

                var latestValid = ancestorHash;
                foreach (var block in branch)
                {
                    var failure = await ExecuteAndRecordAsync(block, ct).ConfigureAwait(false);
                    if (failure != null) return Invalid(latestValid, failure);
                    latestValid = block.HeaderHash;
                }
            }
            finally
            {
                _gate.Release();
            }

            return Valid(head.HeaderHash);
        }

        public async Task SetHeadAsync(ulong target, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_gate.Executor == null || !_gate.HasHead)
                    throw new RpcException(GenericServerErrorCode, "Server error: the follower has not started");
                if (target > _gate.HeadNumber)
                    throw new RpcException(GenericServerErrorCode, "not allowed to rewind to a future block");
                if (target == _gate.HeadNumber) return;

                var hash = await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(target)).ConfigureAwait(false);
                var header = await _bundle.Blocks.GetByNumberAsync(target).ConfigureAwait(false);
                if (hash == null || header == null)
                    throw new RpcException(GenericServerErrorCode, $"Server error: block {target} is not in the store");

                await RewindGateToAsync(target, hash, ct).ConfigureAwait(false);
                var head = new EngineForkchoiceHead(header, hash);
                _sink.Publish(head, head);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task RewindGateToAsync(ulong ancestorNumber, byte[] ancestorHash, CancellationToken ct)
        {
            var rewind = await ReorgRewindCoordinator.RewindToAncestorAsync(
                _bundle, _gate.Executor, ancestorNumber, RewindScope.ExecutionAndChain,
                _gate.HeadNumber, 0, 0, _logger, ct).ConfigureAwait(false);
            if (rewind.Terminal != null)
                throw new RpcException(GenericServerErrorCode, "Server error: " + rewind.Terminal.Detail);

            _gate.SetHead(ancestorNumber, ancestorHash);
        }

        private async Task<string> ExecuteAndRecordAsync(BlockBundle block, CancellationToken ct)
        {
            var number = (ulong)block.Header.BlockNumber;
            (_bundle as IAtomicBlockFlush)?.ArmWithdrawals(number, block.Withdrawals);
            var result = await _gate.Executor.ProcessBlockAsync(
                block.Header, block.Transactions, block.Uncles, block.Withdrawals, ct).ConfigureAwait(false);

            if (!result.RootMatches)
            {
                _invalid.Add(block.HeaderHash, block.Header.ParentHash, result);
                _stash.Remove(block.HeaderHash);
                return result.DescribeRejection();
            }

            _bundle.Metadata.SaveExecutedHash(number, block.HeaderHash);
            await FollowerEngineBase.CommitMatchedBlockAsync(
                _bundle, number, block.HeaderHash, block, result.ComputedStateRoot, _options, _logger, ct).ConfigureAwait(false);
            _gate.SetHead(number, block.HeaderHash);
            _stash.Remove(block.HeaderHash);
            return null;
        }

        private async Task PublishAsync(byte[] headHash, EngineForkchoiceHead finalized, CancellationToken ct)
        {
            var header = await _resolver.ResolveAsync(headHash, ct).ConfigureAwait(false);
            if (header == null) return;
            _sink.Publish(new EngineForkchoiceHead(header, headHash), finalized);
        }

        private async Task<EngineForkchoiceHead> ResolveOptionalAsync(string hashHex, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(hashHex)) return null;
            var hash = hashHex.HexToByteArray();
            if (IsZero(hash)) return null;
            var header = await _resolver.ResolveAsync(hash, ct).ConfigureAwait(false);
            return header == null ? null : new EngineForkchoiceHead(header, hash);
        }

        private async Task<BlockHeader> FindHeaderAsync(byte[] hash)
        {
            if (_stash.TryGet(hash, out var stashed)) return stashed.Header;
            return await _bundle.Blocks.GetByHashAsync(hash).ConfigureAwait(false);
        }

        private async Task<ulong> NumberOfAsync(byte[] hash)
        {
            var header = await _bundle.Blocks.GetByHashAsync(hash).ConfigureAwait(false);
            return header == null ? ulong.MaxValue : (ulong)header.BlockNumber;
        }

        private async Task<bool> IsExecutedCanonicalAsync(ulong number, byte[] hash)
        {
            if (!_gate.HasHead || number > _gate.HeadNumber) return false;
            var canonical = await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(number)).ConfigureAwait(false);
            return canonical != null && ByteUtil.AreEqual(canonical, hash);
        }

        private static bool IsZero(byte[] hash)
        {
            if (hash == null) return true;
            foreach (var b in hash) if (b != 0) return false;
            return true;
        }

        private static PayloadStatusV1 Valid(byte[] latestValid) =>
            new PayloadStatusV1 { Status = EnginePayloadStatus.Valid, LatestValidHash = latestValid.ToHex(true) };

        private static PayloadStatusV1 Syncing() => new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing };

        private static PayloadStatusV1 Invalid(byte[] latestValid, string error) =>
            new PayloadStatusV1
            {
                Status = EnginePayloadStatus.Invalid,
                LatestValidHash = latestValid?.ToHex(true),
                ValidationError = error
            };

        private static ForkchoiceUpdatedResponseV1 Respond(PayloadStatusV1 status) =>
            new ForkchoiceUpdatedResponseV1 { PayloadStatus = status };
    }
}
