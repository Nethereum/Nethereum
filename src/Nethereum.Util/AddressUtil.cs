using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Util
{
    public class AddressUtil
    {
        private static AddressUtil _current;

        public static AddressUtil Current
        {
            get
            {
                if (_current == null) _current = new AddressUtil();
                return _current;
            }
        }

        public bool AreAddressesTheSame(string address1, string address2)
        {
            //simple string comparison as opposed to use big integer comparison
            return string.Equals(address1.EnsureHexPrefix()?.ToLowerInvariant(), address2.EnsureHexPrefix()?.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase); 
        }

        public string ConvertToChecksumAddress(string address)
        {
            address = address.ToLower().RemoveHexPrefix();
            var addressHash = new Sha3Keccack().CalculateHash(address);
            var checksumAddress = "0x";

            for (var i = 0; i < address.Length; i++)
                if (int.Parse(addressHash[i].ToString(), NumberStyles.HexNumber) > 7)
                    checksumAddress += address[i].ToString().ToUpper();
                else
                    checksumAddress += address[i];
            return checksumAddress;
        }

        public string ConvertToValid20ByteAddress(string address)
        {
            if (address == null) address = string.Empty;
            address = address.RemoveHexPrefix();
            return address.PadLeft(40, '0').EnsureHexPrefix();
        }

        public bool IsValidAddressLength(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            address = address.RemoveHexPrefix();
            return address.Length == 40;
        }

        /// <summary>
        /// Validates if the hex string is 40 alphanumeric characters
        /// </summary>
        public bool IsValidEthereumAddressHexFormat(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            return address.HasHexPrefix() && IsValidAddressLength(address) &&
                   address.ToCharArray().All(char.IsLetterOrDigit);
        }

        public bool IsChecksumAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            address = address.RemoveHexPrefix();
            var addressHash = new Sha3Keccack().CalculateHash(address.ToLower());

            for (var i = 0; i < 40; i++)
            {
                var value = int.Parse(addressHash[i].ToString(), NumberStyles.HexNumber);
                // the nth letter should be uppercase if the nth digit of casemap is 1
                if (value > 7 && address[i].ToString().ToUpper() != address[i].ToString() ||
                    value <= 7 && address[i].ToString().ToLower() != address[i].ToString())
                    return false;
            }
            return true;
        }
    }
}
