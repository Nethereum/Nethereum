using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.RPC
{
    public class FeeSuggestionService : RpcClientWrapper
    {
        public FeeSuggestionService(IClient client) : base(client)
        {
        }

#if !DOTNET35

        public SimpleFeeSuggestionStrategy GetSimpleFeeSuggestionStrategy()
        {
            return new SimpleFeeSuggestionStrategy(Client);
        }

        public TimePreferenceFeeSuggestionStrategy GeTimePreferenceFeeSuggestionStrategy()
        {
            return new TimePreferenceFeeSuggestionStrategy(Client);
        }
#endif
    }
}