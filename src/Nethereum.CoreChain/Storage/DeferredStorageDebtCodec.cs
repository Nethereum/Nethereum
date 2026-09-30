using System;
using System.Buffers.Binary;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public static class DeferredStorageDebtCodec
    {
        public const byte CurrentSchemaVersion = 1;
        private const int HeaderLength = 1 + 1 + 1 + 1 + 8;
        private const int EncodedLength = HeaderLength + (32 * 5);
        private const byte HasFetchPivotBlock = 1;
        private const byte HasFinalStateRoot = 2;
        private const byte HasFinalStorageRoot = 4;

        public static byte[] Encode(DeferredStorageDebt debt)
        {
            DeferredStorageDebt.Validate(debt);

            var header = new byte[HeaderLength];
            header[0] = CurrentSchemaVersion;
            header[1] = (byte)debt.Reason;
            header[2] = (byte)debt.Status;

            byte flags = 0;
            if (debt.FetchPivotBlock.HasValue) flags |= HasFetchPivotBlock;
            if (debt.FinalStateRoot != null) flags |= HasFinalStateRoot;
            if (debt.FinalStorageRoot != null) flags |= HasFinalStorageRoot;
            header[3] = flags;
            BinaryPrimitives.WriteUInt64BigEndian(
                header.AsSpan(4, 8),
                debt.FetchPivotBlock.GetValueOrDefault());

            return ByteUtil.Merge(
                header,
                debt.AccountHash,
                debt.DiscoveredStorageRoot,
                debt.FetchStateRoot,
                debt.FinalStateRoot ?? ByteUtil.InitialiseEmptyByteArray(32),
                debt.FinalStorageRoot ?? ByteUtil.InitialiseEmptyByteArray(32));
        }

        public static DeferredStorageDebt Decode(byte[] blob)
        {
            if (blob == null || blob.Length != EncodedLength)
                throw new ArgumentException($"Deferred storage debt blob must be {EncodedLength} bytes.", nameof(blob));
            if (blob[0] != CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported deferred storage debt schema version {blob[0]}.");

            var flags = blob[3];
            return new DeferredStorageDebt
            {
                Reason = (DeferredStorageReason)blob[1],
                Status = (StorageCompleteness)blob[2],
                FetchPivotBlock = (flags & HasFetchPivotBlock) != 0
                    ? BinaryPrimitives.ReadUInt64BigEndian(blob.AsSpan(4, 8))
                    : (ulong?)null,
                AccountHash = blob.Slice(HeaderLength, HeaderLength + 32),
                DiscoveredStorageRoot = blob.Slice(HeaderLength + 32, HeaderLength + 64),
                FetchStateRoot = blob.Slice(HeaderLength + 64, HeaderLength + 96),
                FinalStateRoot = (flags & HasFinalStateRoot) != 0
                    ? blob.Slice(HeaderLength + 96, HeaderLength + 128)
                    : null,
                FinalStorageRoot = (flags & HasFinalStorageRoot) != 0
                    ? blob.Slice(HeaderLength + 128, HeaderLength + 160)
                    : null,
            };
        }
    }
}
