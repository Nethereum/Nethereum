namespace Nethereum.Util
{
    public static class AddressExtensions
    {
        public static string ConvertToEthereumChecksumAddress(this string address)
        {
           return AddressUtil.Current.ConvertToChecksumAddress(address);
        }

        public static bool IsEthereumChecksumAddress(this string address)
        {
            return AddressUtil.Current.IsChecksumAddress(address);
        }

        /// <summary>
        /// Validates if the hex string is 40 alphanumeric characters
        /// </summary>
        public static bool IsValidEthereumAddressHexFormat(this string address)
        {
            return AddressUtil.Current.IsValidEthereumAddressHexFormat(address);
        }

        public static bool IsValidEthereumAddressLength(this string address)
        {
            return AddressUtil.Current.IsValidAddressLength(address);
        }
    }
}