using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.Bundler
{
    internal static class RevertReasonDecoder
    {
        internal static string Decode(byte[]? data)
        {
            if (data == null || data.Length == 0)
            {
                return "execution reverted";
            }

            var message = new FunctionCallDecoder().DecodeFunctionErrorMessage(data.ToHex(true));
            return message ?? data.ToHex(true);
        }
    }
}
