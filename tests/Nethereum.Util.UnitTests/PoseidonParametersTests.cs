using System.Numerics;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class PoseidonParametersTests
    {
        [Fact]
        public void DefaultBn254MatchesExpectedRoundsAndPrime()
        {
            var parameters = PoseidonParameters.CreateBn254();

            Assert.Equal(BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617"), parameters.Prime);
            Assert.Equal(3, parameters.StateWidth);
            Assert.Equal(2, parameters.Rate);
            Assert.Equal(1, parameters.Capacity);
            Assert.Equal(8, parameters.FullRounds);
            Assert.Equal(57, parameters.PartialRounds);
            Assert.Equal(5, parameters.SBoxExponent);
        }
    }
}
