namespace Nethereum.Rsk.RPC.RskEth.DTOs
{
    public interface IRskBlockExtended
    {
        /// <summary>
        ///     QUANTITY - the minimum gas price in Wei
        /// </summary>
        string MinimumGasPriceString { get; set; }
    }
}