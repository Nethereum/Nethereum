using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;
using DomainUserOperation = Nethereum.AccountAbstraction.UserOperation;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc
{
    public static class UserOperationRpcExtensions
    {
        public static Dictionary<string, object> ToUnpackedRpcDictionary(this RpcUserOperation rpcUserOp)
        {
            var userOperation = new Dictionary<string, object>
            {
                ["sender"] = rpcUserOp.Sender,
                ["nonce"] = rpcUserOp.Nonce?.HexValue ?? "0x0"
            };

            if (rpcUserOp.Factory != null)
            {
                userOperation["factory"] = rpcUserOp.Factory;
                userOperation["factoryData"] = rpcUserOp.FactoryData;
            }

            userOperation["callData"] = rpcUserOp.CallData ?? "0x";
            userOperation["callGasLimit"] = rpcUserOp.CallGasLimit?.HexValue ?? "0x0";
            userOperation["verificationGasLimit"] = rpcUserOp.VerificationGasLimit?.HexValue ?? "0x0";
            userOperation["preVerificationGas"] = rpcUserOp.PreVerificationGas?.HexValue ?? "0x0";
            userOperation["maxFeePerGas"] = rpcUserOp.MaxFeePerGas?.HexValue ?? "0x0";
            userOperation["maxPriorityFeePerGas"] = rpcUserOp.MaxPriorityFeePerGas?.HexValue ?? "0x0";

            if (rpcUserOp.Paymaster != null)
            {
                userOperation["paymaster"] = rpcUserOp.Paymaster;
                userOperation["paymasterVerificationGasLimit"] = rpcUserOp.PaymasterVerificationGasLimit?.HexValue;
                userOperation["paymasterPostOpGasLimit"] = rpcUserOp.PaymasterPostOpGasLimit?.HexValue;
                userOperation["paymasterData"] = rpcUserOp.PaymasterData;
            }

            userOperation["signature"] = rpcUserOp.Signature ?? "0x";

            return userOperation;
        }

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
                Signature = rpcUserOp.Signature?.HexToByteArray() ?? Array.Empty<byte>(),
                // EIP-7702 auth is a side-channel, not packed: carry it through the RPC to domain hop.
                Eip7702Auth = rpcUserOp.Eip7702Auth
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
            var dataBytes = string.IsNullOrEmpty(factoryData) || factoryData == "0x"
                ? Array.Empty<byte>()
                : factoryData.HexToByteArray();

            return AAEIP7702Utils.BuildInitCode(factory, dataBytes);
        }
    }
}
