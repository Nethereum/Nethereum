using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Rpc.Handlers.Debug;
using Nethereum.DevChain.Rpc.Handlers.Dev;

namespace Nethereum.DevChain.Rpc
{
    public static class DevRpcHandlerExtensions
    {
        public static RpcHandlerRegistry AddDevHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new EvmMineHandler());
            registry.Register(new EvmSnapshotHandler());
            registry.Register(new EvmRevertHandler());
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

            return registry;
        }

        public static RpcHandlerRegistry AddDebugHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new DebugTraceTransactionHandler());
            registry.Register(new DebugTraceCallHandler());

            return registry;
        }
    }
}
