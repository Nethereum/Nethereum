using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.Interceptors;
using Nethereum.Quorum.RPC.Services;

namespace Nethereum.Quorum
{
    public class Web3Quorum:Web3.Web3
    {
        public Web3Quorum(IClient client):base(client)
        {
           
        }

        public Web3Quorum(string url = @"http://localhost:8545/", ILog log = null, AuthenticationHeaderValue authenticationHeader = null) : base(url, log, authenticationHeader)
        {

        }

        protected override void InitialiseInnerServices()
        {
            base.InitialiseInnerServices();
            Quorum = new QuorumChainService(Client);
            base.TransactionManager.DefaultGasPrice = 0;
        }

        public QuorumChainService Quorum { get; private set; }

        public List<string> PrivateFor { get; private set; }
        public string PrivateFrom { get; private set; }

        public void SetPrivateRequestParameters(List<string> privateFor, string privateFrom = null)
        {
            if(privateFor == null || privateFor.Count == 0) throw new ArgumentNullException(nameof(privateFor));
            this.PrivateFor = privateFor;
            this.PrivateFrom = privateFrom;
            this.Client.OverridingRequestInterceptor = new PrivateForInterceptor(privateFor, privateFrom);
        }

        public void ClearPrivateForRequestParameters()
        {
            if (Client.OverridingRequestInterceptor is PrivateForInterceptor)
            {
                Client.OverridingRequestInterceptor = null;
            }

            PrivateFor = null;
            PrivateFrom = null;
        }

       
    }
}
