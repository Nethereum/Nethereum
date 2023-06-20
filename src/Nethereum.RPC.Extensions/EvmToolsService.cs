using Nethereum.RPC;
using Nethereum.RPC.Extensions.DevTools.Evm;

namespace Nethereum.RPC.Extensions
{
    public class EvmToolsService
    {
        public EvmSetNextBlockTimestamp SetNextBlockTimestamp { get; private set; }
        public EvmToolsService(IEthApiService ethApiService)
        {
            EthApiService = ethApiService;
            SetNextBlockTimestamp = new EvmSetNextBlockTimestamp(EthApiService.Client);
        }
        public IEthApiService EthApiService { get; }
    }


}