using System.Numerics;

namespace Nethereum.EVM.StateChanges
{
    public enum BalanceChangeType
    {
        Native,
        ERC20,
        ERC721,
        ERC1155
    }

    public enum BalanceValidationStatus
    {
        NotValidated,
        Verified,
        FeeOnTransfer,
        Rebasing,
        OwnerMismatch,
        Mismatch
    }

    public class BalanceChange
    {
        public string Address { get; set; }
        public string AddressLabel { get; set; }
        public bool IsCurrentUser { get; set; }

        public BalanceChangeType Type { get; set; }
        public string TokenAddress { get; set; }
        public string TokenSymbol { get; set; }
        public int TokenDecimals { get; set; }
        public BigInteger? TokenId { get; set; }

        public BigInteger Change { get; set; }
        public BigInteger? BalanceBefore { get; set; }
        public BigInteger? BalanceAfter { get; set; }

        public BigInteger? ActualChange { get; set; }
        public string ActualOwner { get; set; }
        public BalanceValidationStatus ValidationStatus { get; set; } = BalanceValidationStatus.NotValidated;

        public bool HasDiscrepancy => ValidationStatus != BalanceValidationStatus.NotValidated
                                   && ValidationStatus != BalanceValidationStatus.Verified;

        public string GetTokenIdentifier()
        {
            if (Type == BalanceChangeType.Native) return "ETH";
            if (Type == BalanceChangeType.ERC721 || Type == BalanceChangeType.ERC1155)
            {
                return TokenId.HasValue
                    ? $"{TokenAddress?.ToLowerInvariant()}:{TokenId}"
                    : TokenAddress?.ToLowerInvariant();
            }
            return TokenAddress?.ToLowerInvariant();
        }

        public string GetDisplaySymbol()
        {
            if (Type == BalanceChangeType.Native)
            {
                return "ETH";
            }
            if (Type == BalanceChangeType.ERC721 || Type == BalanceChangeType.ERC1155)
            {
                var symbol = !string.IsNullOrEmpty(TokenSymbol) ? TokenSymbol : "NFT";
                return TokenId.HasValue ? $"{symbol} #{TokenId}" : symbol;
            }
            return !string.IsNullOrEmpty(TokenSymbol) ? TokenSymbol : $"{TokenAddress?.Substring(0, 10)}...";
        }

        public string GetAddressDisplay()
        {
            if (!string.IsNullOrEmpty(AddressLabel))
            {
                return AddressLabel;
            }
            if (!string.IsNullOrEmpty(Address) && Address.Length > 10)
            {
                return $"{Address.Substring(0, 6)}...{Address.Substring(Address.Length - 4)}";
            }
            return Address ?? "unknown";
        }
    }
}
