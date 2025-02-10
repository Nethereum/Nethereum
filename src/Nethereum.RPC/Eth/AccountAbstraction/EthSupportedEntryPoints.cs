using Nethereum.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{


    /// <summary>
    /// Represents the eth_supportedEntryPoints RPC method.
    /// Returns an array of the entry point addresses supported by the client.
    /// </summary>
    public class EthSupportedEntryPoints : RpcRequestResponseHandler<string[]>, IEthSupportedEntryPoints
    {
        public EthSupportedEntryPoints(IClient client)
            : base(client, ApiMethods.eth_supportedEntryPoints.ToString())
        {
        }

        /// <summary>
        /// Sends a request to retrieve the supported entry point addresses.
        /// </summary>
        /// <param name="id">Optional request id.</param>
        /// <returns>A task that returns a list of entry point addresses as strings.</returns>
        public Task<string[]> SendRequestAsync(object id = null)
        {
            return base.SendRequestAsync(id);
        }

        /// <summary>
        /// Builds the RPC request for retrieving supported entry point addresses.
        /// </summary>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(object id = null)
        {
            return base.BuildRequest(id);
        }
    }
}
