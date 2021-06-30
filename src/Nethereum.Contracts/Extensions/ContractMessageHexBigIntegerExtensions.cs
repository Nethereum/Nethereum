using System.Numerics;
using Nethereum.Contracts.CQS;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;

namespace Nethereum.Contracts
{
    public static class ContractMessageHexBigIntegerExtensions
    {

        public static void SetTransactionType1559(this ContractMessageBase contractMessage)
        {
            contractMessage.TransactionType = 0x02;
        }

        public static HexBigInteger GetHexTransactionType(this ContractMessageBase contractMessage)
        {
            return GetDefaultValue(contractMessage.TransactionType);
        }

        public static HexBigInteger GetHexMaxFeePerGas(this ContractMessageBase contractMessage)
        {
            return GetDefaultValue(contractMessage.MaxFeePerGas);
        }

        public static HexBigInteger GetMaxPriorityFeePerGas(this ContractMessageBase contractMessage)
        {
            return GetDefaultValue(contractMessage.MaxPriorityFeePerGas);
        }

        public static HexBigInteger GetHexMaximumGas(this ContractMessageBase contractMessage)
        {
            return GetDefaultValue(contractMessage.Gas);
        }

        public static HexBigInteger GetHexValue(this ContractMessageBase contractMessage)
        {
            return GetDefaultValue(contractMessage.AmountToSend);
        }

        public static HexBigInteger GetHexGasPrice(this ContractMessageBase contractMessage)
        {
            return GetDefaultValue(contractMessage.GasPrice);
        }

        public static void SetGasPriceFromGwei(this ContractMessageBase contractMessage, decimal gweiAmount)
        {
            contractMessage.GasPrice = UnitConversion.Convert.ToWei(gweiAmount, UnitConversion.EthUnit.Gwei);
        }

        public static HexBigInteger GetHexNonce(this ContractMessageBase contractMessage)
        {
            return GetDefaultValue(contractMessage.Nonce);
        }

        public static HexBigInteger GetDefaultValue(BigInteger? bigInteger)
        {
            return bigInteger == null ? null : new HexBigInteger(bigInteger.Value);
        }

        public static string SetDefaultFromAddressIfNotSet(this ContractMessageBase contractMessage, string defaultFromAdddress)
        {
            if (string.IsNullOrEmpty(contractMessage.FromAddress))
            {
                contractMessage.FromAddress = defaultFromAdddress;
            }
            return contractMessage.FromAddress;
        }

    }
}