using Nethereum.Contracts.Services;
using Nethereum.GSN.DTOs;
using Nethereum.GSN.Exceptions;
using Nethereum.GSN.Interfaces;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public class RelayHubManager : IRelayHubManager
    {
        private readonly IEthApiContractService _ethApiContractService;
        private readonly IRelayQuery _relayQuery;

        public RelayHubManager(
            IEthApiContractService ethApiContractService,
            IRelayQuery relayQuery)
        {
            _ethApiContractService = ethApiContractService;
            _relayQuery = relayQuery;
        }

        public async Task<string> GetHubAddressAsync(string contractAddress)
        {
            var getHubAddr = new GetHubAddrFunction();
            var getHubAddrHandler = _ethApiContractService.GetContractQueryHandler<GetHubAddrFunction>();
            var address = await getHubAddrHandler
               .QueryAsync<string>(contractAddress, getHubAddr)
               .ConfigureAwait(false);
            if(address == null)
            {
                throw new GSNNotSupportedException();
            }

            var code = await _ethApiContractService.GetCode
                .SendRequestAsync(address)
                .ConfigureAwait(false);
            if (code.Length <= 2)
            {
                throw new GSNRelayHubNotFoundException(address);
            }

            var getHubVersion = new VersionFunction();
            var getHubVersionHandler = _ethApiContractService.GetContractQueryHandler<VersionFunction>();
            var hubVersion = await getHubVersionHandler
                .QueryAsync<string>(address, getHubVersion)
                .ConfigureAwait(false);

            if (!hubVersion.StartsWith("1"))
                throw new GSNException($"Unsupported relay hub version {hubVersion}");

            return address;
        }

        public async Task<BigInteger> GetNonceAsync(string hubAddress, string from)
        {
            var getNonce = new GetNonceFunction() { From = from };
            var getNonceHandler = _ethApiContractService.GetContractQueryHandler<GetNonceFunction>();
            return await getNonceHandler.QueryAsync<BigInteger>(hubAddress, getNonce)
                .ConfigureAwait(false);
        }

        public Task<RelayCollection> GetRelaysAsync(string hubAddress, IRelayPriorityPolicy policy)
        {
            return _relayQuery.GetAsync(hubAddress, policy);
        }
    }
}
