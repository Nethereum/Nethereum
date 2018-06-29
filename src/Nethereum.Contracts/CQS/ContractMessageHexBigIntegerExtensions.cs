using System.Numerics;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Contracts.CQS
{
    public static class ContractMessageHexBigIntegerExtensions
    {
        public static HexBigInteger GetHexMaximumGas(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.Gas);
        }

        public static HexBigInteger GetHexValue(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.AmountToSend);
        }

        public static HexBigInteger GetHexGasPrice(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.GasPrice);
        }

        public static HexBigInteger GetHexNonce(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.Nonce);
        }

        public static HexBigInteger GetDefaultValue(BigInteger? bigInteger)
        {
            return bigInteger == null ? null : new HexBigInteger(bigInteger.Value);
        }

        public static string SetDefaultFromAddressIfNotSet(this ContractMessage contractMessage, string defaultFromAdddress)
        {
            if (string.IsNullOrEmpty(contractMessage.FromAddress))
            {
                contractMessage.FromAddress = defaultFromAdddress;
            }
            return contractMessage.FromAddress;
        }
    }
}