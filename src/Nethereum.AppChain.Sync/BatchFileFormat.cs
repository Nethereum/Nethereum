using System;

namespace Nethereum.AppChain.Sync
{
    public static class BatchFileFormat
    {
        public static readonly byte[] MAGIC = new byte[] { 0x4E, 0x44, 0x43, 0x48 }; // "NDCH"

        public const ushort VERSION = 1;

        public const int HEADER_SIZE = 4 + 2 + 8 + 8 + 8 + 2; // magic + version + chainId + from + to + blockCount = 32 bytes

        public static readonly byte[] STATE_MAGIC = new byte[] { 0x4E, 0x44, 0x53, 0x54 }; // "NDST"

        public const ushort STATE_VERSION = 1;

        public const int STATE_HEADER_SIZE = 4 + 2 + 8 + 8 + 32 + 8 + 8; // magic + version + chainId + blockNumber + stateRoot + accountCount + codeCount = 70 bytes

        public const string BATCH_FILE_EXTENSION = ".bin";
        public const string BATCH_FILE_COMPRESSED_EXTENSION = ".bin.zst";
        public const string STATE_FILE_EXTENSION = ".state";
        public const string STATE_FILE_COMPRESSED_EXTENSION = ".state.zst";

        public static string GetBatchFileName(long chainId, long fromBlock, long toBlock, bool compressed)
        {
            var ext = compressed ? BATCH_FILE_COMPRESSED_EXTENSION : BATCH_FILE_EXTENSION;
            return $"batch_{chainId}_{fromBlock}_{toBlock}{ext}";
        }

        public static string GetStateSnapshotFileName(long chainId, long blockNumber, bool compressed)
        {
            var ext = compressed ? STATE_FILE_COMPRESSED_EXTENSION : STATE_FILE_EXTENSION;
            return $"state_{chainId}_{blockNumber}{ext}";
        }

        public static bool TryParseBatchFileName(string fileName, out long chainId, out long fromBlock, out long toBlock)
        {
            chainId = 0;
            fromBlock = 0;
            toBlock = 0;

            var name = System.IO.Path.GetFileName(fileName);
            if (name.EndsWith(BATCH_FILE_COMPRESSED_EXTENSION))
                name = name.Substring(0, name.Length - BATCH_FILE_COMPRESSED_EXTENSION.Length);
            else if (name.EndsWith(BATCH_FILE_EXTENSION))
                name = name.Substring(0, name.Length - BATCH_FILE_EXTENSION.Length);
            else
                return false;

            if (!name.StartsWith("batch_"))
                return false;

            var parts = name.Substring(6).Split('_');
            if (parts.Length != 3)
                return false;

            return long.TryParse(parts[0], out chainId) &&
                   long.TryParse(parts[1], out fromBlock) &&
                   long.TryParse(parts[2], out toBlock);
        }
    }
}
