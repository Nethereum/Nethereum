using System;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    public static class ValidationValidityWindow
    {
        public const ulong VALIDITY_BLOCK_RANGE_FLAG = 0x800000000000;
        public const ulong VALIDITY_BLOCK_RANGE_MASK = 0x7FFFFFFFFFFF;

        public const ulong MAX_UINT48 = 0xFFFFFFFFFFFF;

        public static ulong NormalizeValidUntil(ulong validUntil)
        {
            return validUntil == 0 ? MAX_UINT48 : validUntil;
        }

        public static bool IsBlockRange(ulong validAfter, ulong validUntil)
        {
            return validAfter >= VALIDITY_BLOCK_RANGE_FLAG && NormalizeValidUntil(validUntil) >= VALIDITY_BLOCK_RANGE_FLAG;
        }

        public static bool IsValidAt(ulong validAfter, ulong validUntil, ulong timestamp, ulong blockNumber)
        {
            var until = NormalizeValidUntil(validUntil);

            if (validAfter >= VALIDITY_BLOCK_RANGE_FLAG && until >= VALIDITY_BLOCK_RANGE_FLAG)
            {
                return blockNumber > GetBlockNumber(validAfter) && blockNumber <= GetBlockNumber(until);
            }

            return timestamp > validAfter && timestamp <= until;
        }

        public static bool IsBlockWindowOpen(ulong? validAfterBlock, ulong? validUntilBlock, ulong blockNumber)
        {
            if (!validAfterBlock.HasValue && !validUntilBlock.HasValue)
                return true;

            return blockNumber > (validAfterBlock ?? 0) && blockNumber <= (validUntilBlock ?? GetBlockNumber(MAX_UINT48));
        }

        public static bool IsBlockWindowEnded(ulong? validUntilBlock, ulong blockNumber)
        {
            return validUntilBlock.HasValue && blockNumber > validUntilBlock.Value;
        }

        public static bool IsBlockRangeValidity(ulong validAfter, ulong validUntil)
        {
            return validAfter >= VALIDITY_BLOCK_RANGE_FLAG && validUntil >= VALIDITY_BLOCK_RANGE_FLAG;
        }

        public static ulong GetBlockNumber(ulong value)
        {
            return value & VALIDITY_BLOCK_RANGE_MASK;
        }

        public static bool IsValidNow(ulong? validAfter, ulong? validUntil, ulong currentTimestamp, ulong currentBlockNumber)
        {
            if (!validAfter.HasValue && !validUntil.HasValue)
                return true;

            var after = validAfter ?? 0;
            var until = validUntil ?? ulong.MaxValue;

            if (IsBlockRangeValidity(after, until))
            {
                var afterBlock = GetBlockNumber(after);
                var untilBlock = GetBlockNumber(until);
                return currentBlockNumber > afterBlock && currentBlockNumber <= untilBlock;
            }
            else
            {
                return currentTimestamp > after && (until == 0 || currentTimestamp <= until);
            }
        }

        public static bool IsValidAtTime(ulong? validAfter, ulong? validUntil, ulong timestamp)
        {
            if (!validAfter.HasValue && !validUntil.HasValue)
                return true;

            var after = validAfter ?? 0;
            var until = validUntil ?? ulong.MaxValue;

            if (IsBlockRangeValidity(after, until))
            {
                return true;
            }

            return timestamp > after && (until == 0 || timestamp <= until);
        }

        public static bool IsValidAtBlock(ulong? validAfter, ulong? validUntil, ulong blockNumber)
        {
            if (!validAfter.HasValue && !validUntil.HasValue)
                return true;

            var after = validAfter ?? 0;
            var until = validUntil ?? ulong.MaxValue;

            if (!IsBlockRangeValidity(after, until))
            {
                return true;
            }

            var afterBlock = GetBlockNumber(after);
            var untilBlock = GetBlockNumber(until);
            return blockNumber > afterBlock && blockNumber <= untilBlock;
        }
    }
}
