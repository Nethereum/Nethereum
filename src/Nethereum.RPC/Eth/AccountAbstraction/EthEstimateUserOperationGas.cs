using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{

    /// <summary>
    /// Represents the eth_estimateUserOperationGas RPC method.
    /// Estimates the gas values for a given UserOperation.
    /// The RPC parameters are: [userOperation, entryPoint] or
    /// [userOperation, entryPoint, stateOverrides] where stateOverrides is the
    /// address-keyed override set used by eth_call.
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
        /// Sends a request to estimate gas for the provided v7 UserOperation with state overrides.
        /// </summary>
        /// <param name="userOperation">The v7 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="stateOverrides">Address-keyed state override set, as in eth_call.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>A task returning a UserOperationGasEstimate.</returns>
        public Task<UserOperationGasEstimate> SendRequestAsync(UserOperation userOperation, string entryPoint, Dictionary<string, StateChange> stateOverrides, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));
            if (stateOverrides == null)
                throw new ArgumentNullException(nameof(stateOverrides));

            return base.SendRequestAsync(id, userOperation, entryPoint, stateOverrides);
        }

        /// <summary>
        /// Builds the RPC request for estimating gas for the provided v7 UserOperation with state overrides.
        /// </summary>
        /// <param name="userOperation">The v7 UserOperation object.</param>
        /// <param name="entryPoint">The EntryPoint contract address.</param>
        /// <param name="stateOverrides">Address-keyed state override set, as in eth_call.</param>
        /// <param name="id">Optional request id.</param>
        /// <returns>An RpcRequest object.</returns>
        public RpcRequest BuildRequest(UserOperation userOperation, string entryPoint, Dictionary<string, StateChange> stateOverrides, object id = null)
        {
            if (userOperation == null)
                throw new ArgumentNullException(nameof(userOperation));
            if (string.IsNullOrEmpty(entryPoint))
                throw new ArgumentNullException(nameof(entryPoint));
            if (stateOverrides == null)
                throw new ArgumentNullException(nameof(stateOverrides));

            return base.BuildRequest(id, userOperation, entryPoint, stateOverrides);
        }


       
    }
}
