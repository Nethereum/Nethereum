using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EVM.Execution
{
    public static class Eip7702DelegationUtils
    {
        private static readonly byte[] DELEGATION_PREFIX = new byte[] { 0xef, 0x01, 0x00 };

        public static bool IsDelegatedCode(byte[] code)
        {
            if (code == null || code.Length != 23)
                return false;
            return code[0] == DELEGATION_PREFIX[0] &&
                   code[1] == DELEGATION_PREFIX[1] &&
                   code[2] == DELEGATION_PREFIX[2];
        }

        public static string GetDelegateAddress(byte[] delegationCode)
        {
            if (delegationCode == null || delegationCode.Length != 23)
                return null;
            var addressBytes = new byte[20];
            System.Array.Copy(delegationCode, 3, addressBytes, 0, 20);
            return addressBytes.ToHex(true);
        }

        public static byte[] CreateDelegationCode(string address)
        {
            var addressBytes = address.HexToByteArray();
            var code = new byte[23];
            code[0] = DELEGATION_PREFIX[0];
            code[1] = DELEGATION_PREFIX[1];
            code[2] = DELEGATION_PREFIX[2];
            System.Array.Copy(addressBytes, 0, code, 3, 20);
            return code;
        }
    }
}
