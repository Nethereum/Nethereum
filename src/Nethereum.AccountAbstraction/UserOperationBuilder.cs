using Nethereum.ABI;
using Nethereum.ABI.Encoders;
using Nethereum.ABI.EIP712;
using Nethereum.Contracts.Services;
using Nethereum.RLP;
using Nethereum.Util;
using System.Numerics;
using System.Linq;
using Nethereum.Signer.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction
{ 
    
    public class UserOperationBuilder
    {
        public static PackedUserOperation PackUserOperation(UserOperation userOperation)
        {
            var packedUserOperationForHash = PackUserOperationForHash(userOperation);
            return new PackedUserOperation
            {
                Sender = packedUserOperationForHash.Sender,
                Nonce = packedUserOperationForHash.Nonce,
                InitCode = packedUserOperationForHash.InitCode,
                CallData = packedUserOperationForHash.CallData,
                AccountGasLimits = packedUserOperationForHash.AccountGasLimits,
                PreVerificationGas = packedUserOperationForHash.PreVerificationGas,
                GasFees = packedUserOperationForHash.GasFees,
                PaymasterAndData = packedUserOperationForHash.PaymasterAndData,
                Signature = userOperation.Signature
            };
        }

        public static PackedUserOperationForHash PackUserOperationForHash(UserOperation userOperation)
        {
            var accountGasLimits = PackAccountGasLimits(userOperation.VerificationGasLimit.Value, userOperation.CallGasLimit.Value);
            var gasFees = PackAccountGasLimits(userOperation.MaxPriorityFeePerGas.Value, userOperation.MaxFeePerGas.Value);
            var paymasterAndData = new byte[0];
            if(userOperation.Paymaster.IsAnEmptyAddress() == false && userOperation.Paymaster.IsValidEthereumAddressLength() && userOperation.Paymaster != AddressUtil.ZERO_ADDRESS)
            {
                paymasterAndData = PackPaymasterData(userOperation.Paymaster, userOperation.PaymasterVerificationGasLimit.Value, userOperation.PaymasterPostOpGasLimit.Value, userOperation.PaymasterData);
            }

            return new PackedUserOperationForHash
            {
                Sender = userOperation.Sender,
                Nonce = userOperation.Nonce.Value,
                CallData = userOperation.CallData,
                AccountGasLimits = accountGasLimits,
                InitCode = userOperation.InitCode,
                PreVerificationGas = userOperation.PreVerificationGas.Value,
                GasFees = gasFees,
                PaymasterAndData = paymasterAndData,
            };
        }

        public static byte[] PackAccountGasLimits(BigInteger verificationGasLimit, BigInteger callGasLimit)
        {
           var verificationBytes = ByteUtil.PadBytesLeft(verificationGasLimit.ToBytesForRLPEncoding(), 16);
           var callBytes = ByteUtil.PadBytesLeft(callGasLimit.ToBytesForRLPEncoding(), 16);
           return ByteUtil.Merge(verificationBytes, callBytes);
        }

        public static byte[] HashUserOperation(PackedUserOperation userOperation, string entryPointAddress, BigInteger chainId)
        {
            var domain = new ERC4337Domain(entryPointAddress, chainId);
            var typedData = CreateUserOperationTypeData(domain);
            var packedUserOperation = ToForHash(userOperation);

            var typedDataEncoder = new Eip712TypedDataEncoder();
            return typedDataEncoder.EncodeAndHashTypedData(packedUserOperation, typedData);
        }

        private static PackedUserOperationForHash ToForHash(PackedUserOperation userOperation)
        {
            return new PackedUserOperationForHash
            {
                Sender = userOperation.Sender,
                Nonce = userOperation.Nonce,
                InitCode = userOperation.InitCode,
                CallData = userOperation.CallData,
                AccountGasLimits = userOperation.AccountGasLimits,
                PreVerificationGas = userOperation.PreVerificationGas,
                GasFees = userOperation.GasFees,
                PaymasterAndData = userOperation.PaymasterAndData
            };
        }

        public static string BuildUserOperationTypedDataJson(PackedUserOperation userOperation, string entryPointAddress, BigInteger chainId)
        {
            var domain = new ERC4337Domain(entryPointAddress, chainId);
            var typedData = CreateUserOperationTypeData(domain);
            var packedUserOperationForHash = ToForHash(userOperation);
            return typedData.ToJson(packedUserOperationForHash);
        }

        public static byte[] PackPaymasterData(string paymaster, BigInteger paymasterVerificationGasLimit, BigInteger postOpGasLimit, byte[] paymasterData)
        {
            return ByteUtil.Merge(
                new AddressTypeEncoder().EncodePacked(paymaster),
                ByteUtil.PadBytesLeft(paymasterVerificationGasLimit.ToBytesForRLPEncoding(), 16),
                ByteUtil.PadBytesLeft(postOpGasLimit.ToBytesForRLPEncoding(), 16),
                paymasterData ?? Array.Empty<byte>()
            );
        }

        public static byte[] PackAndHashEIP712UserOperation(UserOperation userOperation, string entryPointAddress, BigInteger chainId)
        {
            return PackAndHashEIP712UserOperation(userOperation, new ERC4337Domain(entryPointAddress, chainId));
        }

        public static (PackedUserOperation packedOp, byte[] hash) PackAndHashEIP712UserOperationForSigning(UserOperation userOperation, string entryPointAddress, BigInteger chainId)
        {
            var packedUserOperationForHash = PackUserOperationForHash(userOperation);
            var domain = new ERC4337Domain(entryPointAddress, chainId);
            var typedData = CreateUserOperationTypeData(domain);

            var hash = new Eip712TypedDataEncoder().EncodeAndHashTypedData(packedUserOperationForHash, typedData);

            var packedUserOperation = new PackedUserOperation
            {
                Sender = packedUserOperationForHash.Sender,
                Nonce = packedUserOperationForHash.Nonce,
                InitCode = packedUserOperationForHash.InitCode,
                CallData = packedUserOperationForHash.CallData,
                AccountGasLimits = packedUserOperationForHash.AccountGasLimits,
                PreVerificationGas = packedUserOperationForHash.PreVerificationGas,
                GasFees = packedUserOperationForHash.GasFees,
                PaymasterAndData = packedUserOperationForHash.PaymasterAndData
            };
            return (packedUserOperation, hash);
        }

        public static PackedUserOperation PackAndSignEIP712UserOperation(UserOperation userOperation, string entryPointAddress, BigInteger chainId, EthECKey signer)
        {
            var (packedUserOperation, hash) = PackAndHashEIP712UserOperationForSigning(userOperation, entryPointAddress, chainId);
            packedUserOperation.Signature = EthECDSASignature.CreateStringSignature(signer.SignAndCalculateV(hash)).HexToByteArray();
            return packedUserOperation;
        }

        public static byte[] PackAndHashEIP712UserOperation(UserOperation userOperation, ERC4337Domain domain)
        {
            var typedDataEncoder = new Eip712TypedDataEncoder();
            var packedUserOperation = PackUserOperationForHash(userOperation);
            var typedData = CreateUserOperationTypeData(domain);
            var encoded = typedDataEncoder.EncodeAndHashTypedData(packedUserOperation, typedData);
            return encoded;
        }

        public static byte[] PackAndEncodeEip712UserOperation(UserOperation userOperation, string entryPointAddress, BigInteger chainId)
        {
            var packedUserOperation = PackUserOperationForHash(userOperation);
            var domain = new ERC4337Domain(entryPointAddress, chainId);
            var typedData = CreateUserOperationTypeData(domain);
            var typedDataEncoder = new Eip712TypedDataEncoder();
            var encoded = typedDataEncoder.EncodeTypedData(packedUserOperation, typedData);
            return encoded;
        }

        public static byte[] PackAndEncodeUserOperationStruct(UserOperation userOperation)
        {
            var packedUserOperation = PackUserOperationForHash(userOperation);
            var typedDataEncoder = new Eip712TypedDataEncoder();
            return typedDataEncoder.EncodeStruct(packedUserOperation, "PackedUserOperation", typeof(PackedUserOperationForHash));
        }

        public static TypedData<ERC4337Domain> CreateUserOperationTypeData(ERC4337Domain eRC4337Domain)
        {
            var typedData = new TypedData<ERC4337Domain>();
            typedData.Domain = eRC4337Domain;
            typedData.PrimaryType = typeof(PackedUserOperation).Name;
            typedData.Types = MemberDescriptionFactory.GetTypesMemberDescription(new[] { typeof(ERC4337Domain), typeof(PackedUserOperationForHash) });
            return typedData;
        }

    }
}
