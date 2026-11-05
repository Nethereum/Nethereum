using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;
using EngineWithdrawal = Nethereum.RPC.Eth.DTOs.Withdrawal;
using ModelWithdrawal = Nethereum.Model.Withdrawal;

namespace Nethereum.CoreChain.Engine
{
    public sealed class DecodedEnginePayload
    {
        public BlockHeader Header { get; init; }
        public List<ISignedTransaction> Transactions { get; init; }
        public List<ModelWithdrawal> Withdrawals { get; init; }
        public byte[] DeclaredBlockHash { get; init; }
        public Nethereum.EVM.HardforkName Fork { get; init; }
    }

    public sealed class InvalidEnginePayloadException : Exception
    {
        public InvalidEnginePayloadException(string message) : base(message) { }
    }

    public static class EnginePayloadDecoder
    {
        private const byte BlobTransactionType = 0x03;

        private static readonly byte[] EmptyListHash = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());

        public static DecodedEnginePayload Decode(
            ExecutionPayloadV3 payload,
            string parentBeaconBlockRoot,
            byte[] requestsHash,
            byte[] declaredBlockAccessListRlp,
            ulong? slotNumber,
            ChainConfig config)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var parentHash = payload.ParentHash.HexToByteArray();
            var transactions = DecodeTransactions(payload.Transactions);
            if (payload.BlockNumber.Value < 0 || payload.BlockNumber.Value > long.MaxValue)
                throw new InvalidEnginePayloadException($"blockNumber {payload.BlockNumber.Value} is outside the supported range");
            if (payload.Timestamp.Value < 0 || payload.Timestamp.Value > ulong.MaxValue)
                throw new InvalidEnginePayloadException($"timestamp {payload.Timestamp.Value} is outside the uint64 range");
            var fork = config.ResolveActivations().ResolveAt(
                (long)payload.BlockNumber.Value, (ulong)payload.Timestamp.Value);
            var withdrawals = fork >= Nethereum.EVM.HardforkName.Shanghai && payload.Withdrawals != null
                ? ToModelWithdrawals(payload.Withdrawals)
                : null;

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
                WithdrawalsRoot = withdrawals != null
                    ? PatriciaBlockRootsProvider.Instance.CalculateWithdrawalsRoot(withdrawals)
                    : null,
                BlobGasUsed = payload.BlobGasUsed != null ? ToHeaderScalarLong(payload.BlobGasUsed.Value) : (long?)null,
                ExcessBlobGas = payload.ExcessBlobGas != null ? ToHeaderScalarLong(payload.ExcessBlobGas.Value) : (long?)null,
                ParentBeaconBlockRoot = parentBeaconBlockRoot != null
                    ? parentBeaconBlockRoot.HexToByteArray()
                    : null,
                RequestsHash = requestsHash,
                BlockAccessListHash = declaredBlockAccessListRlp != null
                    ? new Sha3Keccack().CalculateHash(declaredBlockAccessListRlp)
                    : null,
                SlotNumber = slotNumber
            };

            return new DecodedEnginePayload
            {
                Header = header,
                Transactions = transactions,
                Withdrawals = withdrawals,
                DeclaredBlockHash = payload.BlockHash.HexToByteArray(),
                Fork = fork
            };
        }

        public static List<AccountChanges> DecodeDeclaredBlockAccessList(byte[] rlp)
        {
            if (rlp == null) return null;
            try
            {
                return BlockAccessListRLPEncoder.Current.Decode(rlp);
            }
            catch (Exception ex) when (ex is not RpcException)
            {
                throw RpcException.InvalidParams("invalid blockAccessList encoding: " + ex.Message);
            }
        }

        public static long ToHeaderScalarLong(BigInteger value)
        {
            if (value < 0 || value > ulong.MaxValue)
            {
                throw RpcException.InvalidParams($"value {value} is outside the uint64 range");
            }

            return unchecked((long)(ulong)value);
        }

        public static byte[] RequestsHashFor(string[] executionRequests)
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

        public static List<ISignedTransaction> DecodeTransactions(List<string> raw)
        {
            var list = new List<ISignedTransaction>(raw?.Count ?? 0);
            if (raw == null) return list;

            for (int i = 0; i < raw.Count; i++)
            {
                list.Add(DecodeTransaction(raw[i], i));
            }

            return list;
        }

        public static PayloadStatusV1 InvalidStatus(string validationError) =>
            new PayloadStatusV1 { Status = EnginePayloadStatus.Invalid, ValidationError = validationError };

        public static PayloadStatusV1 CheckBlobVersionedHashes(ExecutionPayloadV3 payload, string[] expectedBlobVersionedHashes)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (expectedBlobVersionedHashes == null)
                throw RpcException.InvalidParams("nil versionedHashes post-cancun");

            var actual = new List<byte[]>();
            if (payload.Transactions != null)
            {
                for (int i = 0; i < payload.Transactions.Count; i++)
                {
                    var bytes = payload.Transactions[i]?.HexToByteArray();
                    if (bytes == null || bytes.Length == 0 || bytes[0] != BlobTransactionType) continue;

                    ISignedTransaction transaction;
                    try
                    {
                        transaction = DecodeTransaction(payload.Transactions[i], i);
                    }
                    catch (InvalidEnginePayloadException ex)
                    {
                        return InvalidStatus(ex.Message);
                    }

                    if (transaction is Transaction4844 blob) actual.AddRange(blob.BlobVersionedHashes);
                }
            }

            if (actual.Count != expectedBlobVersionedHashes.Length)
                return InvalidStatus($"invalid number of versionedHashes: {expectedBlobVersionedHashes.Length} blobHashes: {actual.Count}");

            for (int i = 0; i < actual.Count; i++)
            {
                byte[] expected;
                try
                {
                    expected = expectedBlobVersionedHashes[i].HexToByteArray();
                }
                catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
                {
                    throw RpcException.InvalidParams($"invalid versionedHash at {i}");
                }

                if (!ByteUtil.AreEqual(actual[i], expected))
                    return InvalidStatus($"invalid versionedHash at {i}: {expectedBlobVersionedHashes[i]} blobHash: {actual[i].ToHex(true)}");
            }

            return null;
        }

        private static ISignedTransaction DecodeTransaction(string hex, int index)
        {
            try
            {
                return TransactionFactory.CreateTransaction(hex.HexToByteArray(), allowBlobNetworkWrapper: false);
            }
            catch (Exception ex) when (ex is not RpcException)
            {
                throw new InvalidEnginePayloadException($"transaction {index} does not decode: {ex.Message}");
            }
        }

        public static List<ModelWithdrawal> ToModelWithdrawals(List<EngineWithdrawal> withdrawals)
        {
            var list = new List<ModelWithdrawal>(withdrawals?.Count ?? 0);
            if (withdrawals == null) return list;

            foreach (var w in withdrawals)
            {
                list.Add(new ModelWithdrawal
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
