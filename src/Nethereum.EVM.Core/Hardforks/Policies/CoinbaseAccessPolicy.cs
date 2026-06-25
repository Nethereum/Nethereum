namespace Nethereum.EVM.Hardforks.Policies
{
    /// <summary>
    /// EIP-3651 coinbase access policy. Decides whether the block
    /// coinbase address is pre-warmed in the EIP-2929 access list
    /// before transaction execution begins.
    ///
    /// <para><b>Fork history:</b></para>
    /// <list type="bullet">
    ///   <item>Frontier through Paris — <see cref="Cold"/>. The coinbase
    ///   is not pre-warmed; first state-touching opcode on the coinbase
    ///   (BALANCE / EXTCODE* / CALL / SELFDESTRUCT) at Berlin+ pays the
    ///   cold-access surcharge (2600 gas with EIP-2929 active, 700 gas
    ///   pre-Berlin).</item>
    ///   <item>Shanghai onwards — <see cref="Eip3651Warm"/>. EIP-3651
    ///   pre-warms the coinbase before tx execution, so the first access
    ///   costs 100 (warm) instead of 2600 (cold).</item>
    /// </list>
    ///
    /// <para><b>Why this matters:</b> MEV-Boost relays write to the
    /// coinbase as part of bundle settlement, and the 2500-gas
    /// cold-access surcharge made certain MEV bundles uneconomical.
    /// EIP-3651 removed that wart.</para>
    /// </summary>
    public abstract class CoinbaseAccessPolicy
    {
        /// <summary>
        /// Pre-EIP-3651: coinbase is cold on first access. First touch
        /// pays the standard access cost (700 pre-Berlin, 2600 at
        /// Berlin+ via EIP-2929).
        /// </summary>
        public static readonly CoinbaseAccessPolicy Cold = new ColdPolicy();

        /// <summary>
        /// EIP-3651 (Shanghai+): coinbase pre-warmed before tx
        /// execution. First touch pays the warm cost (100 gas).
        /// </summary>
        public static readonly CoinbaseAccessPolicy Eip3651Warm = new Eip3651WarmPolicy();

        /// <summary>True if the executor should pre-warm the coinbase.</summary>
        public abstract bool ShouldPreWarmCoinbase { get; }

        private sealed class ColdPolicy : CoinbaseAccessPolicy
        {
            public override bool ShouldPreWarmCoinbase => false;
        }

        private sealed class Eip3651WarmPolicy : CoinbaseAccessPolicy
        {
            public override bool ShouldPreWarmCoinbase => true;
        }
    }
}
