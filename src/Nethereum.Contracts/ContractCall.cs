using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts
{
    public class ContractCall
    {
        private readonly IEthCall _ethCall;
        private readonly BlockParameter _defaulBlock;

        public ContractCall(IEthCall ethCall, BlockParameter defaulBlock)
        {
            _ethCall = ethCall;
            _defaulBlock = defaulBlock;
            if (_defaulBlock == null) _defaulBlock = BlockParameter.CreateLatest();
        }

#if !DOTNET35
        public async Task<string> CallAsync(CallInput callInput, BlockParameter block = null)
        {
            try
            {
                if (block == null) block = _defaulBlock;
                return await _ethCall.SendRequestAsync(callInput, block).ConfigureAwait(false);
            }
            catch (RpcResponseException rpcException)
            {
                if (rpcException.RpcError.Data != null)
                {
                    var encodedErrorData = rpcException.RpcError.Data.ToObject<string>();
                    if (encodedErrorData.IsHex())
                    {
                        //check normal revert
                        new FunctionCallDecoder().ThrowIfErrorOnOutput(encodedErrorData);

                        //throw custom error
                        throw new SmartContractCustomErrorRevertException(encodedErrorData);
                    }
                }
                throw;
            }
        }
#endif
    }
}