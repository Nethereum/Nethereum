using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EcRecoverDirectTest
    {
        [Fact]
        public void TestEcRecoverWithKnownData()
        {
            // Test data from CALLCODEEcrecover0.json
            var hash = "18c547e4f7b0f325ad1e56f57e26c745b09a3e503d86e00e5255ff7f715d3d1c".HexToByteArray();
            byte v = 0x1c; // 28
            var r = "73b1693892219d736caba55bdb67216e485557ea6b6af75f37096c9aa6a5a75f".HexToByteArray();
            var s = "eeb940b1d03b21e36b0e47e79769f095fe2ab855bd91e3a38756b7d75a9c4549".HexToByteArray();
            
            var expectedAddress = "a94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            
            // Test via EthECKey directly first
            var signature = EthECDSASignatureFactory.FromComponents(r, s, new byte[] { v });
            var recoveredKey = EthECKey.RecoverFromSignature(signature, hash);
            var recoveredAddress = recoveredKey.GetPublicAddress().ToLower().Replace("0x", "");
            
            Assert.Equal(expectedAddress, recoveredAddress);
        }
        
        [Fact]
        public void TestEcRecoverPrecompile()
        {
            // Build 128-byte input in ECRECOVER format
            var hash = "18c547e4f7b0f325ad1e56f57e26c745b09a3e503d86e00e5255ff7f715d3d1c".HexToByteArray();
            byte v = 0x1c; // 28
            var r = "73b1693892219d736caba55bdb67216e485557ea6b6af75f37096c9aa6a5a75f".HexToByteArray();
            var s = "eeb940b1d03b21e36b0e47e79769f095fe2ab855bd91e3a38756b7d75a9c4549".HexToByteArray();
            
            // Build input: hash(32) + v_padded(32) + r(32) + s(32) = 128 bytes
            var input = new byte[128];
            System.Array.Copy(hash, 0, input, 0, 32);
            input[63] = v; // v is right-padded as big-endian in 32-byte field
            System.Array.Copy(r, 0, input, 64, 32);
            System.Array.Copy(s, 0, input, 96, 32);
            
            var precompile = new EvmPreCompiledContractsExecution();

            var result = precompile.EcRecover(input);
            
            var expectedAddress = "a94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            var resultAddress = result.ToHex().ToLower();
            
            // Result should be 32 bytes with address left-padded
            Assert.Equal(32, result.Length);
            Assert.EndsWith(expectedAddress, resultAddress);
        }
    }
}
