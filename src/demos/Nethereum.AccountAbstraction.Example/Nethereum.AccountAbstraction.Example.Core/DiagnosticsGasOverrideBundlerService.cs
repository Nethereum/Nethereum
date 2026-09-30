using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.AccountAbstraction;
using Nethereum.RPC.Eth.DTOs;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;

namespace Nethereum.AccountAbstraction.Example.Core
{
    internal sealed class DiagnosticsGasOverrideBundlerService : IAccountAbstractionBundlerService
    {
        private readonly IAccountAbstractionBundlerService _inner;

        public DiagnosticsGasOverrideBundlerService(IAccountAbstractionBundlerService inner, UserOperationGasEstimate fixedEstimate)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            EstimateUserOperationGas = new FixedGasEstimate(fixedEstimate ?? throw new ArgumentNullException(nameof(fixedEstimate)));
        }

        public IEthChainId ChainId => _inner.ChainId;
        public IEthEstimateUserOperationGas EstimateUserOperationGas { get; }
        public IEthGetUserOperationByHash GetUserOperationByHash => _inner.GetUserOperationByHash;
        public IEthGetUserOperationReceipt GetUserOperationReceipt => _inner.GetUserOperationReceipt;
        public IEthSendUserOperation SendUserOperation => _inner.SendUserOperation;
        public IEthSupportedEntryPoints SupportedEntryPoints => _inner.SupportedEntryPoints;

        private sealed class FixedGasEstimate : IEthEstimateUserOperationGas
        {
            private readonly UserOperationGasEstimate _estimate;

            public FixedGasEstimate(UserOperationGasEstimate estimate)
            {
                _estimate = estimate;
            }

            public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, object id = null)
                => throw new NotSupportedException("Diagnostics-only fixed estimate: no RPC request is ever built.");

            public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, Dictionary<string, StateChange> stateOverrides, object id = null)
                => throw new NotSupportedException("Diagnostics-only fixed estimate: no RPC request is ever built.");

            public Task<UserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, object id = null)
                => Task.FromResult(_estimate);

            public Task<UserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, Dictionary<string, StateChange> stateOverrides, object id = null)
                => Task.FromResult(_estimate);
        }
    }
}
