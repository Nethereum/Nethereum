using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.GSN.DTOs;
using Nethereum.GSN.Models;
using Nethereum.GSN.Policies;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    internal class RelayHubManager : IRelayHubManager
    {
        private readonly GSNOptions _options;
        private readonly IEthApiContractService _ethApiContractService;
        private readonly IRelayClient _relayClient;

        public RelayHubManager(
            GSNOptions options,
            IEthApiContractService ethApiContractService,
            IRelayClient relayClient)
        {
            _options = options;
            _ethApiContractService = ethApiContractService;
            _relayClient = relayClient;
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
                throw new Exception($"Contract does not support Gas Station Network");
            }

            var code = await _ethApiContractService.GetCode
                .SendRequestAsync(address)
                .ConfigureAwait(false);
            if (code.Length <= 2)
            {
                throw new Exception($"Relay hub is not deployed at address {address}");
            }

            var getHubVersion = new VersionFunction();
            var getHubVersionHandler = _ethApiContractService.GetContractQueryHandler<VersionFunction>();
            var hubVersion = await getHubVersionHandler
                .QueryAsync<string>(address, getHubVersion)
                .ConfigureAwait(false);

            if (!hubVersion.StartsWith("1"))
                throw new Exception($"Unsupported relay hub version {hubVersion}");

            return address;
        }

        public async Task<BigInteger> GetNonceAsync(string hubAddress, string from)
        {
            var getNonce = new GetNonceFunction() { From = from };
            var getNonceHandler = _ethApiContractService.GetContractQueryHandler<GetNonceFunction>();
            return await getNonceHandler.QueryAsync<BigInteger>(hubAddress, getNonce)
                .ConfigureAwait(false);
        }

        public async Task<RelayCollection> GetRelaysAsync(string hubAddress, IRelayPriorityPolicy policy)
        {
            var relays = await GetRelaysFromEvents(hubAddress);

            if (relays.Count == 0)
            {
                throw new Exception($"No relayers registered in the requested hub at {hubAddress}");
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

            var blockFrom = blockNow.Value - new BigInteger(_options.RelayLookupLimitBlocks);
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
