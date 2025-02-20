using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts
{
    public class MulticallInputOutputRpcBatchItem: IRpcRequestResponseBatchItem
        
    {
        public int RpcObjectId { get; protected set; }
        public IMulticallInputOutput MulticallInputOutput { get; protected set; }
        public RpcRequestResponseBatchItem<EthCall, string> RpcRequestResponseBatchItem { get; protected set; }

        public bool HasError => ((IRpcRequestResponseBatchItem)RpcRequestResponseBatchItem).HasError;

        public object RawResponse => ((IRpcRequestResponseBatchItem)RpcRequestResponseBatchItem).RawResponse;

        public JsonRpc.Client.RpcError RpcError => ((IRpcRequestResponseBatchItem)RpcRequestResponseBatchItem).RpcError;

        public RpcRequestMessage RpcRequestMessage => ((IRpcRequestResponseBatchItem)RpcRequestResponseBatchItem).RpcRequestMessage;


        public MulticallInputOutputRpcBatchItem(IMulticallInputOutput multicallInputOutput, RpcRequestResponseBatchItem<EthCall, string> rpcRequestResponseBatchItem, int rpcObjectId)   
        {
           MulticallInputOutput = multicallInputOutput;
           RpcRequestResponseBatchItem = rpcRequestResponseBatchItem;
           RpcObjectId = rpcObjectId;
        }

        public void DecodeResponse(RpcResponseMessage rpcResponse)
        {
            ((IRpcRequestResponseBatchItem)RpcRequestResponseBatchItem).DecodeResponse(rpcResponse);
            MulticallInputOutput.Decode(RpcRequestResponseBatchItem.Response.HexToByteArray());
        }
    }
}