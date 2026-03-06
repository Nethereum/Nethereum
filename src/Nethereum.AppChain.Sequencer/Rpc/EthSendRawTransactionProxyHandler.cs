using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.AppChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.AppChain.Sequencer.Rpc
{
    public class EthSendRawTransactionProxyHandler : RpcHandlerBase
    {
        private readonly ISequencerTxProxy _txProxy;
        private readonly AppChainReplicaConfig _config;

        public EthSendRawTransactionProxyHandler(ISequencerTxProxy txProxy, AppChainReplicaConfig config)
        {
            _txProxy = txProxy;
            _config = config;
        }

        public override string MethodName => ApiMethods.eth_sendRawTransaction.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var rawTxHex = GetParam<string>(request, 0);
            var rawTxBytes = rawTxHex.HexToByteArray();

            var txHash = await _txProxy.SendRawTransactionAsync(rawTxBytes);

            if (_config.TxConfirmationTimeoutMs > 0)
            {
                await _txProxy.WaitForReceiptAsync(
                    txHash,
                    _config.TxConfirmationTimeoutMs,
                    _config.TxPollIntervalMs);
            }

            return Success(request.Id, txHash.ToHex(true));
        }
    }
}
