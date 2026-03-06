using Nethereum.Hex.HexConvertors.Extensions;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;
using DomainUserOperation = Nethereum.AccountAbstraction.UserOperation;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc
{
    public static class UserOperationRpcExtensions
    {
        public static DomainUserOperation ToDomainUserOperation(this RpcUserOperation rpcUserOp)
        {
            return new DomainUserOperation
            {
                Sender = rpcUserOp.Sender,
                Nonce = rpcUserOp.Nonce?.Value,
                InitCode = BuildInitCode(rpcUserOp.Factory, rpcUserOp.FactoryData),
                CallData = rpcUserOp.CallData?.HexToByteArray() ?? Array.Empty<byte>(),
                CallGasLimit = rpcUserOp.CallGasLimit?.Value,
                VerificationGasLimit = rpcUserOp.VerificationGasLimit?.Value,
                PreVerificationGas = rpcUserOp.PreVerificationGas?.Value,
                MaxFeePerGas = rpcUserOp.MaxFeePerGas?.Value,
                MaxPriorityFeePerGas = rpcUserOp.MaxPriorityFeePerGas?.Value,
                Paymaster = rpcUserOp.Paymaster,
                PaymasterData = rpcUserOp.PaymasterData?.HexToByteArray() ?? Array.Empty<byte>(),
                PaymasterVerificationGasLimit = rpcUserOp.PaymasterVerificationGasLimit?.Value,
                PaymasterPostOpGasLimit = rpcUserOp.PaymasterPostOpGasLimit?.Value,
                Signature = rpcUserOp.Signature?.HexToByteArray() ?? Array.Empty<byte>()
            };
        }

        public static PackedUserOperation ToPackedUserOperation(this RpcUserOperation rpcUserOp)
        {
            var domainUserOp = rpcUserOp.ToDomainUserOperation();
            domainUserOp.SetNullValuesToDefaultValues();
            return UserOperationBuilder.PackUserOperation(domainUserOp);
        }

        private static byte[] BuildInitCode(string? factory, string? factoryData)
        {
            if (string.IsNullOrEmpty(factory) || factory == "0x" ||
                factory == Nethereum.Util.AddressUtil.ZERO_ADDRESS)
            {
                return Array.Empty<byte>();
            }

            var factoryBytes = factory.HexToByteArray();
            var dataBytes = string.IsNullOrEmpty(factoryData) || factoryData == "0x"
                ? Array.Empty<byte>()
                : factoryData.HexToByteArray();

            var result = new byte[factoryBytes.Length + dataBytes.Length];
            Buffer.BlockCopy(factoryBytes, 0, result, 0, factoryBytes.Length);
            Buffer.BlockCopy(dataBytes, 0, result, factoryBytes.Length, dataBytes.Length);
            return result;
        }
    }
}
