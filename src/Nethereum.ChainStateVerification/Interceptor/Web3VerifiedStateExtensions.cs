using System;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;

namespace Nethereum.ChainStateVerification.Interceptor
{
    public static class Web3VerifiedStateExtensions
    {
        public static IWeb3 UseVerifiedState(
            this IWeb3 web3,
            IVerifiedStateService verifiedStateService,
            Action<VerifiedStateInterceptorConfiguration> configure = null)
        {
            web3.Client.UseVerifiedState(verifiedStateService, configure);
            return web3;
        }

        public static TWeb3 UseVerifiedState<TWeb3>(
            this TWeb3 web3,
            IVerifiedStateService verifiedStateService,
            Action<VerifiedStateInterceptorConfiguration> configure = null)
            where TWeb3 : IWeb3
        {
            web3.Client.UseVerifiedState(verifiedStateService, configure);
            return web3;
        }

        public static IClient UseVerifiedState(
            this IClient client,
            IVerifiedStateService verifiedStateService,
            Action<VerifiedStateInterceptorConfiguration> configure = null)
        {
            var config = new VerifiedStateInterceptorConfiguration();
            configure?.Invoke(config);

            verifiedStateService.Mode = config.Mode;

            var interceptor = new VerifiedStateInterceptor(verifiedStateService, config.EnabledMethods)
            {
                FallbackOnError = config.FallbackOnError
            };

            client.OverridingRequestInterceptor = interceptor;
            return client;
        }

        public static VerifiedStateInterceptor CreateVerifiedStateInterceptor(
            this IVerifiedStateService verifiedStateService,
            Action<VerifiedStateInterceptorConfiguration> configure = null)
        {
            var config = new VerifiedStateInterceptorConfiguration();
            configure?.Invoke(config);

            verifiedStateService.Mode = config.Mode;

            return new VerifiedStateInterceptor(verifiedStateService, config.EnabledMethods)
            {
                FallbackOnError = config.FallbackOnError
            };
        }
    }
}
