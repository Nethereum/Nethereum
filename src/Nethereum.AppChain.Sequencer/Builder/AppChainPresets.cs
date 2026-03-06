using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.Sequencer.Builder
{
    public static class AppChainPresets
    {
        public static AppChainBuilder ForGaming(string name, int chainId, string operatorPrivateKey)
        {
            return new AppChainBuilder(name, chainId)
                .WithOperator(operatorPrivateKey)
                .WithAA(PaymasterType.Sponsored)
                .WithTrust(TrustModel.Open)
                .WithOnDemandBlocks();
        }

        public static AppChainBuilder ForGaming(string name, int chainId, IAccount operatorAccount)
        {
            return new AppChainBuilder(name, chainId)
                .WithOperator(operatorAccount)
                .WithAA(PaymasterType.Sponsored)
                .WithTrust(TrustModel.Open)
                .WithOnDemandBlocks();
        }

        public static AppChainBuilder ForSocial(string name, int chainId, string operatorPrivateKey, int maxInvites = 3)
        {
            return new AppChainBuilder(name, chainId)
                .WithOperator(operatorPrivateKey)
                .WithAA(PaymasterType.Sponsored)
                .WithTrust(TrustModel.InviteTree, maxInvites)
                .WithOnDemandBlocks();
        }

        public static AppChainBuilder ForSocial(string name, int chainId, IAccount operatorAccount, int maxInvites = 3)
        {
            return new AppChainBuilder(name, chainId)
                .WithOperator(operatorAccount)
                .WithAA(PaymasterType.Sponsored)
                .WithTrust(TrustModel.InviteTree, maxInvites)
                .WithOnDemandBlocks();
        }

        public static AppChainBuilder ForEnterprise(string name, int chainId, string operatorPrivateKey, string admin)
        {
            return new AppChainBuilder(name, chainId)
                .WithOperator(operatorPrivateKey)
                .WithAA()
                .WithTrust(TrustModel.Whitelist, admin)
                .WithStorage(StorageType.InMemory)
                .WithOnDemandBlocks();
        }

        public static AppChainBuilder ForEnterprise(string name, int chainId, IAccount operatorAccount, string admin)
        {
            return new AppChainBuilder(name, chainId)
                .WithOperator(operatorAccount)
                .WithAA()
                .WithTrust(TrustModel.Whitelist, admin)
                .WithStorage(StorageType.InMemory)
                .WithOnDemandBlocks();
        }

        public static AppChainBuilder ForTesting(string name, int chainId, string operatorPrivateKey)
        {
            return new AppChainBuilder(name, chainId)
                .WithOperator(operatorPrivateKey)
                .WithTrust(TrustModel.Open)
                .WithStorage(StorageType.InMemory)
                .WithOnDemandBlocks()
                .WithBaseFee(0);
        }
    }
}
