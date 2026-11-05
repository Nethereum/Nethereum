using System;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperDeferredCodeTests
    {
        [Fact]
        public void SnapRootMismatchException_CarriesDeferredCodeHashes()
        {
            var codeHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0x60, 0x00, 0xf3 });
            var ex = new SnapSyncClient.SnapRootMismatchException(
                "mismatch",
                Array.Empty<SnapSyncClient.AccountNeedingHeal>(),
                new[] { codeHash });
            Assert.Single(ex.CodeHashesNeedingHeal);
            Assert.Equal(codeHash, ex.CodeHashesNeedingHeal[0]);
        }
    }
}
