using System.Diagnostics.Metrics;
using Nethereum.DevChain;

namespace Nethereum.DevChain.Server.Metrics
{
    public sealed class DevChainNodeMetrics
    {
        private readonly Meter _meter;

        public DevChainNodeMetrics(DevChainNode node)
        {
            _meter = new Meter("Nethereum.DevChain");

            _meter.CreateObservableGauge(
                "devchain.block.number",
                () => node.BlockManager.LastBlockProductionResult?.Header?.BlockNumber.ToLong() ?? 0,
                unit: "{block}",
                description: "Current block number");
        }
    }
}
