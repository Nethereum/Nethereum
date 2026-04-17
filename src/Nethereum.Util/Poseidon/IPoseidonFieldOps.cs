namespace Nethereum.Util.Poseidon
{
    public interface IPoseidonFieldOps<T>
    {
        T Zero { get; }
        T AddMod(T a, T b);
        T MulMod(T a, T b);
        T ModPow(T baseVal, T exponent);
        T FromBytes(byte[] bigEndianData);
        byte[] ToBytes(T value);
    }
}
