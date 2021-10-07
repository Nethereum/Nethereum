using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.Services
{
    public class EthGetContractTransactionErrorReason: IEthGetContractTransactionErrorReason
    {
        private readonly IEthApiTransactionsService _apiTransactionsService;

        public EthGetContractTransactionErrorReason(IEthApiTransactionsService apiTransactionsService)
        {
            _apiTransactionsService = apiTransactionsService;
        }
#if !DOTNET35
        public async Task<string> SendRequestAsync(string transactionHash)
        {
            var transaction = await _apiTransactionsService.GetTransactionByHash.SendRequestAsync(transactionHash);
            var transactionInput = transaction.ConvertToTransactionInput();
            var functionCallDecoder = new FunctionCallDecoder();
            if (transactionInput.MaxFeePerGas != null)
            {
                transactionInput.GasPrice = null;
            }
            try
            {
                var errorHex = await _apiTransactionsService.Call.SendRequestAsync(transactionInput, new BlockParameter(transaction.BlockNumber));

                if (ErrorFunction.IsErrorData(errorHex))
                {
                    return functionCallDecoder.DecodeFunctionErrorMessage(errorHex);
                }
                return string.Empty;

            }
            catch (RpcResponseException rpcException)
            {
                if (rpcException.RpcError.Data != null)
                {
                    var encodedErrorData = rpcException.RpcError.Data.ToObject<string>();
                    if (encodedErrorData.IsHex())
                    {
                        var error = functionCallDecoder.DecodeFunctionErrorMessage(encodedErrorData);
                        if (error != null)
                        {
                            return error;
                        }
                        throw new SmartContractCustomErrorRevertException(encodedErrorData);
                    }  
                }
               throw;
            }
        }
#endif
    }
}