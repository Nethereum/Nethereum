using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.Personal;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Web3.Accounts.Managed;
using Transaction = Nethereum.Signer.Transaction;

namespace Nethereum.Web3.Accounts
{
    public class ExternalAccount : IAccount
    {
        public IEthExternalSigner ExternalSigner { get; }
        public BigInteger? ChainId { get; }


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

        public async Task InitialiseAsync()
        {
            Address = await ExternalSigner.GetAddressAsync();
        }

        public void InitialiseDefaultTransactionManager(IClient client)
        {
            TransactionManager = new ExternalAccountSignerTransactionManager(client, this, ChainId);
        }

        public string Address { get; protected set; }
        public ITransactionManager TransactionManager { get; protected set; }
        public INonceService NonceService { get; set; }
    }
}