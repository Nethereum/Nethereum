using System.Security.Cryptography;
using System.Text;

namespace Nethereum.CoreChain.Storage
{
    public static class FilterIdGenerator
    {
        public const int IdBytes = 16;

        public static string Next()
        {
            var bytes = new byte[IdBytes];
            RandomNumberGenerator.Fill(bytes);

            var id = new StringBuilder("0x", 2 + IdBytes * 2);
            var leading = true;
            foreach (var value in bytes)
            {
                var hex = value.ToString("x2");
                if (leading)
                {
                    hex = hex.TrimStart('0');
                    if (hex.Length == 0) continue;
                    leading = false;
                }

                id.Append(hex);
            }

            return leading ? "0x0" : id.ToString();
        }
    }
}
