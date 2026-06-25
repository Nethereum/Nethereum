namespace Nethereum.EVM.Hardforks.Policies
{
    /// <summary>
    /// EIP-1559 fee-distribution policy. Decides where the gas fee
    /// flows at the end of a transaction.
    ///
    /// <para><b>Fork history:</b></para>
    /// <list type="bullet">
    ///   <item>Frontier through Berlin — <see cref="MinerKeepsAll"/>.
    ///   The miner receives the full <c>gasUsed * effectiveGasPrice</c>
    ///   as block reward. There is no base fee concept; the gas price
    ///   is whatever the tx declares.</item>
    ///   <item>London onwards — <see cref="Eip1559Burnt"/>. The base fee
    ///   portion of <c>gasUsed * effectiveGasPrice</c> is burnt (removed
    ///   from supply); only the priority tip
    ///   <c>gasUsed * (effectiveGasPrice - baseFee)</c> goes to the
    ///   miner / validator.</item>
    /// </list>
    /// </summary>
    public abstract class BaseFeePolicy
    {
        /// <summary>Pre-EIP-1559: miner receives the full gas fee.</summary>
        public static readonly BaseFeePolicy MinerKeepsAll = new MinerKeepsAllPolicy();

        /// <summary>EIP-1559 (London+): base fee burnt, miner receives tip only.</summary>
        public static readonly BaseFeePolicy Eip1559Burnt = new Eip1559BurntPolicy();

        /// <summary>
        /// True when the executor should compute the miner reward as
        /// <c>gasUsed * (effectiveGasPrice - baseFee)</c> instead of
        /// <c>gasUsed * effectiveGasPrice</c>.
        /// </summary>
        public abstract bool BurnsBaseFee { get; }

        private sealed class MinerKeepsAllPolicy : BaseFeePolicy
        {
            public override bool BurnsBaseFee => false;
        }

        private sealed class Eip1559BurntPolicy : BaseFeePolicy
        {
            public override bool BurnsBaseFee => true;
        }
    }
}
