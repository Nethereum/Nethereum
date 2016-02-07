using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Web3
{
    public class Event
    {
        private RpcClient client;
        private EventABI eventABI;

        private Contract contract;
        private EthNewFilter ethNewFilter;
        private EventTopicBuilder eventTopicBuilder;


        public Task<HexBigInteger> CreateFilterAsync()
        {
            var ethFilterInput = new NewFilterInput();
            ethFilterInput.Address = new[] { contract.Address };
            ethFilterInput.Topics = new[] { eventTopicBuilder.GetSignaguteTopic()};
            return ethNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1)
        {
            var ethFilterInput = new NewFilterInput();
            ethFilterInput.Address = new[] { contract.Address };
            ethFilterInput.Topics = new[] { eventTopicBuilder.GetSignaguteTopic(), eventTopicBuilder.GetValueTopic(filterTopic1, 1) };
            return ethNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Task<HexBigInteger> CreateFilterAsync(object[] filterTopic1, object[] filterTopic2)
        {
            var ethFilterInput = new NewFilterInput();
            ethFilterInput.Address = new[] { contract.Address };
            ethFilterInput.Topics = new[] { eventTopicBuilder.GetSignaguteTopic(),
                eventTopicBuilder.GetValueTopic(filterTopic1, 1),
                eventTopicBuilder.GetValueTopic(filterTopic2, 2)
            };
            return ethNewFilter.SendRequestAsync(ethFilterInput);
        }

        public Event(RpcClient client, Contract contract, EventABI eventABI)
        {
            this.client = client;
            this.contract = contract;
            this.eventABI = eventABI;
            this.eventTopicBuilder = new EventTopicBuilder(eventABI);
            ethNewFilter = new EthNewFilter(client);

        }
    }
}