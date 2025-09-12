using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network
{
    public sealed class ChainManagementRegistrationOptions
    {
        public ChainFeatureStrategyType Strategy { get; set; } = ChainFeatureStrategyType.PreconfiguredEnrich;
        public IEnumerable<BigInteger>? DefaultChainIds { get; set; }
        public IEnumerable<ChainFeature>? PreconfiguredFeatures { get; set; }
        public bool EnableExternalChainList { get; set; } = true;
        public TimeSpan ChainListTtl { get; set; } = TimeSpan.FromMinutes(30);
        public Action<List<ChainFeature>>? PostProcessPreconfigured { get; set; }
    }
}