using System.Numerics;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256BigIntegerConversionTests
    {
        private readonly ITestOutputHelper _output;

        public EvmUInt256BigIntegerConversionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ImplicitOperator_And_ToBigInteger_Match_ForSmallValues()
        {
            var values = new BigInteger[] { 0, 1, 42, 255, 65535, BigInteger.Pow(2, 64) - 1 };
            foreach (var val in values)
            {
                var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(val);
                BigInteger viaOperator = (BigInteger)evm;
                BigInteger viaExtension = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);
                Assert.Equal(viaExtension, viaOperator);
                Assert.Equal(val, viaOperator);
            }
        }

        [Fact]
        public void ImplicitOperator_And_ToBigInteger_Match_ForLargeValues()
        {
            var values = new[]
            {
                BigInteger.Pow(2, 128) - 1,
                BigInteger.Pow(2, 192) - 1,
                BigInteger.Pow(2, 254) - 1,
                BigInteger.Pow(2, 255) - 1,
                BigInteger.Pow(2, 256) - 1,
            };

            foreach (var val in values)
            {
                var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(val);
                BigInteger viaOperator = (BigInteger)evm;
                BigInteger viaExtension = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);

                if (viaOperator != viaExtension)
                {
                    _output.WriteLine($"MISMATCH for {val}:");
                    _output.WriteLine($"  operator:  {viaOperator}");
                    _output.WriteLine($"  extension: {viaExtension}");
                    _output.WriteLine($"  evm: u3={evm.U3:X16} u2={evm.U2:X16} u1={evm.U1:X16} u0={evm.U0:X16}");
                }
                Assert.Equal(viaExtension, viaOperator);
                Assert.Equal(val, viaOperator);
            }
        }

        [Fact]
        public void ImplicitOperator_And_ToBigInteger_Match_ForBN254Prime()
        {
            var prime = BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");
            var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(prime);
            BigInteger viaOperator = (BigInteger)evm;
            BigInteger viaExtension = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);

            _output.WriteLine($"prime:     {prime}");
            _output.WriteLine($"operator:  {viaOperator}");
            _output.WriteLine($"extension: {viaExtension}");
            Assert.Equal(prime, viaOperator);
            Assert.Equal(prime, viaExtension);
        }

        [Fact]
        public void ImplicitOperator_And_ToBigInteger_Match_ForPoseidonRoundConstants()
        {
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT2);
            var totalRounds = parameters.FullRounds + parameters.PartialRounds;
            int mismatches = 0;

            for (int r = 0; r < totalRounds; r++)
            {
                for (int c = 0; c < parameters.StateWidth; c++)
                {
                    var original = parameters.RoundConstants[r, c];
                    var evm = EvmUInt256BigIntegerExtensions.FromBigInteger(original);
                    BigInteger viaOperator = (BigInteger)evm;
                    BigInteger viaExtension = EvmUInt256BigIntegerExtensions.ToBigInteger(evm);

                    if (viaOperator != viaExtension)
                    {
                        if (mismatches == 0)
                        {
                            _output.WriteLine($"MISMATCH at [{r},{c}]:");
                            _output.WriteLine($"  original:  {original}");
                            _output.WriteLine($"  operator:  {viaOperator}");
                            _output.WriteLine($"  extension: {viaExtension}");
                            _output.WriteLine($"  evm: u3={evm.U3:X16} u2={evm.U2:X16} u1={evm.U1:X16} u0={evm.U0:X16}");
                        }
                        mismatches++;
                    }

                    if (original != viaExtension)
                    {
                        if (mismatches == 0)
                        {
                            _output.WriteLine($"EXTENSION MISMATCH at [{r},{c}]:");
                            _output.WriteLine($"  original:  {original}");
                            _output.WriteLine($"  extension: {viaExtension}");
                        }
                        mismatches++;
                    }
                }
            }

            _output.WriteLine($"Checked {totalRounds * parameters.StateWidth} constants, {mismatches} mismatches");
            Assert.Equal(0, mismatches);
        }

        [Fact]
        public void Constructor_And_FromBigInteger_Match()
        {
            var values = new[]
            {
                BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617"),
                BigInteger.Parse("21888242871839275222246405745257275088696311157297823662689037894645226208583"),
                BigInteger.Pow(2, 254) - 19,
                BigInteger.Pow(2, 128) + 7,
                BigInteger.One,
                BigInteger.Zero,
            };

            foreach (var val in values)
            {
                var viaConstructor = new EvmUInt256(val);
                var viaExtension = EvmUInt256BigIntegerExtensions.FromBigInteger(val);

                if (viaConstructor != viaExtension)
                {
                    _output.WriteLine($"MISMATCH for {val}:");
                    _output.WriteLine($"  ctor: u3={viaConstructor.U3:X16} u2={viaConstructor.U2:X16} u1={viaConstructor.U1:X16} u0={viaConstructor.U0:X16}");
                    _output.WriteLine($"  ext:  u3={viaExtension.U3:X16} u2={viaExtension.U2:X16} u1={viaExtension.U1:X16} u0={viaExtension.U0:X16}");
                }
                Assert.Equal(viaExtension, viaConstructor);
            }
        }

        [Fact]
        public void FullRoundTrip_AllPaths_Match()
        {
            var val = BigInteger.Parse("21888242871839275222246405745257275088696311157297823662689037894645226208583");

            var viaConstructor = new EvmUInt256(val);
            var viaFromBigInt = EvmUInt256BigIntegerExtensions.FromBigInteger(val);
            Assert.Equal(viaFromBigInt, viaConstructor);

            BigInteger backViaOperator = (BigInteger)viaFromBigInt;
            BigInteger backViaExtension = EvmUInt256BigIntegerExtensions.ToBigInteger(viaFromBigInt);

            _output.WriteLine($"original:      {val}");
            _output.WriteLine($"via operator:  {backViaOperator}");
            _output.WriteLine($"via extension: {backViaExtension}");
            _output.WriteLine($"evm: u3={viaFromBigInt.U3:X16} u2={viaFromBigInt.U2:X16} u1={viaFromBigInt.U1:X16} u0={viaFromBigInt.U0:X16}");

            Assert.Equal(val, backViaOperator);
            Assert.Equal(val, backViaExtension);
        }
    }
}
