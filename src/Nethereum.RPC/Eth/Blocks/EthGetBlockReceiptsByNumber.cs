using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    public class EthGetBlockReceiptsByNumber : RpcRequestResponseHandler<TransactionReceipt[]>, IEthGetBlockReceiptsByNumber
    {
        public EthGetBlockReceiptsByNumber(IClient client)
            : base(client, ApiMethods.eth_getBlockReceipts.ToString())
        {
        }

        public Task<TransactionReceipt[]> SendRequestAsync(BlockParameter blockParameter, object id = null)
        {
            if (blockParameter == null) throw new ArgumentNullException(nameof(blockParameter));
            return base.SendRequestAsync(id, blockParameter);
        }

        public Task<TransactionReceipt[]> SendRequestAsync(HexBigInteger number, object id = null)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            return base.SendRequestAsync(id, number);
        }

        public RpcRequestResponseBatchItem<EthGetBlockReceiptsByNumber, TransactionReceipt[]> CreateBatchItem(HexBigInteger number, object id)
        {
            return new RpcRequestResponseBatchItem<EthGetBlockReceiptsByNumber, TransactionReceipt[]>(this, BuildRequest(number, id));
        }

#if !DOTNET35
        public async Task<List<TransactionReceipt[]>> SendBatchRequestAsync(params HexBigInteger[] numbers)
        {
            var batchRequest = new RpcRequestResponseBatch();
            for (int i = 0; i < numbers.Length; i++)
            {
                batchRequest.BatchItems.Add(CreateBatchItem(numbers[i], i));
            }

            var response = await Client.SendBatchRequestAsync(batchRequest);
            return response.BatchItems.Select(x => ((RpcRequestResponseBatchItem<EthGetBlockReceiptsByNumber, TransactionReceipt[]>)x).Response).ToList();

        }
#endif

        public RpcRequest BuildRequest(HexBigInteger number, object id = null)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            return base.BuildRequest(id, number);
        }

        public RpcRequest BuildRequest(BlockParameter blockParameter, object id = null)
        {
            if (blockParameter == null) throw new ArgumentNullException(nameof(blockParameter));
            return base.BuildRequest(id, blockParameter);
        }
    }
}