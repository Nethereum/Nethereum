using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.AccountSigning;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;

namespace Nethereum.Web3.Accounts
{
    public class ExternalAccount : IAccount
    {
        public ExternalAccount(IEthExternalSigner externalSigner, BigInteger? chainId = null)
        {
            ExternalSigner = externalSigner;
            ChainId = chainId;
        }

        public ExternalAccount(string address, IEthExternalSigner externalSigner, BigInteger? chainId = null)
        {
            ChainId = chainId;
            Address = address;
            ExternalSigner = externalSigner;
        }

        public IEthExternalSigner ExternalSigner { get; }
        public BigInteger? ChainId { get; }

        public string Address { get; protected set; }
        public ITransactionManager TransactionManager { get; protected set; }
        public INonceService NonceService { get; set; }

        public IAccountSigningService AccountSigningService { get; private set; }

        public async Task InitialiseAsync()
        {
            Address = await ExternalSigner.GetAddressAsync().ConfigureAwait(false);
            AccountSigningService = new AccountSigningExternalService(ExternalSigner);
        }

        public void InitialiseDefaultTransactionManager(IClient client)
        {
            TransactionManager = new ExternalAccountSignerTransactionManager(client, this, ChainId);
        }
    }
}