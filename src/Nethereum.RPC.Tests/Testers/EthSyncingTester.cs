using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    
    public class EthSyncingTester : RPCRequestTester<SyncingOutput>, IRPCRequestTester
    {
        [Fact]
        public async void HighestBlockShouldBeBiggerThan0WhenSyncing()
        {
            var syncResult = await ExecuteAsync(ClientFactory.GetClient());
            if (syncResult.IsSyncing)
            {
                Assert.True(syncResult.HighestBlock.Value > 0);
            }
        }

        public override async Task<SyncingOutput> ExecuteAsync(IClient client)
        {
            var ethSyncing = new EthSyncing(client);
            return await ethSyncing.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof (EthSyncing);
        }
    }
}