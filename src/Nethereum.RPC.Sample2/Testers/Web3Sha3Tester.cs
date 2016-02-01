using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Ethereum.RPC.Web3;

namespace Ethereum.RPC.Sample
{
    public class Web3Sha3Tester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var web3Sha3 = new Web3Sha3();
            return await web3Sha3.SendRequestAsync(client, "Monkey");
        }

        public Type GetRequestType()
        {
            return typeof(Web3Sha3Tester);
        }
    }
}