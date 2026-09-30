using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.Web3;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.Bundler.InProcess
{
    public class InProcessBundlerHost : IDisposable, IAsyncDisposable
    {
        private bool _disposed;

        public DevChainNode Node { get; }
        public IWeb3 OperatorWeb3 { get; }
        public BigInteger ChainId { get; }

        public BundlerService BundlerService { get; private set; }
        public IAccountAbstractionBundlerService Bundler { get; private set; }

        private InProcessBundlerHost(DevChainNode node, IWeb3 operatorWeb3, BigInteger chainId)
        {
            Node = node;
            OperatorWeb3 = operatorWeb3;
            ChainId = chainId;
        }

        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Start an in-process DevChain and operator Web3 for an embedded bundler", Order = 1)]
        public static async Task<InProcessBundlerHost> StartAsync(
            Web3Account operatorAccount,
            BigInteger chainId,
            IEnumerable<string> prefundedAddresses,
            BigInteger prefundBalanceWei,
            DevChainConfig config = null)
        {
            config ??= new DevChainConfig
            {
                ChainId = (int)chainId,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            var node = new DevChainNode(config);
            await node.StartAsync(prefundedAddresses, prefundBalanceWei);

            var operatorWeb3 = node.CreateWeb3(operatorAccount);
            return new InProcessBundlerHost(node, operatorWeb3, chainId);
        }

        public IAccountAbstractionBundlerService StartBundler(
            string entryPointAddress,
            Web3Account bundlerAccount,
            bool enableErc7562Validation = false,
            Action<BundlerConfig> configureOverrides = null)
        {
            var bundlerWeb3 = Node.CreateWeb3(bundlerAccount);

            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { entryPointAddress },
                BeneficiaryAddress = bundlerAccount.Address,
                ChainId = ChainId,
                UnsafeMode = false,
                SimulateValidation = true,
                StrictValidation = true,
                EnableERC7562Validation = enableErc7562Validation,
                AutoBundleIntervalMs = 0
            };
            configureOverrides?.Invoke(config);

            BundlerService?.Dispose();
            BundlerService = new BundlerService(bundlerWeb3, config);
            Bundler = new BundlerServiceAdapter(BundlerService, ChainId);
            return Bundler;
        }

        public static IAccountAbstractionBundlerService UseHostedUrl(string bundlerRpcUrl, BigInteger chainId)
        {
            return new AccountAbstractionBundlerService(new RpcClient(new Uri(bundlerRpcUrl)));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            BundlerService?.Dispose();
            Node?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}
