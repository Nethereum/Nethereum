using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;

namespace Nethereum.PrivacyPools
{
    public static class WithdrawalContextHelper
    {
        public static BigInteger ComputeContext(Withdrawal withdrawal, BigInteger scope)
        {
            var contextParams = new WithdrawalContextParams
            {
                Withdrawal = withdrawal,
                Scope = scope
            };
            var encoded = new ABIEncode().GetABIParamsEncoded(contextParams);
            var hash = Util.Sha3Keccack.Current.CalculateHash(encoded);
            return new BigInteger(hash, isUnsigned: true, isBigEndian: true)
                % PrivacyPoolConstants.SNARK_SCALAR_FIELD;
        }

        public static byte[] BuildRelayData(string recipient, string relayer, BigInteger relayFeeBps)
        {
            return new ABIEncode().GetABIEncoded(
                new ABIValue("address", recipient),
                new ABIValue("address", relayer),
                new ABIValue("uint256", relayFeeBps));
        }
    }

    [FunctionOutput]
    public class WithdrawalContextParams
    {
        [Parameter("tuple", "withdrawal", 1, "IPrivacyPool.Withdrawal")]
        public virtual Withdrawal Withdrawal { get; set; } = null!;

        [Parameter("uint256", "scope", 2)]
        public virtual BigInteger Scope { get; set; }
    }
}
