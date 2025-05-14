using System;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Contracts
{
    public class ContractRevertExceptionHandler
    {
        public static void HandleContractRevertException(RpcResponseException rpcException)
        {
            var encodedErrorData = rpcException.RpcError.GetDataAsString();
            if (!encodedErrorData.IsHex()) return;

            new FunctionCallDecoder().ThrowIfErrorOnOutput(encodedErrorData);

            throw new SmartContractCustomErrorRevertException(encodedErrorData);
        }

       
    }
}