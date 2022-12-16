using System.Text;

namespace Nethereum.Merkle.ByteConvertors
{
    public class ChartByteArrayConvertor : IByteArrayConvertor<char>
    {
        public byte[] ConvertToByteArray(char data)
        {
            return Encoding.UTF8.GetBytes(new[] { data });
        }
    }

}
