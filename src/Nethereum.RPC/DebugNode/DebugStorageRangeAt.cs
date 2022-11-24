using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugNode.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RPC.DebugNode
{
    public class DebugStorageRangeAt : RpcRequestResponseHandler<DebugStorageAtResult>, IDebugStorageRangeAt
    {
        public DebugStorageRangeAt(IClient client)
            : base(client, ApiMethods.debug_storageRangeAt.ToString())
        {
        }

        public Task<DebugStorageAtResult> SendRequestAsync(string blockHash, int transactionIndex, string address, string startKeyHex, int limit, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            if (address == null) throw new ArgumentNullException(nameof(address));

            return base.SendRequestAsync(id, blockHash, transactionIndex, address, startKeyHex, limit);
        }

        public Task<DebugStorageAtResult> SendRequestAsync(string blockHash, int transactionIndex, string address, BigInteger startKey, int limit, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            if (address == null) throw new ArgumentNullException(nameof(address));
            var startKeyHex = startKey.ConvertToByteArray(false).PadTo32Bytes().ToHex(true);
            return SendRequestAsync(id, blockHash, transactionIndex, address, startKeyHex, limit);
        }


        public RpcRequest BuildRequest(string blockHash, int transactionIndex, string address, string startKeyHex, int limit,
             object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            if (address == null) throw new ArgumentNullException(nameof(address));
            return base.BuildRequest(id, blockHash, transactionIndex, address, startKeyHex, limit);
        }
    }
}