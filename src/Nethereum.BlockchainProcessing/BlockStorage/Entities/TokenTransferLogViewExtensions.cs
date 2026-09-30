using Nethereum.Util;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public static class TokenTransferLogViewExtensions
    {
        public static bool IsNativeTransfer(this ITokenTransferLogView transferRow)
        {
            return transferRow != null
                && transferRow.ContractAddress.IsTheSameAddress(AddressUtil.SYSTEM_ADDRESS);
        }
    }
}
