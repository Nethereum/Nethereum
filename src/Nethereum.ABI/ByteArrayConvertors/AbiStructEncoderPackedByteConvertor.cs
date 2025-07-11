using Nethereum.Util.ByteArrayConvertors;

namespace Nethereum.ABI.ByteArrayConvertors
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

        public T ConvertFromByteArray(byte[] data)
        {
            // Note: Packed encoding is not reversible due to lack of padding and type information
            // This implementation assumes the data was encoded using standard ABI encoding
            return _abiEncode.DecodeEncodedComplexType<T>(data);
        }
    }

}
