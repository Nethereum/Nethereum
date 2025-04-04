using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    /// <summary>
    /// Represents the eth_sendUserOperation RPC method.
    /// Sends a UserOperation to the given EVM network.
    /// The RPC parameters are: [userOperation, entryPoint]
    /// </summary>
    public class EthSendUserOperationV06 : RpcRequestResponseHandler<string>, IEthSendUserOperationV06
    {
        public EthSendUserOperationV06(IClient client)
            : base(client, ApiMethods.eth_sendUserOperation.ToString())
        {
        }

        /// <summary>
        /// Sends a v0.6 UserOperation to the network.
        /// </summary>
        /// <param name="userOperation">The v0.6 user operation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>The user operation hash as a hex string.</returns>
        public Task<string> SendRequestAsync(UserOperationV06 userOperation, string entryPoint, object id = null)
        {
            if (userOperation == null) throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint)) throw new ArgumentNullException(nameof(entryPoint));
            return base.SendRequestAsync(id, userOperation, entryPoint);
        }

        /// <summary>
        /// Builds the RPC request for a v0.6 UserOperation.
        /// </summary>
        /// <param name="userOperation">The v0.6 user operation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(UserOperationV06 userOperation, string entryPoint, object id = null)
        {
            if (userOperation == null) throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint)) throw new ArgumentNullException(nameof(entryPoint));
            return base.BuildRequest(id, userOperation, entryPoint);
        }
    }
}
