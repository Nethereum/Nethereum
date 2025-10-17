namespace Nethereum.Uniswap.V4.Positions
{
    public class PositionInfoPacked
    {
        public byte[] PoolId { get; set; }
        public int TickLower { get; set; }
        public int TickUpper { get; set; }
        public bool HasSubscriber { get; set; }
    }
}
