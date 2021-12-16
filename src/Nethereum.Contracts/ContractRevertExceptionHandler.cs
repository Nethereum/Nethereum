using System;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using Newtonsoft.Json.Linq;

namespace Nethereum.Contracts
{
    public class ContractRevertExceptionHandler
    {
        public static void HandleContractRevertException(RpcResponseException rpcException)
        {
            if (rpcException.RpcError.Data != null)
            {
                var encodedErrorData = rpcException.RpcError.Data.ToString();
                if (encodedErrorData.IsHex())
                {
                    //check normal revert
                    new FunctionCallDecoder().ThrowIfErrorOnOutput(encodedErrorData);

                    //throw custom error
                    throw new SmartContractCustomErrorRevertException(encodedErrorData);
                }
            }
        }

       
    }
}