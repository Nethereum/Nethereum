using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    internal static class StateRootAccountRules
    {
        internal static bool IsEmptyAccount(AccountExecutionState account)
        {
            if (account.Nonce.HasValue && account.Nonce.Value > 0) return false;
            if (!account.Balance.GetTotalBalance().IsZero) return false;
            if (account.Code != null && account.Code.Length > 0) return false;
            if (account.Storage != null)
            {
                foreach (var kvp in account.Storage)
                {
                    if (kvp.Value != null && kvp.Value.Length > 0 && !ByteUtil.IsZero(kvp.Value))
                        return false;
                }
            }
            return true;
        }

        internal static byte[] TrimLeadingZeros(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return new byte[0];

            var firstNonZero = 0;
            while (firstNonZero < bytes.Length && bytes[firstNonZero] == 0)
                firstNonZero++;

            if (firstNonZero == bytes.Length)
                return new byte[0];

            if (firstNonZero == 0)
                return bytes;

            var result = new byte[bytes.Length - firstNonZero];
            System.Array.Copy(bytes, firstNonZero, result, 0, result.Length);
            return result;
        }
    }
}
