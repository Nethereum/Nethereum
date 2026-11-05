#if !EVM_SYNC
using System.Numerics;
#endif
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Util
{
    public static class ContractUtils
    {
        public const string LIBRARY_PLACEHOLDER = "__$";

        public const int MAXIMUM_BYTECODE_SIZE = 24576;

        public static string CalculateContractAddress(string address, long nonce)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new System.ArgumentException($"'{nameof(address)}' cannot be null or empty.", nameof(address));
            }

            var sha3 = new Sha3Keccack();
            return
                sha3.CalculateHash(RLP.RLP.EncodeList(RLP.RLP.EncodeElement(address.HexToByteArray()),
                        RLP.RLP.EncodeElement(nonce.ToBytesForRLPEncoding()))).ToHex().Substring(24)
                    .ConvertToEthereumChecksumAddress();
        }

#if !EVM_SYNC
        public static string CalculateContractAddress(string address, BigInteger nonce)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new System.ArgumentException($"'{nameof(address)}' cannot be null or empty.", nameof(address));
            }

            var sha3 = new Sha3Keccack();
            return
                sha3.CalculateHash(RLP.RLP.EncodeList(RLP.RLP.EncodeElement(address.HexToByteArray()),
                        RLP.RLP.EncodeElement(nonce.ToBytesForRLPEncoding()))).ToHex().Substring(24)
                    .ConvertToEthereumChecksumAddress();
        }
#endif

        public static string CalculateCreate2AddressMinimalProxy(string address, string saltHex,
            string deploymentAddress)
        {
            if (string.IsNullOrEmpty(deploymentAddress))
            {
                throw new System.ArgumentException($"'{nameof(deploymentAddress)}' cannot be null or empty.",
                    nameof(deploymentAddress));
            }

            var bytecode = "363d3d373d3d3d363d73" + deploymentAddress.RemoveHexPrefix() +
                           "5af43d82803e903d91602b57fd5bf3";
            return CalculateCreate2Address(address, saltHex, bytecode);
        }

        public static string CalculateCreate2Address(string address, string saltHex, string byteCodeHex)
        {
            if(byteCodeHex.Contains(LIBRARY_PLACEHOLDER)) throw new System.ArgumentException($"'{nameof(byteCodeHex)}' cannot contain the placeholder {LIBRARY_PLACEHOLDER}", nameof(byteCodeHex));
            var sha3 = new Sha3Keccack();
            return CalculateCreate2AddressUsingByteCodeHash(address, saltHex, sha3.CalculateHashFromHex(byteCodeHex));
        }


        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "util-create2-bytes", "ContractUtils.CalculateCreate2Address from bytes")]
        public static string CalculateCreate2Address(string address, byte[] salt, byte[] byteCode)
        {
            var sha3 = new Sha3Keccack();
            var preimage = new byte[1 + 20 + salt.Length + 32];
            preimage[0] = 0xff;
            System.Array.Copy(address.HexToByteArray(), 0, preimage, 1, 20);
            System.Array.Copy(salt, 0, preimage, 21, salt.Length);
            System.Array.Copy(sha3.CalculateHash(byteCode), 0, preimage, 21 + salt.Length, 32);
            return sha3.CalculateHash(preimage).ToHex().Substring(24).ConvertToEthereumChecksumAddress();
        }

        public static bool IsContractByteCodeSizeAboveLimit(string byteCodeHex)
        {
            return byteCodeHex.RemoveHexPrefix().Length / 2 > MAXIMUM_BYTECODE_SIZE;
        }

        public static string CalculateCreate2AddressUsingByteCodeHash(string address, string saltHex, string byteCodeHexHash)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new System.ArgumentException($"'{nameof(address)}' cannot be null or empty.", nameof(address));
            }

            if (string.IsNullOrEmpty(saltHex))
            {
                throw new System.ArgumentException($"'{nameof(saltHex)}' cannot be null or empty.", nameof(saltHex));
            }

            if (saltHex.EnsureHexPrefix().Length != 66)
            {
                throw new System.ArgumentException($"'{nameof(saltHex)}' needs to be 32 bytes", nameof(saltHex));
            }
            var sha3 = new Sha3Keccack();
            return sha3.CalculateHashFromHex("0xff", address, saltHex, byteCodeHexHash)
                .Substring(24).ConvertToEthereumChecksumAddress();
        }
    }
}
