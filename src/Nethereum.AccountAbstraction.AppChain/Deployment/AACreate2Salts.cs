namespace Nethereum.AccountAbstraction.AppChain.Deployment
{
    public static class AACreate2Salts
    {
        public const string CREATE2_FACTORY =
            Nethereum.CoreChain.Forks.SystemContractPredeploys.Create2FactoryAddress;

        public static readonly byte[] ENTRYPOINT_SALT;
        public static readonly byte[] ACCOUNT_FACTORY_SALT;
        public static readonly byte[] ACCOUNT_IMPL_SALT;
        public static readonly byte[] ACCOUNT_REGISTRY_SALT;
        public static readonly byte[] SPONSORED_PAYMASTER_SALT;

        static AACreate2Salts()
        {
            ENTRYPOINT_SALT = CreateSalt("ENTRYPOINT_V0.9.0");
            ACCOUNT_FACTORY_SALT = CreateSalt("NETHEREUM_ACCOUNT_FACTORY_V1");
            ACCOUNT_IMPL_SALT = CreateSalt("NETHEREUM_ACCOUNT_IMPL_V1");
            ACCOUNT_REGISTRY_SALT = CreateSalt("APPCHAIN_ACCOUNT_REGISTRY_V1");
            SPONSORED_PAYMASTER_SALT = CreateSalt("APPCHAIN_SPONSORED_PAYMASTER_V1");
        }

        private static byte[] CreateSalt(string name)
        {
            var hash = new Nethereum.Util.Sha3Keccack().CalculateHash(System.Text.Encoding.UTF8.GetBytes(name));
            return hash;
        }
    }
}
