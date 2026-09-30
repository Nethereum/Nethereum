using System;
using Nethereum.Documentation;
using Nethereum.Freezer;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerHeadFinalitySource — the freezer head as the finality signal")]
    public sealed class FreezerHeadFinalitySource : IFinalitySource
    {
        private readonly IFrozenReadSource _freezer;

        public FreezerHeadFinalitySource(IFrozenReadSource freezer)
        {
            _freezer = freezer ?? throw new ArgumentNullException(nameof(freezer));
        }

        public long FinalizedBlockNumber => _freezer.Items - 1;
    }
}
