using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MempoolReplacementRules
    {
        public static bool IsValidFeeBump(PackedUserOperation existing, PackedUserOperation replacement)
        {
            var (existingPriorityFee, existingMaxFee) = existing.UnpackGasFees();
            var (replacementPriorityFee, replacementMaxFee) = replacement.UnpackGasFees();

            return replacementPriorityFee >= existingPriorityFee * 11 / 10 &&
                   replacementMaxFee >= existingMaxFee * 11 / 10;
        }
    }
}
