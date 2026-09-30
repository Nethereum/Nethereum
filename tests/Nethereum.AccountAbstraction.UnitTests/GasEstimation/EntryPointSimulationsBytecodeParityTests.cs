using System;
using System.Security.Cryptography;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPointSimulations;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.GasEstimation
{
    public class EntryPointSimulationsBytecodeParityTests
    {
        private const int ExpectedByteLength = 23681;
        private const string ExpectedSha256 =
            "1ff140957c64aa903b6d387e297824845d87cb7846087c67fec27f7fe30e52da";

        private const string Solc0828Marker = "64736f6c634300081c";

        private static byte[] ToBytes(string hex)
        {
            var s = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex.Substring(2) : hex;
            var bytes = new byte[s.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return bytes;
        }

        [Fact]
        public void V09_IsWellFormedRuntimeBytecode()
        {
            var hex = EntryPointSimulationsRuntimeBytecode.V09;
            Assert.StartsWith("0x", hex);
            Assert.True((hex.Length - 2) % 2 == 0, "runtime bytecode must be whole bytes");
            var bytes = ToBytes(hex);
            Assert.Equal(ExpectedByteLength, bytes.Length);
        }

        [Fact]
        public void V09_ContentMatchesPinnedDeployedEntryPointCompile()
        {
            var bytes = ToBytes(EntryPointSimulationsRuntimeBytecode.V09);
            using var sha = SHA256.Create();
            var actual = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
            Assert.Equal(ExpectedSha256, actual);
        }

        [Fact]
        public void V09_AndDeployedEntryPoint_ShareSolcCompilerVersion()
        {
            Assert.Contains(Solc0828Marker, EntryPointSimulationsRuntimeBytecode.V09);
            Assert.Contains(Solc0828Marker, EntryPointDeploymentBase.BYTECODE);
        }
    }
}
