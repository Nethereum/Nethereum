using System.Numerics;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MempoolReplacementRules
    {
        private const int MinimumBumpPercent = 10;

        public static bool IsValidFeeBump(PackedUserOperation existing, PackedUserOperation replacement)
        {
            var (existingPriorityFee, existingMaxFee) = existing.UnpackGasFees();
            var (replacementPriorityFee, replacementMaxFee) = replacement.UnpackGasFees();

            return IsBumped(existingPriorityFee, replacementPriorityFee) &&
                   IsBumped(existingMaxFee, replacementMaxFee);
        }

        private static bool IsBumped(BigInteger existing, BigInteger replacement)
        {
            var required = (existing * (100 + MinimumBumpPercent) + 99) / 100;
            return replacement > existing && replacement >= required;
        }
    }
}
