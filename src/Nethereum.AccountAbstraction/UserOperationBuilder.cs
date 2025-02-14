using Nethereum.ABI;
using Nethereum.ABI.EIP712;
using Nethereum.RLP;
using Nethereum.Util;
using System.Numerics;


namespace Nethereum.AccountAbstraction
{
    public class UserOperationBuilder
    {

        public static PackedUserOperation Pack(UserOperation userOperation)
        {
            var accountGasLimits = PackAccountGasLimits(userOperation.VerificationGasLimit, userOperation.CallGasLimit);
            var gasFees = PackAccountGasLimits(userOperation.MaxPriorityFeePerGas, userOperation.MaxFeePerGas);
            var paymasterAndData = new byte[0];
            if(userOperation.Paymaster.IsAnEmptyAddress() == false && userOperation.Paymaster.IsValidEthereumAddressLength() && userOperation.Paymaster != AddressUtil.ZERO_ADDRESS)
            {
                paymasterAndData = PackPaymasterData(userOperation.Paymaster, userOperation.PaymasterVerificationGasLimit, userOperation.PaymasterPostOpGasLimit, userOperation.PaymasterData);
            }

            return new PackedUserOperation
            {
                Sender = userOperation.Sender,
                Nonce = userOperation.Nonce,
                CallData = userOperation.CallData,
                AccountGasLimits = accountGasLimits,
                InitCode = userOperation.InitCode,
                PreVerificationGas = userOperation.PreVerificationGas,
                GasFees = gasFees,
                PaymasterAndData = paymasterAndData
            };
        }

        public static byte[] PackAccountGasLimits(BigInteger verificationGasLimit, BigInteger callGasLimit)
        {
           var verificationBytes = ByteUtil.PadBytesLeft(verificationGasLimit.ToBytesForRLPEncoding(), 16);
           var callBytes = ByteUtil.PadBytesLeft(callGasLimit.ToBytesForRLPEncoding(), 16);
           return ByteUtil.Merge(verificationBytes, callBytes);
        }

        public static byte[] PackPaymasterData(string paymaster, BigInteger paymasterVerificationGasLimit, BigInteger postOpGasLimit, byte[] paymasterData)
        {
            return ByteUtil.Merge(
                new AddressType().Encode(paymaster),
                ByteUtil.PadBytesLeft(paymasterVerificationGasLimit.ToBytesForRLPEncoding(), 16),
                ByteUtil.PadBytesLeft(postOpGasLimit.ToBytesForRLPEncoding(), 16),
                paymasterData
            );
        }

        public static byte[] CreateUserOperationEip712Hash(UserOperation userOperation, string entryPointAddress, BigInteger chainId)
        {
            return CreateUserOperationEip712Hash(userOperation, new ERC4337Domain(entryPointAddress, chainId));
        }

        public static byte[] CreateUserOperationEip712Hash(UserOperation userOperation, ERC4337Domain domain)
        {
            var packedUserOperation = Pack(userOperation);
            var typedData = CreateUserOperationTypeData(domain);
            var typedDataEncoder = new Eip712TypedDataEncoder();
            var encoded = typedDataEncoder.EncodeAndHashTypedData(packedUserOperation, typedData);
            return encoded;
        }

        public static TypedData<TDomain> CreateUserOperationTypeData<TDomain>(TDomain domain)
        {
            var typedData = new TypedData<TDomain>();
            typedData.Domain = domain;
            typedData.PrimaryType = typeof(UserOperation).Name;
            typedData.Types = MemberDescriptionFactory.GetTypesMemberDescription(new[] { typeof(PackedUserOperation) });
            return typedData;
        }

    }
}
