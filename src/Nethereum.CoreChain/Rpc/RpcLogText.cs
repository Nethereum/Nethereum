using System.Text;

namespace Nethereum.CoreChain.Rpc
{
    internal static class RpcLogText
    {
        public const int MaxLength = 64;
        public const int MaxBodyLength = 2048;

        public static string Body(string? body)
        {
            if (body == null) return "null";
            return body.Length > MaxBodyLength ? body.Substring(0, MaxBodyLength) + "..." : body;
        }

        public static string Method(string? method)
        {
            if (string.IsNullOrEmpty(method)) return string.Empty;

            var length = method.Length > MaxLength ? MaxLength : method.Length;
            var text = new StringBuilder(length + 3);
            for (var i = 0; i < length; i++)
            {
                text.Append(char.IsControl(method[i]) ? '?' : method[i]);
            }

            if (method.Length > MaxLength) text.Append("...");
            return text.ToString();
        }
    }
}
