using System;
using Nethereum.Documentation;
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;
using Xunit;

namespace Nethereum.Signer.Bls.Tests
{
    [Collection("HerumiNative")]
    public class FoundationsBlsDocExampleTests
    {
        private static void SkipIfNativeUnavailable()
        {
            try
            {
                new Bls12381Operations().TryG1Add(new byte[128], new byte[128], out _);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is BadImageFormatException || ex is TypeInitializationException)
            {
                Skip.If(true, "Herumi native library is not available: " + ex.Message);
            }
        }

        [SkippableFact]
        [NethereumDocExample(DocSection.Consensus, "bls12381-outcome-operations", "Invalid EIP-2537 input is a false return, not an exception")]
        public void Given_InvalidAndValidEip2537Input_When_TryG1AddIsCalled_Then_InvalidInputReturnsFalseAndTheFacadeThrows()
        {
            SkipIfNativeUnavailable();
            IBls12381OutcomeOperations operations = new Bls12381Operations();

            var infinity = new byte[128];
            var accepted = operations.TryG1Add(infinity, infinity, out var sum);
            var rejected = operations.TryG1Add(new byte[3], infinity, out var none);

            Assert.True(accepted);
            Assert.Equal(new byte[128], sum);
            Assert.False(rejected);
            Assert.Null(none);
            Assert.Throws<ArgumentException>(() => new Bls12381Operations().G1Add(new byte[3], infinity));
        }

        [SkippableFact]
        [NethereumDocExample(DocSection.Consensus, "bls12381-outcome-pairing", "TryPairing reports malformed input as false")]
        public void Given_MismatchedPairingInputs_When_TryPairingIsCalled_Then_FalseIsReturnedAndAnEmptyInputPairs()
        {
            SkipIfNativeUnavailable();
            IBls12381OutcomeOperations operations = new Bls12381Operations();

            Assert.True(operations.TryPairing(new byte[0][], new byte[0][], out var emptyPairs));
            Assert.True(emptyPairs);
            Assert.False(operations.TryPairing(new[] { new byte[128] }, new byte[0][], out _));
        }
    }
}
