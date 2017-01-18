using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.Interceptors;

namespace Nethereum.Quorum
{
    public class Web3Quorum:Web3.Web3
    {
        public Web3Quorum(IClient client):base(client)
        {
           
        }

        public Web3Quorum(string url = @"http://localhost:8545/"):base(url)
        {

        }

        public List<string> PrivateFor { get; private set; }
        public string PrivateFrom { get; private set; }

        public void SetPrivateRequestsParamaters(List<string> privateFor, string privateFrom = null)
        {
            this.PrivateFor = privateFor;
            this.PrivateFrom = privateFrom;
            this.Client.OverridingRequestInterceptor = new PrivateForInterceptor(privateFor, privateFrom);
        }
    }
}
