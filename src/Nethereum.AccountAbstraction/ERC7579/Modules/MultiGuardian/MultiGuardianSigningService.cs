using System.Collections.Generic;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian
{
    public sealed class MultiGuardianSigningService : IAccountSigningService
    {
        public IEthSignTypedDataV4 SignTypedDataV4 { get; }
        public IEthPersonalSign PersonalSign { get; }

        public MultiGuardianSigningService(IReadOnlyList<EthECKey> guardians, int threshold)
        {
            SignTypedDataV4 = new MultiGuardianSignTypedDataV4(guardians, threshold);
            PersonalSign = new MultiGuardianPersonalSign(guardians, threshold);
        }
    }
}
