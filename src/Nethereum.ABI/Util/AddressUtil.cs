using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.Util
{
    public class AddressUtil
    {
        public string ConvertToChecksumAddress(string address)
        {
            address = address.ToLower().RemoveHexPrefix();
            var addressHash = new Sha3Keccack().CalculateHash(address);
            var checksumAddress = "0x";

            for (var i = 0; i < address.Length; i++)
            {
                // If ith character is 9 to f then make it uppercase 
                if (int.Parse(addressHash[i].ToString(), System.Globalization.NumberStyles.HexNumber) > 7)
                {
                    checksumAddress += address[i].ToString().ToUpper();
                }
                else
                {
                    checksumAddress += address[i];
                }
            }
            return checksumAddress;
        }

        public bool IsChecksumAddress(string address)
        {
            address = address.RemoveHexPrefix();
            var addressHash = new Sha3Keccack().CalculateHash(address.ToLower());

            for (var i = 0; i < 40; i++)
            {
                var value = int.Parse(addressHash[i].ToString(), System.Globalization.NumberStyles.HexNumber);
                // the nth letter should be uppercase if the nth digit of casemap is 1
                if ((value > 7 && (address[i].ToString().ToUpper() != address[i].ToString())) || 
                    (value <= 7 && (address[i].ToString().ToLower() != address[i].ToString())))
                {
                    return false;
                }
            }
            return true;
        }

        public string ConvertToValid20ByteAddress(string address)
        {
            address = address.RemoveHexPrefix();
            return address.PadLeft(40, '0').EnsureHexPrefix();
        }


   }
}
