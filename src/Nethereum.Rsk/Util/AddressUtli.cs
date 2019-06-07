using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Rsk.Util
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


        public string ConvertToChecksumAddress(string address, BigInteger? chainId = null)
        {
            address = address.ToLower().RemoveHexPrefix();
            var prefix = chainId != null ? (chainId.ToString() + "0x") : "";
            var addressHash = new Sha3Keccack().CalculateHash(prefix + address);
            var checksumAddress = "0x";

            for (var i = 0; i < address.Length; i++)
                if (int.Parse(addressHash[i].ToString(), NumberStyles.HexNumber) > 7)
                    checksumAddress += address[i].ToString().ToUpper();
                else
                    checksumAddress += address[i];
            return checksumAddress;
        }


        public bool IsChecksumAddress(string address, BigInteger? chainId)
        {
            if (string.IsNullOrEmpty(address)) return false;
            address = address.RemoveHexPrefix();
            var prefix = chainId != null ? (chainId.ToString() + "0x") : "";
            var addressHash = new Sha3Keccack().CalculateHash(prefix + address.ToLower());

            for (var i = 0; i < address.Length; i++)
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
