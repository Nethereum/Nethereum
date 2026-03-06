using System;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    public static class ValidationDataHelper
    {
        public const ulong VALIDITY_BLOCK_RANGE_FLAG = 0x800000000000; // Bit 47 set
        public const ulong VALIDITY_BLOCK_RANGE_MASK = 0x7FFFFFFFFFFF; // Lower 47 bits

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
