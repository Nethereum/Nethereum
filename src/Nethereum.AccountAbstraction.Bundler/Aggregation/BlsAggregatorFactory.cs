using System.Numerics;
using Nethereum.Signer.Bls;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Aggregation
{
    public static class BlsAggregatorFactory
    {
        public static IAggregatorManager CreateAggregatorManager(
            IBls bls,
            IWeb3 web3,
            BundlerConfig config,
            BigInteger chainId)
        {
            var manager = new AggregatorManager();

            if (config.EnableBlsAggregation && config.BlsAggregatorAddresses.Length > 0)
            {
                foreach (var entryPoint in config.SupportedEntryPoints)
                {
                    foreach (var aggregatorAddress in config.BlsAggregatorAddresses)
                    {
                        var aggregator = new BlsAggregator(
                            bls,
                            web3,
                            entryPoint,
                            aggregatorAddress,
                            chainId);

                        manager.RegisterAggregator(aggregatorAddress, aggregator);
                    }
                }
            }

            return manager;
        }

        public static BlsAggregator CreateBlsAggregator(
            IBls bls,
            IWeb3 web3,
            string entryPointAddress,
            string aggregatorAddress,
            BigInteger chainId)
        {
            return new BlsAggregator(bls, web3, entryPointAddress, aggregatorAddress, chainId);
        }
    }
}
