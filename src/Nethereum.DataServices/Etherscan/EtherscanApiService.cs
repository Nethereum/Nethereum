using Nethereum.DataServices.Etherscan.Responses;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Etherscan
{
    public class EtherscanApiService
    {
        private EtherscanRequestService requestService;
        public EtherscanApiContractsService Contracts { get; private set; }
        public EtherscanApiAccountsService Accounts { get; private set; }
        public EtherscanApiService(HttpClient httpClient, string baseUrl, string apiKey = EtherscanRequestService.DefaultToken):this(new EtherscanRequestService(httpClient, baseUrl, apiKey))
        {

        }

        public EtherscanApiService(HttpClient httpClient, EtherscanChain chain, string apiKey = EtherscanRequestService.DefaultToken):
            this(new EtherscanRequestService(httpClient, chain, apiKey))
        {
            
        }

        public EtherscanApiService(EtherscanChain chain = EtherscanChain.Mainnet, string apiKey = EtherscanRequestService.DefaultToken)
            :this(new EtherscanRequestService(chain, apiKey))
        {
           
        }

        public EtherscanApiService(EtherscanRequestService etherscanRequestService)
        {
            requestService = etherscanRequestService;
            InitialiseServices();
        }

        private void InitialiseServices()
        {
            Contracts = new EtherscanApiContractsService(requestService);
            Accounts = new EtherscanApiAccountsService(requestService);
        }
    }
}
