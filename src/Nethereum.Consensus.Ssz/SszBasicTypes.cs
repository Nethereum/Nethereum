using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nethereum.Ssz;
using Nethereum.Util;

namespace Nethereum.Consensus.Ssz
{
    public static class SszBasicTypes
    {
        public const int RootLength = 32;
        public const int PubKeyLength = 48;
        public const int SignatureLength = 96;
        public const int SyncCommitteeSize = 512;
        public const int BeaconBlockHeaderLength = (sizeof(ulong) * 2) + (RootLength * 3); // 112
        public const int SyncAggregateLength = (SyncCommitteeSize / 8) + SignatureLength; // 64 + 96 = 160
        public const int SyncCommitteeLength = (SyncCommitteeSize * PubKeyLength) + PubKeyLength; // 512 pubkeys + aggregate
        public const int LogsBloomLength = 256;
        public const int CurrentSyncCommitteeBranchLength = 5;
        public const int FinalityBranchLength = 7;
        public const int ExecutionBranchLength = 4;

        // Generalized indices
        // Deneb: ExecutionPayloadGIndex = 25, FinalizedRootGIndex = 105
        // Electra: FinalizedRootGIndex = 169 (BeaconState gained new fields)
        public const int ExecutionPayloadGIndex = 25;
        public const int FinalizedRootGIndexDeneb = 105;
        public const int FinalizedRootGIndexElectra = 169;

        // Computed from generalized indices for proof verification
        // Execution branch: depth = floorlog2(25) = 4, index = 25 - 16 = 9
        public const int ExecutionBranchDepth = 4;
        public const int ExecutionBranchIndex = 9;

        // Finality branch (Electra - current mainnet):
        // depth = floorlog2(169) = 7, index = 169 - 128 = 41
        public const int FinalityBranchDepth = 7;
        public const int FinalityBranchIndex = 41;

        public static int BranchByteLength(int depth) => depth * RootLength;

        public static byte[] EncodeRoot(byte[] value, string paramName)
        {
            ValidateFixedLength(value, RootLength, paramName);
            return value;
        }

        public static void ValidateFixedLength(byte[] value, int expectedLength, string paramName)
        {
            if (value == null) throw new ArgumentNullException(paramName);
            if (value.Length != expectedLength)
            {
                throw new ArgumentException($"Expected {expectedLength} bytes but received {value.Length}.", paramName);
            }
        }

        public static byte[] HashTreeRootUInt64(ulong value)
        {
            var chunk = ByteUtil.InitialiseEmptyByteArray(RootLength);
            BinaryPrimitives.WriteUInt64LittleEndian(chunk, value);
            return chunk;
        }

        public static byte[] HashTreeRootFixedBytes(byte[] value, int expectedLength)
        {
            ValidateFixedLength(value, expectedLength, nameof(value));
            if (expectedLength == RootLength)
            {
                var chunk = ByteUtil.InitialiseEmptyByteArray(RootLength);
                Array.Copy(value, chunk, RootLength);
                return chunk;
            }

            var chunks = SszMerkleizer.Chunkify(value);
            return SszMerkleizer.Merkleize(chunks);
        }

        public static byte[] HashTreeRootVariableBytes(byte[] value)
        {
            var data = value ?? Array.Empty<byte>();
            var chunks = SszMerkleizer.Chunkify(data);
            return SszMerkleizer.HashTreeRootList(chunks, (ulong)data.Length);
        }

        public static byte[] HashTreeRootByteVector(IEnumerable<byte[]> data, int elementLength)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var normalized = new List<byte[]>();
            foreach (var entry in data)
            {
                ValidateFixedLength(entry, elementLength, nameof(data));
                var chunk = new byte[elementLength];
                Array.Copy(entry, chunk, elementLength);
                normalized.Add(chunk);
            }

            var chunked = new List<byte[]>(normalized.Count);
            foreach (var entry in normalized)
            {
                if (elementLength == RootLength)
                {
                    chunked.Add(entry);
                }
                else
                {
                    chunked.AddRange(SszMerkleizer.Chunkify(entry));
                }
            }

            return SszMerkleizer.Merkleize(chunked);
        }

        public static byte[] HashTreeRootBranch(IEnumerable<byte[]> branchEnumerable)
        {
            if (branchEnumerable == null) throw new ArgumentNullException(nameof(branchEnumerable));
            var normalized = new List<byte[]>();
            foreach (var entry in branchEnumerable)
            {
                ValidateFixedLength(entry, RootLength, nameof(branchEnumerable));
                var chunk = new byte[RootLength];
                Array.Copy(entry, chunk, RootLength);
                normalized.Add(chunk);
            }

            return SszMerkleizer.Merkleize(normalized);
        }

        public static void WriteBytesVector(this SszWriter writer, IEnumerable<byte[]> values, int elementLength)
        {
            var list = values as IList<byte[]>;
            if (list != null)
            {
                writer.WriteVector(list, elementLength);
            }
            else
            {
                foreach (var entry in values)
                {
                    writer.WriteFixedBytes(entry, elementLength);
                }
            }
        }

        public static List<byte[]> ReadBytesVector(this ref SszReader reader, int elementCount, int elementLength)
        {
            var list = new List<byte[]>(elementCount);
            for (var i = 0; i < elementCount; i++)
            {
                list.Add(reader.ReadFixedBytes(elementLength));
            }

            return list;
        }

        public static void WriteFixedRootVector(this SszWriter writer, IList<byte[]> values, int expectedCount)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count != expectedCount)
            {
                throw new ArgumentException($"Expected {expectedCount} entries but received {values.Count}.");
            }

            foreach (var entry in values)
            {
                writer.WriteFixedBytes(entry, RootLength);
            }
        }

        public static List<byte[]> ReadFixedRootVector(this ref SszReader reader, int expectedCount)
        {
            var branch = new List<byte[]>(expectedCount);
            for (var i = 0; i < expectedCount; i++)
            {
                branch.Add(reader.ReadFixedBytes(RootLength));
            }

            return branch;
        }
    }
}
