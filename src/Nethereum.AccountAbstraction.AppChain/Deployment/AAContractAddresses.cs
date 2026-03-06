namespace Nethereum.AccountAbstraction.AppChain.Deployment
{
    public static class AAContractAddresses
    {
        public const string CREATE2_FACTORY = "0x4e59b44847b379578588920cA78FbF26c0B4956C";

        public static readonly byte[] ENTRYPOINT_SALT;
        public static readonly byte[] ACCOUNT_FACTORY_SALT;
        public static readonly byte[] ACCOUNT_IMPL_SALT;
        public static readonly byte[] ACCOUNT_REGISTRY_SALT;
        public static readonly byte[] SPONSORED_PAYMASTER_SALT;

        static AAContractAddresses()
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
