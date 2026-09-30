using System.Numerics;
using Nethereum.RPC.Chain;
using Xunit;

namespace Nethereum.RPC.UnitTests
{
    public class NativeCurrencyByChainIdTests
    {
        [Fact]
        public void Given_AKnownChainId_When_ItsChainFeatureIsLookedUp_Then_ItCarriesTheNativeCurrency()
        {
            var mainnet = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(new BigInteger(1));

            Assert.NotNull(mainnet);
            Assert.NotNull(mainnet.NativeCurrency);
            Assert.Equal("ETH", mainnet.NativeCurrency.Symbol);
            Assert.Equal(18, mainnet.NativeCurrency.Decimals);
        }

        [Fact]
        public void Given_AnUnknownChainId_When_LookedUp_Then_ItIsNullRatherThanAssumedEther()
        {
            Assert.Null(ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(new BigInteger(999999999)));
        }

        [Fact]
        public void Given_AChainWhoseNativeAssetIsNotEther_When_LookedUp_Then_ItsOwnSymbolIsReturned()
        {
            var features = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeatures();
            var nonEther = features.Find(c => c.NativeCurrency != null && c.NativeCurrency.Symbol != "ETH");

            Assert.NotNull(nonEther);

            var byId = ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(nonEther.ChainId);
            Assert.Equal(nonEther.NativeCurrency.Symbol, byId.NativeCurrency.Symbol);
        }
    }
}
