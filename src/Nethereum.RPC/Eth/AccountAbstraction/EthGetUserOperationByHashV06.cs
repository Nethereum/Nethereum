using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    /// <summary>
    /// Represents the eth_getUserOperationByHash RPC method.
    /// Returns a UserOperation object based on the provided userOpHash.
    /// </summary>
    public class EthGetUserOperationByHashV06 : RpcRequestResponseHandler<UserOperationV06>, IEthGetUserOperationByHashV06
    {
        public EthGetUserOperationByHashV06(IClient client)
            : base(client, ApiMethods.eth_getUserOperationByHash.ToString())
        {
        }

        /// <summary>
        /// Sends a request to retrieve a UserOperation by its hash.
        /// </summary>
        /// <param name="userOpHash">The user operation hash as a hex string.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>
        /// A task returning the UserOperation (in v0.6 format) or null if not found.
        /// </returns>
        public Task<UserOperationV06> SendRequestAsync(string userOpHash, object id = null)
        {
            if (string.IsNullOrEmpty(userOpHash))
                throw new ArgumentNullException(nameof(userOpHash));

            return base.SendRequestAsync(id, userOpHash);
        }

        /// <summary>
        /// Builds the RPC request for retrieving a UserOperation by its hash.
        /// </summary>
        /// <param name="userOpHash">The user operation hash as a hex string.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(string userOpHash, object id = null)
        {
            if (string.IsNullOrEmpty(userOpHash))
                throw new ArgumentNullException(nameof(userOpHash));

            return base.BuildRequest(id, userOpHash);
        }
    }
}
