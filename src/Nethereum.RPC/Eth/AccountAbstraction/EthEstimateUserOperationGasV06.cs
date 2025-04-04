using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public class EthEstimateUserOperationGasV06 : RpcRequestResponseHandler<UserOperationGasEstimate>, IEthEstimateUserOperationGasV06
    {
        public EthEstimateUserOperationGasV06(IClient client)
           : base(client, ApiMethods.eth_estimateUserOperationGas.ToString())
        {
        }

        /// <summary>
        /// Sends a request to estimate gas for the provided v06 UserOperation.
        /// </summary>
        /// <param name="userOperation">The v06 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>A task returning a UserOperationGasEstimate.</returns>
        public Task<UserOperationGasEstimate> SendRequestAsync(UserOperationV06 userOperation, string entryPoint, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));

            return base.SendRequestAsync(id, userOperation, entryPoint);
        }

        /// <summary>
        /// Builds the RPC request for estimating gas for the provided v6 UserOperation.
        /// </summary>
        /// <param name="userOperation">The v6 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(UserOperationV06 userOperation, string entryPoint, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));

            return base.BuildRequest(id, userOperation, entryPoint);
        }

        /// <summary>
        /// Sends a request to estimate gas for the provided v6 UserOperation with state change.
        /// </summary>
        /// <param name="userOperation">The v6 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="stateChange">The state change object to be applied.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>A task returning a UserOperationGasEstimate.</returns>
        public Task<UserOperationGasEstimate> SendRequestAsync(UserOperationV06 userOperation, string entryPoint, StateChange stateChange, object id = null)
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
        /// Builds the RPC request for estimating gas for the provided v6 UserOperation with state change.
        /// </summary>
        /// <param name="userOperation">The v6 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="stateChange">The state change object to be applied.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(UserOperationV06 userOperation, string entryPoint, StateChange stateChange, object id = null)
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
