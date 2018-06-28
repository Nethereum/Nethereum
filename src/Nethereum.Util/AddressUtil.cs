using System.Globalization;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;

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
    }


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
            address = address.RemoveHexPrefix();
            return address.PadLeft(40, '0').EnsureHexPrefix();
        }

        public bool IsValidAddressLength(string address)
        {
            address = address.RemoveHexPrefix();
            return address.Length == 40;
        }

        //public bool IsValidEthereumAddress(string address)
        //{
        //    Regex r = new Regex("^(0x){1}[0-9a-fA-F]{40}$");
        //    // Doesn't match length, prefix and hex
        //    if (!r.IsMatch(address))
        //        return false;
        //    // It's all lowercase, so no checksum needed
        //    else if (address == address.ToLower())
        //        return true;
        //    // Do checksum
        //    else
        //    {
        //        return new AddressUtil().IsChecksumAddress(address);
        //    }
        //}

        public bool IsChecksumAddress(string address)
        {
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