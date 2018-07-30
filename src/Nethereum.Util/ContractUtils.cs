using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Util
{
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
}