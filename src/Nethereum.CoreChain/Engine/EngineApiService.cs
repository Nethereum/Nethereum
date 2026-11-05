using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;
using EngineWithdrawal = Nethereum.RPC.Eth.DTOs.Withdrawal;
using ModelWithdrawal = Nethereum.Model.Withdrawal;

namespace Nethereum.CoreChain.Engine
{
    public sealed class EngineApiService : IEngineApiService
    {
        private static readonly byte[] EmptyListHash = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());
        private static readonly byte[] ZeroHash32 = new byte[32];
        private const int TooDeepReorgCode = -38006;
        private const int GenericServerErrorCode = -32000;

        private readonly IBlockStore _blockStore;
        private readonly BlockImporter _importer;
        private readonly IBlockProducer _blockProducer;
        private readonly IChainForkChoice _forkChoice;
        private readonly IPayloadBuildRegistry _registry;
        private readonly ChainConfig _config;

        public EngineApiService(
            IBlockStore blockStore,
            BlockImporter importer,
            IBlockProducer blockProducer,
            IChainForkChoice forkChoice,
            IPayloadBuildRegistry registry,
            ChainConfig config)
        {
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _blockProducer = blockProducer ?? throw new ArgumentNullException(nameof(blockProducer));
            _forkChoice = forkChoice;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Task<PayloadStatusV1> NewPayloadAsync(
            ExecutionPayloadV3 payload,
            string parentBeaconBlockRoot,
            CancellationToken ct = default) =>
            ImportPayloadAsync(
                payload, parentBeaconBlockRoot, requestsHash: null, declaredBlockAccessListRlp: null, slotNumber: null, ct);

        public Task<PayloadStatusV1> NewPayloadV4Async(
            ExecutionPayloadV3 payload,
            string[] executionRequests,
            string parentBeaconBlockRoot,
            CancellationToken ct = default) =>
            ImportPayloadAsync(
                payload,
                parentBeaconBlockRoot,
                EnginePayloadDecoder.RequestsHashFor(executionRequests),
                declaredBlockAccessListRlp: null,
                slotNumber: null,
                ct);

        public Task<PayloadStatusV1> NewPayloadV5Async(
            ExecutionPayloadV4 payload,
            string[] executionRequests,
            string parentBeaconBlockRoot,
            CancellationToken ct = default) =>
            ImportPayloadAsync(
                payload,
                parentBeaconBlockRoot,
                EnginePayloadDecoder.RequestsHashFor(executionRequests),
                payload?.BlockAccessList != null ? payload.BlockAccessList.HexToByteArray() : null,
                payload?.SlotNumber != null ? (ulong)payload.SlotNumber.Value : (ulong?)null,
                ct);

        private async Task<PayloadStatusV1> ImportPayloadAsync(
            ExecutionPayloadV3 payload,
            string parentBeaconBlockRoot,
            byte[] requestsHash,
            byte[] declaredBlockAccessListRlp,
            ulong? slotNumber,
            CancellationToken ct)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var declaredBlockAccessList = EnginePayloadDecoder.DecodeDeclaredBlockAccessList(declaredBlockAccessListRlp);

            var parentHash = payload.ParentHash.HexToByteArray();
            if (_importer.TryGetInvalidBlock(parentHash, out var invalidParentLatestValid))
            {
                return new PayloadStatusV1
                {
                    Status = EnginePayloadStatus.Invalid,
                    LatestValidHash = invalidParentLatestValid?.ToHex(true),
                    ValidationError = "the parent payload was already found invalid"
                };
            }

            var parentHeader = await _importer.Blocks.GetByHashAsync(parentHash).ConfigureAwait(false);
            if (parentHeader == null)
            {
                return new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing };
            }

            DecodedEnginePayload decoded;
            try
            {
                decoded = EnginePayloadDecoder.Decode(
                    payload, parentBeaconBlockRoot, requestsHash, declaredBlockAccessListRlp, slotNumber, _config);
            }
            catch (InvalidEnginePayloadException ex)
            {
                return EnginePayloadDecoder.InvalidStatus(ex.Message);
            }
            var header = decoded.Header;
            var transactions = decoded.Transactions;
            var withdrawalsForRoot = decoded.Withdrawals;

            var result = await _importer
                .ImportAsync(header, transactions, uncles: null, withdrawalsForRoot, declaredBlockAccessList, ct)
                .ConfigureAwait(false);

            var declaredBlockHash = payload.BlockHash.HexToByteArray();
            var hashMatches = result.BlockHash != null && ByteUtil.AreEqual(result.BlockHash, declaredBlockHash);

