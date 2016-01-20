namespace Ethereum.RPC.ABI
{
    public interface ITypeEncoder
    {
        byte[] Encode(object value);
    }
}