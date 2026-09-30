using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Rpc.Handlers;
using Nethereum.DevChain.Rpc.Handlers.Dev;

namespace Nethereum.DevChain.Rpc
{
    public static class DevRpcHandlerExtensions
    {
        public static RpcHandlerRegistry CreateDevChainRegistry()
        {
            var registry = new RpcHandlerRegistry();

            registry.AddStandardHandlers();
            registry.AddDevHandlers();
            registry.AddAnvilAliases();

            registry.Override(new EthAccountsHandler());
            registry.Register(new HardhatImpersonateAccountHandler());
            registry.Register(new HardhatStopImpersonatingAccountHandler());

            return registry;
        }

        public static RpcHandlerRegistry AddDevHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new EvmMineHandler());
            registry.Register(new EvmSnapshotHandler());
            registry.Register(new EvmRevertHandler());
            registry.Register(new DebugSetHeadHandler());
            registry.Register(new HardhatSetBalanceHandler());
            registry.Register(new HardhatSetCodeHandler());
            registry.Register(new HardhatSetStorageAtHandler());
            registry.Register(new HardhatSetNonceHandler());

            // Time manipulation handlers
            registry.Register(new EvmIncreaseTimeHandler());
            registry.Register(new EvmSetNextBlockTimestampHandler());

            return registry;
        }

        public static RpcHandlerRegistry AddAnvilAliases(this RpcHandlerRegistry registry)
        {
            registry.RegisterAlias("anvil_setBalance", "hardhat_setBalance");
            registry.RegisterAlias("anvil_setCode", "hardhat_setCode");
            registry.RegisterAlias("anvil_setNonce", "hardhat_setNonce");
            registry.RegisterAlias("anvil_setStorageAt", "hardhat_setStorageAt");
            registry.RegisterAlias("anvil_mine", "evm_mine");
            registry.RegisterAlias("anvil_snapshot", "evm_snapshot");
            registry.RegisterAlias("anvil_revert", "evm_revert");
            registry.RegisterAlias("anvil_impersonateAccount", "hardhat_impersonateAccount");
            registry.RegisterAlias("anvil_stopImpersonatingAccount", "hardhat_stopImpersonatingAccount");

            return registry;
        }

    }
}
