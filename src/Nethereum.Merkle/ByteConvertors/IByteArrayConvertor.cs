namespace Nethereum.Merkle.ByteConvertors
{
    public interface IByteArrayConvertor<T>
    {
        byte[] ConvertToByteArray(T data);
    }

}
