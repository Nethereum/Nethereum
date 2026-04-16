namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// EIP-2929 (Berlin) cold/warm address access rule. Returns
    /// <see cref="GasConstants.COLD_ACCOUNT_ACCESS_COST"/> (2600) on the
    /// first touch of an address in a transaction and marks it warm,
    /// then <see cref="GasConstants.WARM_STORAGE_READ_COST"/> (100) for
    /// every subsequent touch.
    ///
    /// <para>
    /// Not fork-variant between Berlin and Osaka, so every fork bundle
    /// shares this implementation via the <see cref="Instance"/>
    /// singleton.
    /// </para>
    /// </summary>
    public sealed class Eip2929AccessAccountRule : IAccessAccountRule
    {
        public static readonly Eip2929AccessAccountRule Instance = new Eip2929AccessAccountRule();

        public long GetAccessCost(Program program, byte[] addressBytes)
        {
            if (program.IsAddressWarm(addressBytes))
                return GasConstants.WARM_STORAGE_READ_COST;

            program.MarkAddressAsWarm(addressBytes);
            return GasConstants.COLD_ACCOUNT_ACCESS_COST;
        }
    }
}
