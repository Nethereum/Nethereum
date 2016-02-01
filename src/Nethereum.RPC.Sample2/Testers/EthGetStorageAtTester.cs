
using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthGetStorageAtTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var ethGetStorageAt = new EthGetStorageAt();
            return await ethGetStorageAt.SendRequestAsync(client, "0x407d73d8a49eeb85d32cf465507dd71d507100c1", new HexBigInteger(2));
        }

        public Type GetRequestType()
        {
            return typeof(EthGetStorageAt);
        }
    }
}
        