using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

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
            return AddressUtil.Current.IsValidEthereumAdressHexFormat(address);
        }

        public static bool IsValidEthereumAddressLength(this string address)
        {
            return AddressUtil.Current.IsValidAddressLength(address);
        }
    }


    public static class ContractUtils
    {
        public static string CalculateContractAddress(string address, BigInteger nonce)
        {
            var sha3 = new Sha3Keccack();
            return  
               sha3.CalculateHash(RLP.RLP.EncodeList(RLP.RLP.EncodeElement(address.HexToByteArray()),
                RLP.RLP.EncodeElement(nonce.ToBytesForRLPEncoding()))).ToHex().Substring(24);
        }
    }

    public static class TransactionUtils
    {
        public static string CalculateTransactionHash(string rawSignedTransaction)
        {
            var sha3 = new Sha3Keccack();
            return sha3.CalculateHashFromHex(rawSignedTransaction);
        }
    }

}