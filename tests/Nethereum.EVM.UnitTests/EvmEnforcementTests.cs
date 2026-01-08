using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmEnforcementTests
    {
        [Fact]
        public void OutOfGas_ShouldThrow_WhenGasExhausted()
        {
            var program = new Program("60016001".HexToByteArray());
            program.GasRemaining = 1;

            var ex = Assert.Throws<OutOfGasException>(() => program.UpdateGasUsed(5));
            Assert.Equal(5, ex.GasRequired);
            Assert.Equal(1, ex.GasRemaining);
        }

        [Fact]
        public void OutOfGas_ShouldNotThrow_WhenGasSufficient()
        {
            var program = new Program("60016001".HexToByteArray());
            program.GasRemaining = 100;

            program.UpdateGasUsed(50);

            Assert.Equal(50, program.GasRemaining);
            Assert.Equal(50, program.TotalGasUsed);
        }

        [Fact]
        public void StaticCallViolation_ShouldIncludeOperation()
        {
            var ex = new StaticCallViolationException("SSTORE");
            Assert.Equal("SSTORE", ex.Operation);
            Assert.Contains("SSTORE", ex.Message);
        }
    }

    public class EvmSnapshotTests
    {
        [Fact]
        public void TakeSnapshot_ShouldReturnIncrementingIds()
        {
            var service = new ExecutionStateService(new MockNodeDataService());

            var id1 = service.TakeSnapshot();
            var id2 = service.TakeSnapshot();
            var id3 = service.TakeSnapshot();

            Assert.Equal(0, id1);
            Assert.Equal(1, id2);
            Assert.Equal(2, id3);
        }

        [Fact]
        public async Task RevertToSnapshot_ShouldRestoreStorageValues()
        {
            var service = new ExecutionStateService(new MockNodeDataService());
            var address = "0x1234567890123456789012345678901234567890";

            service.SaveToStorage(address, 1, new byte[] { 0x42 });

            var snapshotId = service.TakeSnapshot();

            service.SaveToStorage(address, 1, new byte[] { 0xFF });
            service.SaveToStorage(address, 2, new byte[] { 0xAB });

            service.RevertToSnapshot(snapshotId);

            var value1 = await service.GetFromStorageAsync(address, 1);
            var state = service.CreateOrGetAccountExecutionState(address);

            Assert.Equal(0x42, value1[0]);
            Assert.False(state.StorageContainsKey(2));
        }

        [Fact]
        public void RevertToSnapshot_ShouldRestoreBalance()
        {
            var service = new ExecutionStateService(new MockNodeDataService());
            var address = "0x1234567890123456789012345678901234567890";

            service.SetInitialChainBalance(address, 1000);
            service.UpsertInternalBalance(address, 500);

            var snapshotId = service.TakeSnapshot();

            service.UpsertInternalBalance(address, -300);

            service.RevertToSnapshot(snapshotId);

            var state = service.CreateOrGetAccountExecutionState(address);
            Assert.Equal(1500, state.Balance.GetTotalBalance());
        }

        [Fact]
        public void RevertToSnapshot_ShouldRestoreNonce()
        {
            var service = new ExecutionStateService(new MockNodeDataService());
            var address = "0x1234567890123456789012345678901234567890";

            service.SetNonce(address, 5);
            var snapshotId = service.TakeSnapshot();
            service.SetNonce(address, 10);

            service.RevertToSnapshot(snapshotId);

            var state = service.CreateOrGetAccountExecutionState(address);
            Assert.Equal(5, state.Nonce);
        }

        [Fact]
        public void RevertToSnapshot_InvalidId_ShouldThrow()
        {
            var service = new ExecutionStateService(new MockNodeDataService());
            service.TakeSnapshot();

            Assert.Throws<InvalidOperationException>(() => service.RevertToSnapshot(999));
        }

        [Fact]
        public void CommitSnapshot_ShouldRemoveFromStack()
        {
            var service = new ExecutionStateService(new MockNodeDataService());

            var id1 = service.TakeSnapshot();
            var id2 = service.TakeSnapshot();

            service.CommitSnapshot(id1);

            Assert.Throws<InvalidOperationException>(() => service.RevertToSnapshot(id1));
        }
    }

    public class EvmPrecompileTests
    {
        private readonly EvmPreCompiledContractsExecution _precompiles = new EvmPreCompiledContractsExecution();

        [Fact]
        public void IsPrecompiledAddress_ShouldReturnTrueForAddresses1Through9()
        {
            Assert.True(_precompiles.IsPrecompiledAdress("0x0000000000000000000000000000000000000001"));
            Assert.True(_precompiles.IsPrecompiledAdress("0x0000000000000000000000000000000000000002"));
            Assert.True(_precompiles.IsPrecompiledAdress("0x0000000000000000000000000000000000000003"));
            Assert.True(_precompiles.IsPrecompiledAdress("0x0000000000000000000000000000000000000004"));
            Assert.True(_precompiles.IsPrecompiledAdress("0x0000000000000000000000000000000000000005"));
            Assert.True(_precompiles.IsPrecompiledAdress("0x0000000000000000000000000000000000000009"));
            Assert.False(_precompiles.IsPrecompiledAdress("0x000000000000000000000000000000000000000A"));
        }

        [Fact]
        public void Sha256_ShouldHashCorrectly()
        {
            var input = "68656c6c6f".HexToByteArray();
            var result = _precompiles.Sha256Hash(input);

            Assert.Equal(32, result.Length);
            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result.ToHex().ToLower());
        }

        [Fact]
        public void Sha256_EmptyInput_ShouldHashCorrectly()
        {
            var result = _precompiles.Sha256Hash(new byte[0]);
            Assert.Equal(32, result.Length);
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result.ToHex().ToLower());
        }

        [Fact]
        public void Ripemd160_ShouldHashCorrectly()
        {
            var input = "68656c6c6f".HexToByteArray();
            var result = _precompiles.Ripemd160Hash(input);

            Assert.Equal(32, result.Length);
            var resultHex = result.ToHex().ToLower();
            Assert.EndsWith("108f07b8382412612c048d07d13f814118445acd", resultHex);
        }

        [Fact]
        public void DataCopy_ShouldReturnSameData()
        {
            var input = "0102030405".HexToByteArray();
            var result = _precompiles.DataCopy(input);

            Assert.Equal(input, result);
        }

        [Fact]
        public void ModExp_SimpleCase()
        {
            var input = new byte[96 + 1 + 1 + 1];
            input[31] = 1;
            input[63] = 1;
            input[95] = 1;
            input[96] = 2;
            input[97] = 3;
            input[98] = 5;

            var result = _precompiles.ModExp(input);

            Assert.Single(result);
            Assert.Equal(3, result[0]);
        }

        [Fact]
        public void Blake2f_ShouldCompressCorrectly()
        {
            var input = new byte[213];
            input[3] = 12;

            var iv = new ulong[]
            {
                0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL,
                0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
                0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL,
                0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL
            };

            for (int i = 0; i < 8; i++)
            {
                var bytes = BitConverter.GetBytes(iv[i]);
                Array.Copy(bytes, 0, input, 4 + i * 8, 8);
            }

            input[212] = 1;

            var result = _precompiles.Blake2f(input);
            Assert.Equal(64, result.Length);
        }

        [Fact]
        public void Blake2f_InvalidLength_ShouldThrow()
        {
            var input = new byte[100];
            Assert.Throws<ArgumentException>(() => _precompiles.Blake2f(input));
        }

        [Fact]
        public void Blake2f_InvalidFinalFlag_ShouldThrow()
        {
            var input = new byte[213];
            input[212] = 2;

            Assert.Throws<ArgumentException>(() => _precompiles.Blake2f(input));
        }

        [Fact]
        public void BN128_ShouldThrowNotImplemented()
        {
            Assert.Throws<NotImplementedException>(() =>
                _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000006", new byte[0]));
            Assert.Throws<NotImplementedException>(() =>
                _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000007", new byte[0]));
            Assert.Throws<NotImplementedException>(() =>
                _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000008", new byte[0]));
        }
    }
}
