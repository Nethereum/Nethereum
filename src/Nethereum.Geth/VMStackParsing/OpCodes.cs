using System.Linq;

namespace Nethereum.Geth.VMStackParsing
{
    public static class OpCodes
    {
        public const string Call = "CALL";
        public const string DelegateCall = "DELEGATECALL";
        public const string Create = "CREATE";
        public const string Return = "RETURN";
        public const string SelfDestruct = "SELFDESTRUCT";

        public static readonly string[] InterContract = new []{
            Call, Create, DelegateCall
        };

        public static bool IsInterContract(string opCode)
        {
            return InterContract.Contains(opCode);
        }
    }
}