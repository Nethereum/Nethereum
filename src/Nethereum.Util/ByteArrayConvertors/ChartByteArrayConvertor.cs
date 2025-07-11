using System.Text;

namespace Nethereum.Util.ByteArrayConvertors
{
    public class ChartByteArrayConvertor : IByteArrayConvertor<char>
    {
        public byte[] ConvertToByteArray(char data)
        {
            return Encoding.UTF8.GetBytes(new[] { data });
        }

        public char ConvertFromByteArray(byte[] data)
        {
            if (data == null || data.Length == 0)
                return '\0';
            var str = Encoding.UTF8.GetString(data);
            return str.Length > 0 ? str[0] : '\0';
        }
    }

}
