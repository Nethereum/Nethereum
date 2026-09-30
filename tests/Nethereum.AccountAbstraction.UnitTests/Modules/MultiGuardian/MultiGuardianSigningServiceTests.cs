using System;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.MultiGuardian
{
    public class MultiGuardianSigningServiceTests
    {
        [Fact]
        public void Constructor_WiresBothMultiGuardianSigners()
        {
            var guardians = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey() };

            var service = new MultiGuardianSigningService(guardians, 2);

            Assert.IsType<MultiGuardianSignTypedDataV4>(service.SignTypedDataV4);
            Assert.IsType<MultiGuardianPersonalSign>(service.PersonalSign);
        }

        [Fact]
        public void Constructor_NullGuardians_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MultiGuardianSigningService(null!, 1));
        }

        [Fact]
        public void Constructor_FewerGuardiansThanThreshold_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new MultiGuardianSigningService(new[] { EthECKey.GenerateKey() }, 2));
        }
    }
}
