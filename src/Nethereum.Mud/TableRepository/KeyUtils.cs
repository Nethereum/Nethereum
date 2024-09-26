using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Collections.Generic;
using System.Linq;


namespace Nethereum.Mud.TableRepository
{
    public class KeyUtils
    {
        public static string ConvertKeyToCombinedHex(List<byte[]> key)
        {
            return string.Join("", key.Select(k => k.PadTo32Bytes().ToHex()));
        }

        public static List<byte[]> ConvertKeyFromCombinedHex(string key)
        {
            var result = new List<byte[]>();
            key = key.RemoveHexPrefix();

            for (int i = 0; i < key.Length; i += 64)
            {
                string part = key.Substring(i, 64);
                result.Add(part.HexToByteArray()); // Convert hex back to byte[]
            }

            return result;
        }
    }

}
