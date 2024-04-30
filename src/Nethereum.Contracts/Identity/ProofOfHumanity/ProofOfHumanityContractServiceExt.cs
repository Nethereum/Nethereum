using System.Threading.Tasks;

using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.Identity.ProofOfHumanity.ContractDefinition;
using System.Collections.Generic;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Constants;
using System.Linq;
using System.Numerics;
#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP2_1_OR_GREATER
using System.Net.Http;
#endif
using System;
using Newtonsoft.Json;
using Nethereum.Contracts.Identity.ProofOfHumanity.Model;
using Nethereum.Contracts.Identity.ProofOfHumanity;


namespace Nethereum.Contracts.Identity.ProofOfHumanity
{
    public partial class ProofOfHumanityContractService
    {
#if !DOTNET35
        public async Task<List<IsRegisteredInfo>> AreRegisteredQueryUsingMulticallAsync(IEnumerable<string> registeredAddresses,
          BlockParameter block = null,
          int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST,
          string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var registeredCalls = new List<MulticallInputOutput<IsRegisteredFunction, IsRegisteredOutputDTO>>();
            foreach (var registeredAddress in registeredAddresses)
            {
             
                    var registeredCall  = new IsRegisteredFunction() { SubmissionID = registeredAddress };
                    registeredCalls.Add(new MulticallInputOutput<IsRegisteredFunction, IsRegisteredOutputDTO>(registeredCall,
                    ContractAddress));

            }

            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(block, numberOfCallsPerRequest, registeredCalls.ToArray()).ConfigureAwait(false);
            return registeredCalls.Select(x => new IsRegisteredInfo()
            {
                IsRegistered = x.Output.IsRegistered,
                Address = x.Input.SubmissionID,
            }).ToList();
        }

        public Task<List<EventLog<EvidenceEventDTO>>> GetEvidenceLogsAsync(string party, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventDTO = ContractHandler.GetEvent<EvidenceEventDTO>();
            var filterInput = eventDTO.GetFilterBuilder().AddTopic(x => x.Party, party).Build(ContractAddress, fromBlock, toBlock);
            return eventDTO.GetAllChangesAsync(filterInput);
        }

        public async Task<EventLog<EvidenceEventDTO>> GetLatestEvidenceLogAsync(string party, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventLogs = await GetEvidenceLogsAsync(party, fromBlock, toBlock).ConfigureAwait(false);
            if (eventLogs != null && eventLogs.Count > 0)
            {
                if (eventLogs.Count > 1)
                {
                    eventLogs.SortLogs();
                    return eventLogs.Last();
                }

                return eventLogs[0];
            }
            return null;
        }

        public Task<List<EventLog<EvidenceEventDTO>>> GetEvidenceLogsAsync(string arbitrator, BigInteger evidenceGroupId, string party, BlockParameter fromBlock = null, BlockParameter toBlock = null)
        {
            var eventDTO = ContractHandler.GetEvent<EvidenceEventDTO>();
            var filterInput = eventDTO.GetFilterBuilder().AddTopic(x => x.Arbitrator, arbitrator).AddTopic(x => x.EvidenceGroupID, evidenceGroupId).AddTopic(x => x.Party, party).Build(ContractAddress, fromBlock, toBlock);
            return eventDTO.GetAllChangesAsync(filterInput);
        }
#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public Task<Registration> GetRegistrationFromIpfs(EvidenceEventDTO evidenceEvent, string ipfsGateway = "https://gateway.ipfs.io/")
        {
            return GetRegistrationFromIpfs(evidenceEvent.Evidence, ipfsGateway);
        }

        public Task<Registration> GetRegistrationFromIpfs(string evidencePath, string ipfsGateway = "https://gateway.ipfs.io/")
        {
            return GetJsonObjectFromIpfsGateway<Registration>(evidencePath, false, ipfsGateway);
        }

        public Task<RegistrationEvidence> GetRegistrationEvidenceFromIpfs(string registrationEvidencePath, string ipfsGateway = "https://gateway.ipfs.io/")
        {
            return GetJsonObjectFromIpfsGateway<RegistrationEvidence>(registrationEvidencePath, false, ipfsGateway);
        }

        public Task<RegistrationEvidence> GetRegistrationEvidenceFromIpfs(Registration registration, string ipfsGateway = "https://gateway.ipfs.io/")
        {
            return GetRegistrationEvidenceFromIpfs(registration.FileUri, ipfsGateway);
        }

        public async Task<RegistrationEvidence> GetRegistrationEvidenceFromIpfs(EvidenceEventDTO evidenceEventDTO, string ipfsGateway = "https://gateway.ipfs.io/")
        {
            var registration = await GetRegistrationFromIpfs(evidenceEventDTO, ipfsGateway).ConfigureAwait(false);
            return await GetRegistrationEvidenceFromIpfs(registration, ipfsGateway).ConfigureAwait(false);
        }

        internal async Task<T> GetJsonObjectFromIpfsGateway<T>(string relativePath, bool addIpfsSuffix = true, string ipfsGateway = "https://gateway.ipfs.io/")
        {
            var uri = new Uri(ipfsGateway);
            if (addIpfsSuffix) uri = new Uri(uri, "ipfs");
            var fullUri = new Uri(uri, relativePath);
            using (var client = new HttpClient())
            {
                var json = await client.GetStringAsync(fullUri).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<T>(json);
            }
        }
#endif

#endif
    }
}
