using Nethereum.ABI;

namespace Nethereum.Merkle.ByteConvertors
{
    public class AbiStructEncoderByteConvertor<T> : IByteArrayConvertor<T>
    {
        private readonly ABIEncode _abiEncode = new ABIEncode();
        public AbiStructEncoderByteConvertor()
        {

        }

        public byte[] ConvertToByteArray(T data)
        {
            return _abiEncode.GetABIParamsEncoded(data);
        }
    }

}
