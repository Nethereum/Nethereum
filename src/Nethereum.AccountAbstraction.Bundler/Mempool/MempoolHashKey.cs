namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MempoolHashKey
    {
        public const int UserOpHashHexLength = 66;

        public static string Normalize(string? userOpHash)
        {
            if (string.IsNullOrEmpty(userOpHash)) return string.Empty;

            var lower = userOpHash.ToLowerInvariant();
            return lower.StartsWith("0x", StringComparison.Ordinal) ? lower : "0x" + lower;
        }

        public static bool TryNormalizeStrict(string? userOpHash, out string normalized)
        {
            normalized = string.Empty;
            if (userOpHash == null || userOpHash.Length != UserOpHashHexLength) return false;
            if (userOpHash[0] != '0' || (userOpHash[1] != 'x')) return false;

            for (var i = 2; i < userOpHash.Length; i++)
            {
                if (!Uri.IsHexDigit(userOpHash[i])) return false;
            }

            normalized = "0x" + userOpHash.Substring(2).ToLowerInvariant();
            return true;
        }
    }
}
