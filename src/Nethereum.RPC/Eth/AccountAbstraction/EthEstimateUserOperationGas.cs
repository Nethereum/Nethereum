using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    /// <summary>
    /// Represents the eth_estimateUserOperationGas RPC method.
    /// Estimates the gas values for a given UserOperation.
    /// The RPC parameters are: [userOperation, entryPoint] or [userOperation, entryPoint, stateChange].
    /// </summary>
    public class EthEstimateUserOperationGas : RpcRequestResponseHandler<UserOperationGasEstimate>, IEthEstimateUserOperationGas
    {
        public EthEstimateUserOperationGas(IClient client)
            : base(client, ApiMethods.eth_estimateUserOperationGas.ToString())
        {
        }

        /// <summary>
        /// Sends a request to estimate gas for the provided v7 UserOperation.
        /// </summary>
        /// <param name="userOperation">The v7 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>A task returning a UserOperationGasEstimate.</returns>
        public Task<UserOperationGasEstimate> SendRequestAsync(UserOperation userOperation, string entryPoint, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));

            return base.SendRequestAsync(id, userOperation, entryPoint);
        }

        /// <summary>
        /// Builds the RPC request for estimating gas for the provided v7 UserOperation.
        /// </summary>
        /// <param name="userOperation">The v7 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(UserOperation userOperation, string entryPoint, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));

            return base.BuildRequest(id, userOperation, entryPoint);
        }

        /// <summary>
        /// Sends a request to estimate gas for the provided v7 UserOperation with state change.
        /// </summary>
        /// <param name="userOperation">The v7 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="stateChange">The state change object to be applied.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>A task returning a UserOperationGasEstimate.</returns>
        public Task<UserOperationGasEstimate> SendRequestAsync(UserOperation userOperation, string entryPoint, StateChange stateChange, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));
            if (stateChange == null)
                throw new ArgumentNullException(nameof(stateChange));

            return base.SendRequestAsync(id, userOperation, entryPoint, stateChange);
        }

        /// <summary>
        /// Builds the RPC request for estimating gas for the provided v7 UserOperation with state change.
        /// </summary>
        /// <param name="userOperation">The v7 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="stateChange">The state change object to be applied.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(UserOperation userOperation, string entryPoint, StateChange stateChange, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));
            if (stateChange == null)
                throw new ArgumentNullException(nameof(stateChange));

            return base.BuildRequest(id, userOperation, entryPoint, stateChange);
        }
    }
}