            if (hashMatches && result.RootMatches)
            {
                return new PayloadStatusV1
                {
                    Status = EnginePayloadStatus.Valid,
                    LatestValidHash = result.BlockHash.ToHex(true)
                };
            }

            if (hashMatches && (result.Disposition == BlockImportDisposition.StoredAsSide
                                || result.Disposition == BlockImportDisposition.AlreadySide))
            {
                return new PayloadStatusV1 { Status = EnginePayloadStatus.Accepted };
            }

            if (result.Disposition == BlockImportDisposition.UnknownParent)
            {
                return new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing };
            }

            return new PayloadStatusV1
            {
                Status = EnginePayloadStatus.Invalid,
                LatestValidHash = (result.LatestValidHash ?? parentHash).ToHex(true),
                ValidationError = result.DescribeRejection()
            };
        }

        public Task<ForkchoiceUpdatedResponseV1> ForkchoiceUpdatedAsync(
            ForkchoiceStateV1 state,
            PayloadAttributesV3 attributes,
            IReadOnlyList<ISignedTransaction> pendingTransactions = null,
            CancellationToken ct = default) =>
            ForkchoiceUpdatedCoreAsync(state, attributes, BuildProductionOptions, pendingTransactions, ct);

        public Task<ForkchoiceUpdatedResponseV1> ForkchoiceUpdatedV4Async(
            ForkchoiceStateV1 state,
            PayloadAttributesV4 attributes,
            IReadOnlyList<ISignedTransaction> pendingTransactions = null,
            CancellationToken ct = default) =>
            ForkchoiceUpdatedCoreAsync(state, attributes, BuildProductionOptionsV4, pendingTransactions, ct);

        private async Task<ForkchoiceUpdatedResponseV1> ForkchoiceUpdatedCoreAsync<TAttributes>(
            ForkchoiceStateV1 state,
            TAttributes attributes,
            Func<TAttributes, BlockProductionOptions> buildOptions,
            IReadOnlyList<ISignedTransaction> pendingTransactions,
            CancellationToken ct)
            where TAttributes : class
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var headHash = state.HeadBlockHash.HexToByteArray();
            if (_importer.TryGetInvalidBlock(headHash, out var invalidLatestValid))
            {
                return new ForkchoiceUpdatedResponseV1
                {
                    PayloadStatus = new PayloadStatusV1
                    {
                        Status = EnginePayloadStatus.Invalid,
                        LatestValidHash = invalidLatestValid?.ToHex(true)
                    }
                };
            }

            var headHeader = await _importer.Blocks.GetByHashAsync(headHash).ConfigureAwait(false);
            if (headHeader == null)
            {
                return new ForkchoiceUpdatedResponseV1
                {
                    PayloadStatus = new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing }
                };
            }

            var status = await MakeHeadCanonicalAsync(headHeader, headHash, ct).ConfigureAwait(false);
            if (status.Status == EnginePayloadStatus.Valid)
                await PruneSideBlocksBelowFinalizedAsync(state.FinalizedBlockHash).ConfigureAwait(false);

            if (attributes == null || status.Status != EnginePayloadStatus.Valid)
            {
                return new ForkchoiceUpdatedResponseV1 { PayloadStatus = status };
            }

            var options = buildOptions(attributes);
            var submitted = pendingTransactions ?? Array.Empty<ISignedTransaction>();
            var buildTask = BuildPayloadAsync(_blockProducer, submitted, options);
            var payloadId = _registry.Register(buildTask);

            return new ForkchoiceUpdatedResponseV1 { PayloadStatus = status, PayloadId = payloadId };
        }

        private async Task PruneSideBlocksBelowFinalizedAsync(string finalizedBlockHash)
        {
            if (string.IsNullOrEmpty(finalizedBlockHash)) return;

            var finalized = await _blockStore.GetByHashAsync(finalizedBlockHash.HexToByteArray()).ConfigureAwait(false);
            if (finalized != null) _importer.PruneSideBlocksBelow((ulong)finalized.BlockNumber);
        }

        private async Task<PayloadStatusV1> MakeHeadCanonicalAsync(BlockHeader headHeader, byte[] headHash, CancellationToken ct)
        {
            var valid = new PayloadStatusV1 { Status = EnginePayloadStatus.Valid, LatestValidHash = headHash.ToHex(true) };
            if (!_importer.SupportsSideChains) return valid;

            var canonicalAtNumber = await _blockStore
                .GetHashByNumberAsync((BigInteger)headHeader.BlockNumber).ConfigureAwait(false);
            if (canonicalAtNumber != null && ByteUtil.AreEqual(canonicalAtNumber, headHash)) return valid;

            var set = await _importer.SetCanonicalAsync(headHash, ct).ConfigureAwait(false);
            switch (set.Status)
            {
                case SetCanonicalStatus.Reorganised:
                case SetCanonicalStatus.NoOp:
                    return valid;
                case SetCanonicalStatus.BranchInvalid:
                    return new PayloadStatusV1
                    {
                        Status = EnginePayloadStatus.Invalid,
                        LatestValidHash = set.Reorganisation?.FailedBlockParentHash?.ToHex(true),
                        ValidationError = set.Detail
                    };
                case SetCanonicalStatus.AncestryUnavailable:
                    return new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing };
                case SetCanonicalStatus.TooDeepReorg:
                    throw new RpcException(TooDeepReorgCode, "Too deep reorg: " + set.Detail);
                default:
                    throw new RpcException(GenericServerErrorCode, "Server error: " + set.Detail);
            }
        }

        public async Task<ExecutionPayloadV3> GetPayloadAsync(string payloadId, CancellationToken ct = default)
        {
            var build = await _registry.Get(payloadId).ConfigureAwait(false);
            return ConvertToPayload(build);
        }

        public async Task<GetPayloadV4Response> GetPayloadV4Async(string payloadId, CancellationToken ct = default)
        {
            var build = await _registry.Get(payloadId).ConfigureAwait(false);

            return new GetPayloadV4Response
            {
                ExecutionPayload = ConvertToPayload(build),
                BlockValue = new HexBigInteger(BigInteger.Zero),
                BlobsBundle = new BlobsBundleV1(),
                ShouldOverrideBuilder = false,
                ExecutionRequests = EncodeExecutionRequests(build.Result.ExecutionRequests)
            };
        }

        public static GetPayloadV4Response ToGetPayloadV4Response(
            BlockProductionResult result, IReadOnlyList<ModelWithdrawal> withdrawals, BigInteger blockValue)
        {
            var build = new EnginePayloadBuild(result, result.IncludedTransactions, withdrawals);

            return new GetPayloadV4Response
            {
                ExecutionPayload = ConvertToPayload(build),
                BlockValue = new HexBigInteger(blockValue),
                BlobsBundle = new BlobsBundleV1(),
                ShouldOverrideBuilder = false,
                ExecutionRequests = EncodeExecutionRequests(result.ExecutionRequests)
            };
        }

        public async Task<GetPayloadV6Response> GetPayloadV6Async(string payloadId, CancellationToken ct = default)
        {
            var build = await _registry.Get(payloadId).ConfigureAwait(false);

            return new GetPayloadV6Response
            {
                ExecutionPayload = ConvertToPayloadV4(build),
                BlockValue = new HexBigInteger(BigInteger.Zero),
                BlobsBundle = new BlobsBundleV1(),
                ShouldOverrideBuilder = false,
                ExecutionRequests = EncodeExecutionRequests(build.Result.ExecutionRequests)
            };
        }

        private static ExecutionPayloadV4 ConvertToPayloadV4(EnginePayloadBuild build)
        {
            var payload = ConvertToPayload(build);
            var header = build.Result.Header;

            return new ExecutionPayloadV4
            {
                ParentHash = payload.ParentHash,
                FeeRecipient = payload.FeeRecipient,
                StateRoot = payload.StateRoot,
                ReceiptsRoot = payload.ReceiptsRoot,
                LogsBloom = payload.LogsBloom,
                PrevRandao = payload.PrevRandao,
                BlockNumber = payload.BlockNumber,
                GasLimit = payload.GasLimit,
                GasUsed = payload.GasUsed,
                Timestamp = payload.Timestamp,
                ExtraData = payload.ExtraData,
                BaseFeePerGas = payload.BaseFeePerGas,
                BlockHash = payload.BlockHash,
                Transactions = payload.Transactions,
                Withdrawals = payload.Withdrawals,
                BlobGasUsed = payload.BlobGasUsed,
                ExcessBlobGas = payload.ExcessBlobGas,
                BlockAccessList = (build.Result.BlockAccessListRlp ?? Array.Empty<byte>()).ToHex(true),
                SlotNumber = new HexBigInteger(header.SlotNumber ?? 0)
            };
        }

        private static async Task<EnginePayloadBuild> BuildPayloadAsync(
            IBlockProducer producer, IReadOnlyList<ISignedTransaction> transactions, BlockProductionOptions options)
        {
            var result = await producer.ProduceBlockAsync(transactions, options).ConfigureAwait(false);
            return new EnginePayloadBuild(result, transactions, options.Withdrawals == null ? null : new List<ModelWithdrawal>(options.Withdrawals));
        }

        private BlockProductionOptions BuildProductionOptions(PayloadAttributesV3 attributes) =>
            new BlockProductionOptions
            {
                Timestamp = (long)attributes.Timestamp.Value,
                Coinbase = attributes.SuggestedFeeRecipient,
                BaseFee = _config.BaseFee,
                BlockGasLimit = _config.BlockGasLimit,
                Difficulty = 0,
                PrevRandao = attributes.PrevRandao != null ? attributes.PrevRandao.HexToByteArray() : ZeroHash32,
                ExtraData = Array.Empty<byte>(),
                ChainId = _config.ChainId,
                ParentBeaconBlockRoot = attributes.ParentBeaconBlockRoot != null
                    ? attributes.ParentBeaconBlockRoot.HexToByteArray()
                    : ZeroHash32,
                Withdrawals = EnginePayloadDecoder.ToModelWithdrawals(attributes.Withdrawals)
            };

        private BlockProductionOptions BuildProductionOptionsV4(PayloadAttributesV4 attributes)
        {
            var options = BuildProductionOptions(attributes);
            options.SlotNumber = attributes.SlotNumber != null ? (ulong)attributes.SlotNumber.Value : (ulong?)null;
            return options;
        }

        private static List<string> EncodeExecutionRequests(IReadOnlyList<byte[]> executionRequests)
        {
            var encoded = new List<string>(executionRequests?.Count ?? 0);
            if (executionRequests == null) return encoded;

            foreach (var request in executionRequests)
            {
                encoded.Add(request.ToHex(true));
            }

            return encoded;
        }

        private static ExecutionPayloadV3 ConvertToPayload(EnginePayloadBuild build)
        {
            var header = build.Result.Header;

            return new ExecutionPayloadV3
            {
                ParentHash = header.ParentHash.ToHex(true),
                FeeRecipient = header.Coinbase,
                StateRoot = header.StateRoot.ToHex(true),
                ReceiptsRoot = header.ReceiptHash.ToHex(true),
                LogsBloom = header.LogsBloom.ToHex(true),
                PrevRandao = header.MixHash.ToHex(true),
                BlockNumber = header.BlockNumber.ToHexBigInteger(),
                GasLimit = new HexBigInteger(header.GasLimit),
                GasUsed = new HexBigInteger(header.GasUsed),
                Timestamp = new HexBigInteger(header.Timestamp),
                ExtraData = (header.ExtraData ?? Array.Empty<byte>()).ToHex(true),
                BaseFeePerGas = new HexBigInteger((header.BaseFee ?? EvmUInt256.Zero).ToBigInteger()),
                BlockHash = build.Result.BlockHash.ToHex(true),
                Transactions = EncodeIncludedTransactions(build),
                Withdrawals = ToEngineWithdrawals(build.Withdrawals),
                BlobGasUsed = new HexBigInteger(header.BlobGasUsed ?? 0),
                ExcessBlobGas = new HexBigInteger(header.ExcessBlobGas ?? 0)
            };
        }

        private static List<string> EncodeIncludedTransactions(EnginePayloadBuild build)
        {
            var byHash = new Dictionary<string, ISignedTransaction>(StringComparer.OrdinalIgnoreCase);
            foreach (var tx in build.SubmittedTransactions)
            {
                if (tx.Hash != null) byHash[tx.Hash.ToHex(true)] = tx;
            }

            var encoded = new List<string>(build.Result.TransactionResults.Count);
            foreach (var txResult in build.Result.TransactionResults)
            {
                var hashHex = txResult.TxHash.ToHex(true);
                if (byHash.TryGetValue(hashHex, out var tx))
                {
                    encoded.Add(tx.GetRLPEncoded().ToHex(true));
                }
            }

            return encoded;
        }

        private static List<EngineWithdrawal> ToEngineWithdrawals(IReadOnlyList<ModelWithdrawal> withdrawals)
        {
            var list = new List<EngineWithdrawal>(withdrawals.Count);
            foreach (var w in withdrawals)
            {
                list.Add(new EngineWithdrawal
                {
                    Index = new HexBigInteger(w.Index),
                    ValidatorIndex = new HexBigInteger(w.ValidatorIndex),
                    Address = w.Address.ToHex(true),
                    Amount = new HexBigInteger(w.AmountInGwei)
                });
            }

            return list;
        }
    }
}
