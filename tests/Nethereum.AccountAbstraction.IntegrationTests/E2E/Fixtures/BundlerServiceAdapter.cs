using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.AccountAbstraction;
using Nethereum.RPC.Eth.DTOs;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;
using RpcUserOperationReceipt = Nethereum.RPC.AccountAbstraction.DTOs.UserOperationReceipt;
using RpcUserOperationGasEstimate = Nethereum.RPC.AccountAbstraction.DTOs.UserOperationGasEstimate;
using LocalUserOperation = Nethereum.AccountAbstraction.UserOperation;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures
{
    public class BundlerServiceAdapter : IAccountAbstractionBundlerService
    {
        private readonly BundlerService _bundler;
        private readonly BigInteger _chainId;

        public BundlerServiceAdapter(BundlerService bundler, BigInteger chainId)
        {
            _bundler = bundler;
            _chainId = chainId;
            ChainId = new ChainIdAdapter(chainId);
            EstimateUserOperationGas = new EstimateUserOperationGasAdapter(bundler);
            GetUserOperationByHash = new GetUserOperationByHashAdapter(bundler);
            GetUserOperationReceipt = new GetUserOperationReceiptAdapter(bundler);
            SendUserOperation = new SendUserOperationAdapter(bundler);
            SupportedEntryPoints = new SupportedEntryPointsAdapter(bundler);
        }

        public IEthChainId ChainId { get; }
        public IEthEstimateUserOperationGas EstimateUserOperationGas { get; }
        public IEthGetUserOperationByHash GetUserOperationByHash { get; }
        public IEthGetUserOperationReceipt GetUserOperationReceipt { get; }
        public IEthSendUserOperation SendUserOperation { get; }
        public IEthSupportedEntryPoints SupportedEntryPoints { get; }

        public async Task ExecuteBundleAsync()
        {
            await _bundler.ExecuteBundleAsync();
        }

        internal static LocalUserOperation RpcToLocal(RpcUserOperation rpc)
        {
            byte[] initCode = null;
            if (!string.IsNullOrEmpty(rpc.Factory))
            {
                var factoryBytes = rpc.Factory.HexToByteArray();
                var factoryData = rpc.FactoryData?.HexToByteArray() ?? Array.Empty<byte>();
                initCode = new byte[factoryBytes.Length + factoryData.Length];
                Buffer.BlockCopy(factoryBytes, 0, initCode, 0, factoryBytes.Length);
                Buffer.BlockCopy(factoryData, 0, initCode, factoryBytes.Length, factoryData.Length);
            }

            return new LocalUserOperation
            {
                Sender = rpc.Sender,
                Nonce = rpc.Nonce?.Value,
                CallData = rpc.CallData?.HexToByteArray(),
                InitCode = initCode ?? Array.Empty<byte>(),
                CallGasLimit = rpc.CallGasLimit?.Value,
                VerificationGasLimit = rpc.VerificationGasLimit?.Value,
                PreVerificationGas = rpc.PreVerificationGas?.Value,
                MaxFeePerGas = rpc.MaxFeePerGas?.Value,
                MaxPriorityFeePerGas = rpc.MaxPriorityFeePerGas?.Value,
                Paymaster = rpc.Paymaster,
                PaymasterVerificationGasLimit = rpc.PaymasterVerificationGasLimit?.Value,
                PaymasterPostOpGasLimit = rpc.PaymasterPostOpGasLimit?.Value,
                PaymasterData = rpc.PaymasterData?.HexToByteArray(),
                Signature = rpc.Signature?.HexToByteArray()
            };
        }

        internal static RpcUserOperation PackedToRpc(Structs.PackedUserOperation packed)
        {
            return UserOperationConverter.ToRpcFormat(packed);
        }
    }

    internal class ChainIdAdapter : IEthChainId
    {
        private readonly BigInteger _chainId;

        public ChainIdAdapter(BigInteger chainId)
        {
            _chainId = chainId;
        }

        public RpcRequest BuildRequest(object id = null)
        {
            throw new NotImplementedException("BuildRequest not supported for local bundler adapter");
        }

        public Task<HexBigInteger> SendRequestAsync(object id = null)
        {
            return Task.FromResult(new HexBigInteger(_chainId));
        }
    }

    internal class EstimateUserOperationGasAdapter : IEthEstimateUserOperationGas
    {
        private readonly BundlerService _bundler;

        public EstimateUserOperationGasAdapter(BundlerService bundler)
        {
            _bundler = bundler;
        }

        public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, object id = null)
        {
            throw new NotImplementedException("BuildRequest not supported for local bundler adapter");
        }

        public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, StateChange stateChange, object id = null)
        {
            throw new NotImplementedException("BuildRequest not supported for local bundler adapter");
        }

        public async Task<RpcUserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, object id = null)
        {
            var local = BundlerServiceAdapter.RpcToLocal(userOperation);
            return await _bundler.EstimateUserOperationGasAsync(local, entryPoint);
        }

        public Task<RpcUserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, StateChange stateChange, object id = null)
        {
            return SendRequestAsync(userOperation, entryPoint, id);
        }
    }

    internal class GetUserOperationByHashAdapter : IEthGetUserOperationByHash
    {
        private readonly BundlerService _bundler;

        public GetUserOperationByHashAdapter(BundlerService bundler)
        {
            _bundler = bundler;
        }

        public RpcRequest BuildRequest(string userOpHash, object id = null)
        {
            throw new NotImplementedException("BuildRequest not supported for local bundler adapter");
        }

        public async Task<RpcUserOperation> SendRequestAsync(string userOpHash, object id = null)
        {
            var response = await _bundler.GetUserOperationByHashAsync(userOpHash);
            if (response?.UserOperation == null)
                return null;
            return BundlerServiceAdapter.PackedToRpc(response.UserOperation);
        }
    }

    internal class GetUserOperationReceiptAdapter : IEthGetUserOperationReceipt
    {
        private readonly BundlerService _bundler;

        public GetUserOperationReceiptAdapter(BundlerService bundler)
        {
            _bundler = bundler;
        }

        public RpcRequest BuildRequest(string userOpHash, object id = null)
        {
            throw new NotImplementedException("BuildRequest not supported for local bundler adapter");
        }

        public async Task<RpcUserOperationReceipt> SendRequestAsync(string userOpHash, object id = null)
        {
            return await _bundler.GetUserOperationReceiptAsync(userOpHash);
        }
    }

    internal class SendUserOperationAdapter : IEthSendUserOperation
    {
        private readonly BundlerService _bundler;

        public SendUserOperationAdapter(BundlerService bundler)
        {
            _bundler = bundler;
        }

        public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, object id = null)
        {
            throw new NotImplementedException("BuildRequest not supported for local bundler adapter");
        }

        public async Task<string> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, object id = null)
        {
            var packed = UserOperationConverter.FromRpcFormat(userOperation);
            var hash = await _bundler.SendUserOperationAsync(packed, entryPoint);
            await _bundler.ExecuteBundleAsync();
            return hash;
        }
    }

    internal class SupportedEntryPointsAdapter : IEthSupportedEntryPoints
    {
        private readonly BundlerService _bundler;

        public SupportedEntryPointsAdapter(BundlerService bundler)
        {
            _bundler = bundler;
        }

        public RpcRequest BuildRequest(object id = null)
        {
            throw new NotImplementedException("BuildRequest not supported for local bundler adapter");
        }

        public async Task<string[]> SendRequestAsync(object id = null)
        {
            return await _bundler.SupportedEntryPointsAsync();
        }
    }
}
