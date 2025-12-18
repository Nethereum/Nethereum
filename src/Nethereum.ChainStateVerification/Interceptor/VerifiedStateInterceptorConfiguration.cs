using System.Collections.Generic;

namespace Nethereum.ChainStateVerification.Interceptor
{
    public class VerifiedStateInterceptorConfiguration
    {
        public VerificationMode Mode { get; set; } = VerificationMode.Finalized;
        public bool FallbackOnError { get; set; } = true;
        public HashSet<string> EnabledMethods { get; set; } = new HashSet<string>(VerifiedStateInterceptor.DefaultEnabledMethods);
    }
}
