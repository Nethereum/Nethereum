using System.Numerics;
using Nethereum.Util;

namespace Nethereum.RPC.Eth.DTOs
{
    public static class TransactionReceiptExtensions
    {
        private const bool TREAT_NULL_STATUS_AS_FAILURE = false;

        public static bool IsContractAddressEmptyOrEqual(this TransactionReceipt receipt, string contractAddress)
        {
            return receipt.ContractAddress.IsEmptyOrEqualsAddress(contractAddress);
        }

        public static bool IsContractAddressEqual(this TransactionReceipt receipt, string address)
        {
            return receipt.ContractAddress.IsTheSameAddress(address);
        }

        public static bool Succeeded(this TransactionReceipt receipt, bool treatNullStatusAsFailure = TREAT_NULL_STATUS_AS_FAILURE)
        {
            return !receipt.Failed(treatNullStatusAsFailure);
        }

        public static bool Failed(this TransactionReceipt receipt, bool treatNullStatusAsFailure = TREAT_NULL_STATUS_AS_FAILURE)
        {
            return receipt.HasErrors() ?? treatNullStatusAsFailure;
        }

        public static bool HasLogs(this TransactionReceipt receipt)
        {
            return receipt.Logs?.Count > 0;
        }
    }

}
