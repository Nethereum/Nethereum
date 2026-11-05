using Nethereum.AccountAbstraction.Validation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.SecurityAudit71.S7.Round7
{
    public class S7R7ValidationDataTests
    {
        private const string Aggregator = "0x00000000000000000000000000000000000000aa";

        [Fact]
        [Trait("Rule", "S7-R7-03")]
        [Trait("Erc", "EntryPoint._validateAccountAndPaymasterValidationData")]
        public void Merge_WhenThePaymasterSignatureFailedAndTheAccountNamesAnAggregator_KeepsTheFailure()
        {
            var account = ValidationDataCodec.Pack(false, 0, 0, Aggregator);
            var paymaster = ValidationDataCodec.Pack(true, 0, 0, null);

            var merged = ValidationDataCodec.Merge(account, paymaster);

            var (sigFailed, _, _, _) = ValidationDataCodec.Parse(merged);
            Assert.True(sigFailed);
        }

        [Fact]
        [Trait("Rule", "S7-R7-03")]
        [Trait("Erc", "EntryPoint._validateAccountAndPaymasterValidationData")]
        public void Merge_WhenThePaymasterValidationDataNamesAnAggregator_IsAFailureAsTheEntryPointReadsIt()
        {
            var account = ValidationDataCodec.Pack(false, 0, 0, null);
            var paymaster = ValidationDataCodec.Pack(false, 0, 0, Aggregator);

            var merged = ValidationDataCodec.Merge(account, paymaster);

            var (sigFailed, _, _, aggregator) = ValidationDataCodec.Parse(merged);
            Assert.True(sigFailed || aggregator != null);
        }
    }
}
