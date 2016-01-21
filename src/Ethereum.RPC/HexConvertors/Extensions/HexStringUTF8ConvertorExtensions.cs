using System;
using System.Text;
using Ethereum.RPC.Util;

namespace Ethereum.RPC
{
    public static class HexStringUTF8ConvertorExtensions
    {
        public static string ToHexUTF8(this string value)
        {
            return "0x" + Encoding.UTF8.GetBytes(value).ToHex();
        }


        public static String HexToUTF8String(this string hex)
        {
            var bytes = hex.HexToByteArray();
            return System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }
   
    }
}