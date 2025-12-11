using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Beaconchain.LightClient
{
    public static class LightClientResponseMapper
    {
        public static LightClientBootstrap ToDomain(LightClientBootstrapResponse response)
        {
            if (response?.Data == null) return null;

            return new LightClientBootstrap
            {
                Header = MapHeader(response.Data.Header),
                CurrentSyncCommittee = MapSyncCommittee(response.Data.CurrentSyncCommittee),
                CurrentSyncCommitteeBranch = response.Data.CurrentSyncCommitteeBranch?
                    .Select(h => h.HexToByteArray()).ToList() ?? new List<byte[]>()
            };
        }

        public static LightClientUpdate ToDomain(LightClientUpdateResponse response)
        {
            if (response?.Data == null) return null;

            return new LightClientUpdate
            {
                AttestedHeader = MapHeader(response.Data.AttestedHeader),
                NextSyncCommittee = MapSyncCommittee(response.Data.NextSyncCommittee),
                NextSyncCommitteeBranch = response.Data.NextSyncCommitteeBranch?
                    .Select(h => h.HexToByteArray()).ToList() ?? new List<byte[]>(),
                FinalizedHeader = MapHeader(response.Data.FinalizedHeader),
                FinalityBranch = response.Data.FinalityBranch?
                    .Select(h => h.HexToByteArray()).ToList() ?? new List<byte[]>(),
                SyncAggregate = MapSyncAggregate(response.Data.SyncAggregate),
                SignatureSlot = ParseUlong(response.Data.SignatureSlot)
            };
        }

        public static LightClientFinalityUpdate ToDomain(LightClientFinalityUpdateResponse response)
        {
            if (response?.Data == null) return null;

            return new LightClientFinalityUpdate
            {
                AttestedHeader = MapHeader(response.Data.AttestedHeader),
                FinalizedHeader = MapHeader(response.Data.FinalizedHeader),
                FinalityBranch = response.Data.FinalityBranch?
                    .Select(h => h.HexToByteArray()).ToList() ?? new List<byte[]>(),
                SyncAggregate = MapSyncAggregate(response.Data.SyncAggregate),
                SignatureSlot = ParseUlong(response.Data.SignatureSlot)
            };
        }

        public static LightClientOptimisticUpdate ToDomain(LightClientOptimisticUpdateResponse response)
        {
            if (response?.Data == null) return null;

            return new LightClientOptimisticUpdate
            {
                AttestedHeader = MapHeader(response.Data.AttestedHeader),
                SyncAggregate = MapSyncAggregate(response.Data.SyncAggregate),
                SignatureSlot = ParseUlong(response.Data.SignatureSlot)
            };
        }

        public static IReadOnlyList<LightClientUpdate> ToDomain(IEnumerable<LightClientUpdateResponse> responses)
        {
            return responses?.Select(ToDomain).Where(u => u != null).ToList()
                   ?? new List<LightClientUpdate>();
        }

        private static LightClientHeader MapHeader(LightClientHeaderDto dto)
        {
            if (dto == null) return null;

            return new LightClientHeader
            {
                Beacon = MapBeaconBlockHeader(dto.Beacon),
                Execution = MapExecutionPayloadHeader(dto.Execution),
                ExecutionBranch = dto.ExecutionBranch?
                    .Select(h => h.HexToByteArray()).ToList() ?? new List<byte[]>()
            };
        }

        private static BeaconBlockHeader MapBeaconBlockHeader(BeaconBlockHeaderDto dto)
        {
            if (dto == null) return null;

            return new BeaconBlockHeader
            {
                Slot = ParseUlong(dto.Slot),
                ProposerIndex = ParseUlong(dto.ProposerIndex),
                ParentRoot = dto.ParentRoot?.HexToByteArray() ?? new byte[32],
                StateRoot = dto.StateRoot?.HexToByteArray() ?? new byte[32],
                BodyRoot = dto.BodyRoot?.HexToByteArray() ?? new byte[32]
            };
        }

        private static ExecutionPayloadHeader MapExecutionPayloadHeader(ExecutionPayloadHeaderDto dto)
        {
            if (dto == null) return null;

            return new ExecutionPayloadHeader
            {
                ParentHash = dto.ParentHash?.HexToByteArray() ?? new byte[32],
                FeeRecipient = dto.FeeRecipient?.HexToByteArray() ?? new byte[20],
                StateRoot = dto.StateRoot?.HexToByteArray() ?? new byte[32],
                ReceiptsRoot = dto.ReceiptsRoot?.HexToByteArray() ?? new byte[32],
                LogsBloom = dto.LogsBloom?.HexToByteArray() ?? new byte[256],
                PrevRandao = dto.PrevRandao?.HexToByteArray() ?? new byte[32],
                BlockNumber = ParseUlong(dto.BlockNumber),
                GasLimit = ParseUlong(dto.GasLimit),
                GasUsed = ParseUlong(dto.GasUsed),
                Timestamp = ParseUlong(dto.Timestamp),
                ExtraData = dto.ExtraData?.HexToByteArray() ?? Array.Empty<byte>(),
                BaseFeePerGas = ParseBaseFeePerGas(dto.BaseFeePerGas),
                BlockHash = dto.BlockHash?.HexToByteArray() ?? new byte[32],
                TransactionsRoot = dto.TransactionsRoot?.HexToByteArray() ?? new byte[32],
                WithdrawalsRoot = dto.WithdrawalsRoot?.HexToByteArray() ?? new byte[32],
                BlobGasUsed = ParseUlong(dto.BlobGasUsed),
                ExcessBlobGas = ParseUlong(dto.ExcessBlobGas)
            };
        }

        private static SyncCommittee MapSyncCommittee(SyncCommitteeDto dto)
        {
            if (dto == null) return null;

            return new SyncCommittee
            {
                PubKeys = dto.PubKeys?.Select(pk => pk.HexToByteArray()).ToList() ?? new List<byte[]>(),
                AggregatePubKey = dto.AggregatePubKey?.HexToByteArray() ?? new byte[48]
            };
        }

        private static SyncAggregate MapSyncAggregate(SyncAggregateDto dto)
        {
            if (dto == null) return null;

            return new SyncAggregate
            {
                SyncCommitteeBits = dto.SyncCommitteeBits?.HexToByteArray() ?? new byte[64],
                SyncCommitteeSignature = dto.SyncCommitteeSignature?.HexToByteArray() ?? new byte[96]
            };
        }

        private static ulong ParseUlong(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return ulong.TryParse(value, out var result) ? result : 0;
        }

        private static byte[] ParseBaseFeePerGas(string value)
        {
            if (string.IsNullOrEmpty(value)) return new byte[32];

            try
            {
                var bigInt = BigInteger.Parse(value);
                var bytes = bigInt.ToByteArray();

                if (bytes.Length > 32)
                    throw new ArgumentException("BaseFeePerGas exceeds 32 bytes");

                var result = new byte[32];
                Array.Copy(bytes, 0, result, 0, Math.Min(bytes.Length, 32));

                if (bytes.Length > 0 && bytes[bytes.Length - 1] == 0 && (bytes[bytes.Length - 1] & 0x80) != 0)
                {
                    result = new byte[32];
                    Array.Copy(bytes, 0, result, 0, bytes.Length - 1);
                }

                return result;
            }
            catch
            {
                return new byte[32];
            }
        }
    }
}
