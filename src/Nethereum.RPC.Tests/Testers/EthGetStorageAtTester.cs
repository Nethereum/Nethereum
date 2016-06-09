using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Sample.Testers
{
    public class EthGetStorageAtTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethGetStorageAt = new EthGetStorageAt(client);
            return
                await
                    ethGetStorageAt.SendRequestAsync("0x407d73d8a49eeb85d32cf465507dd71d507100c1", new HexBigInteger(2));
        }

        public Type GetRequestType()
        {
            return typeof (EthGetStorageAt);
        }
    }
}