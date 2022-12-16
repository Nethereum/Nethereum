using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Merkle.ByteConvertors
{
    public class HexToByteArrayConvertor : IByteArrayConvertor<string>
    {
        public byte[] ConvertToByteArray(string data)
        {
            return data.HexToByteArray();
        }
    }

}
