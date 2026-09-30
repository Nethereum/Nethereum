using System.Collections.Generic;

namespace Nethereum.Freezer
{
    public interface IFrozenReadSource
    {
        long Items { get; }

        long CommittedHead(params string[] tableNames);

        byte[] ReadHeader(long blockNumber);

        byte[] ReadHash(long blockNumber);

        byte[] ReadBody(long blockNumber);

        byte[] ReadReceipts(long blockNumber);

        IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedRange(
            string tableName, long startBlock, int maxItems, int? maxDegreeOfParallelism = null);

        IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedRange(
            string tableName, long startBlock, int maxItems, long endExclusiveBlock, int? maxDegreeOfParallelism = null);

        IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedChunk(
            string tableName, long startBlock, int targetBytes, long endExclusiveBlock, int? maxDegreeOfParallelism = null);
    }
}
