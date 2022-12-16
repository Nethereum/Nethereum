using System.Text;

namespace Nethereum.Merkle.ByteConvertors
{
    public class StringByteArrayConvertor : IByteArrayConvertor<string>
    {
        public byte[] ConvertToByteArray(string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }
    }

}
