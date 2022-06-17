using System.Threading.Tasks;

using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.Standards.ProofOfHumanity.ContractDefinition;
using System.Collections.Generic;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts;
using System.Linq;
using System.Numerics;

namespace Nethereum.Contracts.Standards.ProofOfHumanity
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
            var results = await multiqueryHandler.MultiCallAsync(block, numberOfCallsPerRequest, registeredCalls.ToArray());
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
            var eventLogs = await GetEvidenceLogsAsync(party, fromBlock, toBlock);
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
#endif
    }
}
