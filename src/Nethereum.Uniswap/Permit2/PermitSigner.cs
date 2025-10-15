using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Uniswap.Core.Permit2.ContractDefinition;
using Nethereum.Util;
using System.Numerics;

namespace Nethereum.Uniswap.Permit2
{
    public class PermitSigner
    {
        public static string SignPermitSingle(BigInteger chainId, string verifyingContract, PermitSingle permitSingle, EthECKey key)
        {
            
            var typedData = Permit2TypedData.GetPermitSingleTypeDefinition(chainId, verifyingContract);
            var signer = new Eip712TypedDataSigner();
            return signer.SignTypedDataV4(permitSingle, typedData, key);
        }

        public static byte[] HashPermitSingle(BigInteger chainId, string verifyingContract, PermitSingle permitSingle)
        {
            var typedData = Permit2TypedData.GetPermitSingleTypeDefinition(chainId, verifyingContract);
            var signer = new Eip712TypedDataSigner();
            var encoded = signer.EncodeTypedData(permitSingle, typedData);
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        public static string SignPermitBatch(BigInteger chainId, string verifyingContract, PermitBatch permitBatch, EthECKey key)
        {
            var typedData = Permit2TypedData.GetPermitBatchTypeDefinition(chainId, verifyingContract);
            var signer = new Eip712TypedDataSigner();
            return signer.SignTypedDataV4(permitBatch, typedData, key);
        }

        public static string SignPermitTransferFrom(BigInteger chainId, string verifyingContract, PermitTransferFrom permitTransferFrom, EthECKey key)
        {
            var typedData = Permit2TypedData.GetPermitTransferFromTypeDefinition(chainId, verifyingContract);
            var signer = new Eip712TypedDataSigner();
            return signer.SignTypedDataV4(permitTransferFrom, typedData, key);
        }

        public static string SignPermitBatchTransferFrom(BigInteger chainId, string verifyingContract, PermitBatchTransferFrom permitBatchTransferFrom, EthECKey key)
        {
            var typedData = Permit2TypedData.GetPermitBatchTransferFromTypeDefinition(chainId, verifyingContract);
            var signer = new Eip712TypedDataSigner();
            return signer.SignTypedDataV4(permitBatchTransferFrom, typedData, key);
        }
    }
}
