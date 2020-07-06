using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.GSN.DTOs;
using Nethereum.GSN.Exceptions;
using Nethereum.GSN.Interfaces;
using Nethereum.GSN.Models;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN.Queries
{
    public class RelayQuery : IRelayQuery
    {
        private readonly int _lookupLimitBlocks;
        private readonly IEthApiContractService _ethApiContractService;
        private readonly IRelayClient _relayClient;

        public RelayQuery(
            int lookupLimitBlocks,
            IEthApiContractService ethApiContractService,
            IRelayClient relayClient)
        {
            _lookupLimitBlocks = lookupLimitBlocks;
            _ethApiContractService = ethApiContractService;
            _relayClient = relayClient;
        }

        public async Task<RelayCollection> GetAsync(string hubAddress, IRelayPriorityPolicy policy)
        {
            var relays = await GetRelaysFromEvents(hubAddress);

            if (relays.Count == 0)
            {
                throw new GSNNoRegisteredRelaysException(hubAddress);
            }

            // TODO: filter relays by minDelay and minStake
            return new RelayCollection(_relayClient, policy.Execute(relays));
        }

        private async Task<IList<RelayOnChain>> GetRelaysFromEvents(string hubAddress)
        {
            var blockFrom = await GetFromBlock();

            var relayAddedEvents = await GetEvents<RelayAddedEvent>(hubAddress, blockFrom)
                .ConfigureAwait(false);

            var relayRemovedEvents = await GetEvents<RelayRemovedEvent>(hubAddress, blockFrom)
                .ConfigureAwait(false);

            var events = relayAddedEvents
                .Select(RelayEvent.FromEventLog)
                .Union(relayRemovedEvents.Select(RelayEvent.FromEventLog))
                .OrderBy(x => x.Block)
                .Distinct(new RelayEventComparer());

            var relays = new List<RelayOnChain>();
            foreach (var ev in events)
            {
                if (ev.Type == RelayEventType.Added)
                {
                    relays.Add(new RelayOnChain
                    {
                        Address = ev.Address,
                        Url = ev.Url,
                        Fee = ev.Fee,
                        Stake = ev.Stake,
                        UnstakeDelay = ev.UnstakeDelay
                    });
                }
                else
                {
                    var relay = relays.FirstOrDefault(x => x.Address == ev.Address);
                    if (relay != null)
                    {
                        relays.Remove(relay);
                    }
                }
            }

            return relays;
        }

        private async Task<HexBigInteger> GetFromBlock()
        {
            var blockNow = await _ethApiContractService.Blocks.GetBlockNumber
                .SendRequestAsync()
                .ConfigureAwait(false);

            var blockFrom = blockNow.Value - new BigInteger(_lookupLimitBlocks);
            if (blockFrom.CompareTo(1) == -1)
            {
                blockFrom = new BigInteger(1);
            }

            return new HexBigInteger(blockFrom);
        }

        private Task<List<EventLog<T>>> GetEvents<T>(string hubAddress, HexBigInteger fromBlock)
            where T : IEventDTO, new()
        {
            var eventHandler = _ethApiContractService.GetEvent<T>(hubAddress);
            var filterInput = eventHandler.CreateFilterInput(fromBlock: new BlockParameter(fromBlock));
            return eventHandler.GetAllChanges(filterInput);
        }
    }
}
