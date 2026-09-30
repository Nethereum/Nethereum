namespace Nethereum.EVM.Hardforks.Policies
{
    public abstract class BaseFeePolicy
    {
        public static readonly BaseFeePolicy MinerKeepsAll = new MinerKeepsAllPolicy();

        public static readonly BaseFeePolicy Eip1559Burnt = new Eip1559BurntPolicy();

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
