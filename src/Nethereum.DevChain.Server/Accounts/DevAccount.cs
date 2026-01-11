using System.Numerics;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;

namespace Nethereum.DevChain.Server.Accounts
{
    public class DevAccount
    {
        public int Index { get; }
        public string Address { get; }
        public byte[] PrivateKey { get; }
        public BigInteger Balance { get; set; }
        public Account Account { get; }

        public DevAccount(int index, EthECKey key, BigInteger chainId, BigInteger initialBalance)
        {
            Index = index;
            Address = key.GetPublicAddress();
            PrivateKey = key.GetPrivateKeyAsBytes();
            Balance = initialBalance;
            Account = new Account(key, chainId);
        }

        public string GetPrivateKeyHex()
        {
            return "0x" + BitConverter.ToString(PrivateKey).Replace("-", "").ToLowerInvariant();
        }
    }
}
