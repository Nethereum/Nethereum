using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockReceiptsHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockReceipts.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);

            BigInteger blockNumber;
            if (blockTag == BlockParameter.BlockParameterType.latest.ToString() || blockTag == BlockParameter.BlockParameterType.pending.ToString())
            {
                blockNumber = await context.Node.GetBlockNumberAsync();
            }
            else if (blockTag == BlockParameter.BlockParameterType.earliest.ToString())
            {
                blockNumber = 0;
            }
            else
            {
                blockNumber = blockTag.HexToBigInteger(false);
            }

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);
            if (blockHash == null)
            {
                return Success(request.Id, null);
            }

            var transactions = await context.Node.Transactions.GetByBlockNumberAsync(blockNumber);
            if (transactions == null || transactions.Count == 0)
            {
                return Success(request.Id, new List<object>());
            }

            var result = new List<object>();
            foreach (var tx in transactions)
            {
                var receiptInfo = await context.Node.Receipts.GetInfoByTxHashAsync(tx.Hash);
                if (receiptInfo != null)
                {
                    var from = GetSenderAddress(tx);
                    var to = GetReceiverAddress(tx);
                    result.Add(receiptInfo.ToTransactionReceipt(from, to));
                }
            }

            return Success(request.Id, result);
        }

        private static string GetSenderAddress(ISignedTransaction tx)
        {
            try
            {
                var signature = tx.Signature;
                if (signature == null) return null;

                var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return key != null ? key.GetPublicAddress() : null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetReceiverAddress(ISignedTransaction tx)
        {
            if (tx == null) return null;

            if (tx is LegacyTransaction legacy)
            {
                var addr = legacy.ReceiveAddress;
                return addr != null && addr.Length > 0 ? addr.ToHex(true) : null;
            }
            if (tx is LegacyTransactionChainId legacyChainId)
            {
                var addr = legacyChainId.ReceiveAddress;
                return addr != null && addr.Length > 0 ? addr.ToHex(true) : null;
            }
            if (tx is Transaction1559 eip1559)
                return eip1559.ReceiverAddress;
            if (tx is Transaction2930 eip2930)
                return eip2930.ReceiverAddress;

            return null;
        }
    }
}
