using Nethereum.ABI;

namespace Nethereum.Merkle.ByteConvertors
{
    public class AbiStructEncoderPackedByteConvertor<T> : IByteArrayConvertor<T>
    {
        private readonly ABIEncode _abiEncode = new ABIEncode();
        public AbiStructEncoderPackedByteConvertor()
        {

        }

        public byte[] ConvertToByteArray(T data)
        {
            return _abiEncode.GetABIParamsEncodedPacked(data);
        }
    }

}
