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
            _forkChoice = forkChoice ?? throw new ArgumentNullException(nameof(forkChoice));
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
                RequestsHashFor(executionRequests),
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
                RequestsHashFor(executionRequests),
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

            var parentHash = payload.ParentHash.HexToByteArray();
            var parentHeader = await _blockStore.GetByHashAsync(parentHash).ConfigureAwait(false);
            if (parentHeader == null)
            {
                return new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing };
            }

            var transactions = DecodeTransactions(payload.Transactions);
            var withdrawalsForRoot = ToModelWithdrawals(payload.Withdrawals);

            var header = new BlockHeader
            {
                ParentHash = parentHash,
                UnclesHash = EmptyListHash,
                Coinbase = payload.FeeRecipient,
                StateRoot = payload.StateRoot.HexToByteArray(),
                TransactionsHash = PatriciaBlockRootsProvider.Instance.CalculateTransactionsRoot(transactions),
                ReceiptHash = payload.ReceiptsRoot.HexToByteArray(),
                LogsBloom = payload.LogsBloom.HexToByteArray(),
                Difficulty = EvmUInt256.Zero,
                BlockNumber = EvmUInt256BigIntegerExtensions.FromBigInteger(payload.BlockNumber.Value),
                GasLimit = ToHeaderScalarLong(payload.GasLimit.Value),
                GasUsed = ToHeaderScalarLong(payload.GasUsed.Value),
                Timestamp = ToHeaderScalarLong(payload.Timestamp.Value),
                ExtraData = payload.ExtraData.HexToByteArray(),
                MixHash = payload.PrevRandao.HexToByteArray(),
                Nonce = new byte[8],
                BaseFee = EvmUInt256BigIntegerExtensions.FromBigInteger(payload.BaseFeePerGas.Value),
                WithdrawalsRoot = PatriciaBlockRootsProvider.Instance.CalculateWithdrawalsRoot(withdrawalsForRoot),
                BlobGasUsed = payload.BlobGasUsed != null ? ToHeaderScalarLong(payload.BlobGasUsed.Value) : (long?)null,
                ExcessBlobGas = payload.ExcessBlobGas != null ? ToHeaderScalarLong(payload.ExcessBlobGas.Value) : (long?)null,
                ParentBeaconBlockRoot = parentBeaconBlockRoot != null
                    ? parentBeaconBlockRoot.HexToByteArray()
                    : ZeroHash32,
                RequestsHash = requestsHash,
                BlockAccessListHash = declaredBlockAccessListRlp != null
                    ? new Sha3Keccack().CalculateHash(declaredBlockAccessListRlp)
                    : null,
                SlotNumber = slotNumber
            };

            var declaredBlockAccessList = declaredBlockAccessListRlp != null
                ? BlockAccessListRLPEncoder.Current.Decode(declaredBlockAccessListRlp)
                : null;

            var result = await _importer
                .ImportAsync(header, transactions, uncles: null, withdrawalsForRoot, declaredBlockAccessList, ct)
                .ConfigureAwait(false);

            var declaredBlockHash = payload.BlockHash.HexToByteArray();

            if (result.RootMatches && result.BlockHash != null && ByteUtil.AreEqual(result.BlockHash, declaredBlockHash))
            {
                return new PayloadStatusV1
                {
                    Status = EnginePayloadStatus.Valid,
                    LatestValidHash = result.BlockHash.ToHex(true)
                };
            }

            return new PayloadStatusV1
            {
                Status = EnginePayloadStatus.Invalid,
                LatestValidHash = parentHash.ToHex(true),
                ValidationError = result.DescribeRejection()
            };
        }

        private static long ToHeaderScalarLong(BigInteger value)
        {
            if (value < 0 || value > ulong.MaxValue)
            {
                throw RpcException.InvalidParams($"value {value} is outside the uint64 range");
            }

            return unchecked((long)(ulong)value);
        }

        private static byte[] RequestsHashFor(string[] executionRequests)
        {
            if (executionRequests == null)
            {
                throw RpcException.InvalidParams("executionRequests must not be null");
            }

            var decoded = DecodeExecutionRequests(executionRequests);
            if (!Nethereum.EVM.Execution.ExecutionRequests.IsValidEngineRequestsList(decoded))
            {
                throw RpcException.InvalidParams(
                    "executionRequests elements must be ordered by request_type ascending, unique per request_type, and longer than 1 byte");
            }

            return Nethereum.EVM.Execution.ExecutionRequests.ComputeRequestsHash(decoded);
        }

        private static List<byte[]> DecodeExecutionRequests(string[] executionRequests)
        {
            var list = new List<byte[]>(executionRequests?.Length ?? 0);
            if (executionRequests == null) return list;

            foreach (var hex in executionRequests)
            {
                list.Add(hex.HexToByteArray());
            }

            return list;
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
            var headHeader = await _blockStore.GetByHashAsync(headHash).ConfigureAwait(false);
            if (headHeader == null)
            {
                return new ForkchoiceUpdatedResponseV1
                {
                    PayloadStatus = new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing }
                };
            }

            var verdict = await _forkChoice.ShouldAdoptAsync(headHeader, headHash, ct).ConfigureAwait(false);

            var status = verdict.Outcome == ForkChoiceOutcome.Undecidable
                ? new PayloadStatusV1 { Status = EnginePayloadStatus.Syncing }
                : new PayloadStatusV1 { Status = EnginePayloadStatus.Valid, LatestValidHash = headHash.ToHex(true) };

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
            return new EnginePayloadBuild(result, transactions);
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
                Withdrawals = ToModelWithdrawals(attributes.Withdrawals)
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
                Withdrawals = new List<EngineWithdrawal>(),
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

        private static List<ISignedTransaction> DecodeTransactions(List<string> raw)
        {
            var list = new List<ISignedTransaction>(raw?.Count ?? 0);
            if (raw == null) return list;

            foreach (var hex in raw)
            {
                list.Add(TransactionFactory.CreateTransaction(hex.HexToByteArray(), allowBlobNetworkWrapper: false));
            }

            return list;
        }

        private static List<ModelWithdrawal> ToModelWithdrawals(List<EngineWithdrawal> withdrawals)
        {
            var list = new List<Nethereum.Model.Withdrawal>(withdrawals?.Count ?? 0);
            if (withdrawals == null) return list;

            foreach (var w in withdrawals)
            {
                list.Add(new Nethereum.Model.Withdrawal
                {
                    Index = (ulong)w.Index.Value,
                    ValidatorIndex = (ulong)w.ValidatorIndex.Value,
                    Address = w.Address.HexToByteArray(),
                    AmountInGwei = (ulong)w.Amount.Value
                });
            }

            return list;
        }
    }
}
