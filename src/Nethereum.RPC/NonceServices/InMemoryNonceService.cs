using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.NonceServices
{
#if !DOTNET35
    public class InMemoryNonceService: INonceService
    {
        public BigInteger CurrentNonce { get; set; } = -1;
        public IClient Client { get; set; }
        private readonly string _account;
        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1,1);

        public InMemoryNonceService(string account, IClient client)
        {
            Client = client;
            _account = account;
        }

        public async Task<HexBigInteger> GetNextNonceAsync()
        {

            if (Client == null) throw new NullReferenceException("Client not configured");
            var ethGetTransactionCount = new EthGetTransactionCount(Client);
            await _semaphoreSlim.WaitAsync();
            try
            {
                var nonce = await ethGetTransactionCount.SendRequestAsync(_account, BlockParameter.CreatePending())
                    .ConfigureAwait(false);
                if (nonce.Value <= CurrentNonce)
                {
                    CurrentNonce = CurrentNonce + 1;
                    nonce = new HexBigInteger(CurrentNonce);
                }
                else
                {
                    CurrentNonce = nonce.Value;
                }
                return nonce;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
    }
#endif
}