namespace Nethereum.Merkle.StrategyOptions.PairingConcat
{
    public interface IPairConcatStrategy
    {
        byte[] Concat(byte[] left, byte[] right);
    }

}
