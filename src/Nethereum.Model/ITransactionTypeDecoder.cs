namespace Nethereum.Model
{
    public interface ITransactionTypeDecoder
    {
        SignedTypeTransaction DecodeAsGeneric(byte[] rlpData);
    }
}