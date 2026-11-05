using System;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public static class EthashDifficulty
    {
        private const long DifficultyBoundDivisor = 2048;
        private const long MinimumDifficulty = 131072;
        private const long DurationLimit = 13;
        private const long ExponentialPeriod = 100000;
        private const long FloorOfAdjustmentFactor = -99;

        public static BigInteger Calculate(HardforkName fork, ulong timestamp, BlockHeader parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var parentHasUncles = parent.UnclesHash != null
                && !Util.ByteUtil.AreEqual(parent.UnclesHash, DefaultValues.EMPTY_UNCLES_HASH);
            return Calculate(
                fork, timestamp, (ulong)parent.BlockNumber, unchecked((ulong)parent.Timestamp),
                (BigInteger)parent.Difficulty, parentHasUncles);
        }

        public static BigInteger Calculate(
            HardforkName fork, ulong timestamp, ulong parentNumber, ulong parentTimestamp,
            BigInteger parentDifficulty, bool parentHasUncles)
        {
            if (fork >= HardforkName.Paris || fork == HardforkName.Unspecified)
                throw new ArgumentOutOfRangeException(
                    nameof(fork), fork, "Proof-of-work difficulty is defined only before the merge.");

            if (fork < HardforkName.Homestead)
                return Frontier(timestamp, parentNumber, parentTimestamp, parentDifficulty);

            if (fork < HardforkName.Byzantium)
                return Homestead(timestamp, parentNumber, parentTimestamp, parentDifficulty);

            return Byzantium(
                BombDelay(fork), timestamp, parentNumber, parentTimestamp, parentDifficulty, parentHasUncles);
        }

        private static long BombDelay(HardforkName fork)
        {
            if (fork >= HardforkName.GrayGlacier) return 11_400_000;
            if (fork >= HardforkName.ArrowGlacier) return 10_700_000;
            if (fork >= HardforkName.London) return 9_700_000;
            if (fork >= HardforkName.MuirGlacier) return 9_000_000;
            if (fork >= HardforkName.Constantinople) return 5_000_000;
            return 3_000_000;
        }

        private static BigInteger Frontier(
            ulong timestamp, ulong parentNumber, ulong parentTimestamp, BigInteger parentDifficulty)
        {
            var adjustment = parentDifficulty / DifficultyBoundDivisor;
            var difficulty = (BigInteger)timestamp - parentTimestamp < DurationLimit
                ? parentDifficulty + adjustment
                : parentDifficulty - adjustment;

            if (difficulty < MinimumDifficulty) difficulty = MinimumDifficulty;

            var periodCount = ((BigInteger)parentNumber + 1) / ExponentialPeriod;
            if (periodCount > 1)
            {
                difficulty += BigInteger.Pow(2, (int)(periodCount - 2));
                if (difficulty < MinimumDifficulty) difficulty = MinimumDifficulty;
            }
            return difficulty;
        }

        private static BigInteger Homestead(
            ulong timestamp, ulong parentNumber, ulong parentTimestamp, BigInteger parentDifficulty)
        {
            var factor = BigInteger.One - FloorDivide((BigInteger)timestamp - parentTimestamp, 10);
            return Adjusted(parentDifficulty, factor, ((BigInteger)parentNumber + 1) / ExponentialPeriod);
        }

        private static BigInteger Byzantium(
            long bombDelay, ulong timestamp, ulong parentNumber, ulong parentTimestamp,
            BigInteger parentDifficulty, bool parentHasUncles)
        {
            var factor = (parentHasUncles ? 2 : 1)
                - FloorDivide((BigInteger)timestamp - parentTimestamp, 9);

            var bombDelayFromParent = bombDelay - 1;
            BigInteger fakeBlockNumber = (BigInteger)parentNumber >= bombDelayFromParent
                ? (BigInteger)parentNumber - bombDelayFromParent
                : 0;
            return Adjusted(parentDifficulty, factor, fakeBlockNumber / ExponentialPeriod);
        }

        private static BigInteger Adjusted(BigInteger parentDifficulty, BigInteger factor, BigInteger periodCount)
        {
            if (factor < FloorOfAdjustmentFactor) factor = FloorOfAdjustmentFactor;

            var difficulty = parentDifficulty + parentDifficulty / DifficultyBoundDivisor * factor;
            if (difficulty < MinimumDifficulty) difficulty = MinimumDifficulty;

            if (periodCount > 1) difficulty += BigInteger.Pow(2, (int)(periodCount - 2));
            return difficulty;
        }

        private static BigInteger FloorDivide(BigInteger dividend, long divisor)
        {
            var quotient = BigInteger.DivRem(dividend, divisor, out var remainder);
            return remainder.Sign < 0 ? quotient - 1 : quotient;
        }
    }
}
