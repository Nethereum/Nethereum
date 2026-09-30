namespace Nethereum.EVM.Hardforks.Policies
{
    public abstract class CoinbaseAccessPolicy
    {
        public static readonly CoinbaseAccessPolicy Cold = new ColdPolicy();

        public static readonly CoinbaseAccessPolicy Eip3651Warm = new Eip3651WarmPolicy();

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
