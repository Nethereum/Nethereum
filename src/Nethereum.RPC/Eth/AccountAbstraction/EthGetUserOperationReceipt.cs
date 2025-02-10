using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{

    /// <summary>
    /// Represents the eth_getUserOperationReceipt RPC method.
    /// Returns the receipt for a given UserOperation based on its hash.
    /// </summary>
    public class EthGetUserOperationReceipt : RpcRequestResponseHandler<UserOperationReceipt>, IEthGetUserOperationReceipt
    {
        public EthGetUserOperationReceipt(IClient client)
            : base(client, ApiMethods.eth_getUserOperationReceipt.ToString())
        {
        }

        /// <summary>
        /// Sends a request to get the UserOperationReceipt for the specified user operation hash.
        /// </summary>
        /// <param name="userOpHash">The user operation hash as a hex string.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>A task returning the UserOperationReceipt.</returns>
        public Task<UserOperationReceipt> SendRequestAsync(string userOpHash, object id = null)
        {
            if (string.IsNullOrEmpty(userOpHash))
                throw new ArgumentNullException(nameof(userOpHash));

            return base.SendRequestAsync(id, userOpHash);
        }

        /// <summary>
        /// Builds the RPC request for retrieving a UserOperationReceipt.
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
