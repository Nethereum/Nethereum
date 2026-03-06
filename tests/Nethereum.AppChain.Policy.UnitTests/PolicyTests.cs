using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Policy.Bootstrap;
using Xunit;

namespace Nethereum.AppChain.Policy.UnitTests
{
    public class StubPolicyService : IPolicyService
    {
        private readonly PolicyConfig _config;

        public StubPolicyService(PolicyConfig config)
        {
            _config = config;
        }

        public Task<PolicyInfo> GetCurrentPolicyAsync()
        {
            return Task.FromResult(new PolicyInfo
            {
                Version = 1,
                MaxCalldataBytes = _config.MaxCalldataBytes,
                MaxLogBytes = _config.MaxLogBytes,
                BlockGasLimit = _config.BlockGasLimit,
                WritersRoot = _config.WritersRoot,
                AdminsRoot = _config.AdminsRoot,
                BlacklistRoot = _config.BlacklistRoot,
                Epoch = _config.Epoch
            });
        }

        public Task<byte[]?> GetWritersRootAsync() => Task.FromResult(_config.WritersRoot);
        public Task<byte[]?> GetAdminsRootAsync() => Task.FromResult(_config.AdminsRoot);
        public Task<byte[]?> GetBlacklistRootAsync() => Task.FromResult(_config.BlacklistRoot);
        public Task<BigInteger> GetEpochAsync() => Task.FromResult(_config.Epoch);

        public Task<bool> IsValidWriterAsync(string address, byte[][] writerProof, byte[]? blacklistProof = null)
        {
            if (_config.AllowedWriters == null || _config.AllowedWriters.Count == 0)
                return Task.FromResult(true);

            var lowerAddress = address.ToLowerInvariant();
            foreach (var writer in _config.AllowedWriters)
            {
                if (writer.ToLowerInvariant() == lowerAddress)
                    return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public class PolicyTests
    {
        [Fact]
        public void PolicyConfig_DefaultValues_AreCorrect()
        {
            var config = new PolicyConfig();

            Assert.True(config.Enabled);
            Assert.Equal(60000, config.SyncIntervalMs);
            Assert.Equal(128_000, config.MaxCalldataBytes);
            Assert.Equal(1_000_000, config.MaxLogBytes);
            Assert.Equal(30_000_000, config.BlockGasLimit);
            Assert.Equal(3, config.MaxRetries);
        }

        [Fact]
        public void PolicyInfo_DefaultValues_AreCorrect()
        {
            var info = new PolicyInfo();

            Assert.Equal(BigInteger.Zero, info.Version);
            Assert.Equal(BigInteger.Zero, info.MaxCalldataBytes);
            Assert.Null(info.WritersRoot);
            Assert.Null(info.AdminsRoot);
        }

        [Fact]
        public void MembershipProof_DefaultValues_AreCorrect()
        {
            var proof = new MembershipProof();

            Assert.Equal(string.Empty, proof.Address);
            Assert.Empty(proof.Proof);
            Assert.Null(proof.BlacklistProof);
        }

        [Fact]
        public async Task StubPolicyService_GetCurrentPolicy_ReturnsConfiguredValues()
        {
            var config = new PolicyConfig
            {
                MaxCalldataBytes = 200_000,
                MaxLogBytes = 2_000_000,
                BlockGasLimit = 50_000_000,
                Epoch = 5
            };
            var service = new StubPolicyService(config);

            var policy = await service.GetCurrentPolicyAsync();

            Assert.Equal(200_000, policy.MaxCalldataBytes);
            Assert.Equal(2_000_000, policy.MaxLogBytes);
            Assert.Equal(50_000_000, policy.BlockGasLimit);
            Assert.Equal(5, policy.Epoch);
        }

        [Fact]
        public async Task StubPolicyService_IsValidWriter_AllowsAllWhenNoAllowlist()
        {
            var config = new PolicyConfig();
            var service = new StubPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0x1234567890abcdef", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task StubPolicyService_IsValidWriter_AllowsListedAddress()
        {
            var config = new PolicyConfig
            {
                AllowedWriters = new List<string> { "0xabcd" }
            };
            var service = new StubPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xabcd", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task StubPolicyService_IsValidWriter_RejectsUnlistedAddress()
        {
            var config = new PolicyConfig
            {
                AllowedWriters = new List<string> { "0xabcd" }
            };
            var service = new StubPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0x1234", Array.Empty<byte[]>());

            Assert.False(isValid);
        }

        [Fact]
        public async Task StubPolicyService_IsValidWriter_IsCaseInsensitive()
        {
            var config = new PolicyConfig
            {
                AllowedWriters = new List<string> { "0xABCD" }
            };
            var service = new StubPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xabcd", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task StubPolicyService_GetEpoch_ReturnsConfiguredValue()
        {
            var config = new PolicyConfig { Epoch = 10 };
            var service = new StubPolicyService(config);

            var epoch = await service.GetEpochAsync();

            Assert.Equal(10, epoch);
        }

        [Fact]
        public async Task StubPolicyService_GetWritersRoot_ReturnsConfiguredValue()
        {
            var writersRoot = new byte[32];
            for (int i = 0; i < 32; i++) writersRoot[i] = (byte)i;

            var config = new PolicyConfig { WritersRoot = writersRoot };
            var service = new StubPolicyService(config);

            var root = await service.GetWritersRootAsync();

            Assert.Equal(writersRoot, root);
        }

        [Fact]
        public void EvmPolicyService_WithoutConfig_DoesNotThrow()
        {
            var config = new PolicyConfig { Enabled = false };

            var service = new EvmPolicyService(config);

            Assert.NotNull(service);
        }

        [Fact]
        public async Task EvmPolicyService_GetCurrentPolicy_ReturnsLocalConfigWithoutRpc()
        {
            var config = new PolicyConfig
            {
                Enabled = true,
                MaxCalldataBytes = 100_000,
                MaxLogBytes = 500_000,
                BlockGasLimit = 20_000_000,
                Epoch = 3
            };
            var service = new EvmPolicyService(config);

            var policy = await service.GetCurrentPolicyAsync();

            Assert.Equal(100_000, policy.MaxCalldataBytes);
            Assert.Equal(500_000, policy.MaxLogBytes);
            Assert.Equal(20_000_000, policy.BlockGasLimit);
        }

        [Fact]
        public async Task EvmPolicyService_GetWritersRoot_ReturnsLocalConfigWithoutRpc()
        {
            var writersRoot = new byte[32];
            var config = new PolicyConfig { WritersRoot = writersRoot };
            var service = new EvmPolicyService(config);

            var root = await service.GetWritersRootAsync();

            Assert.Equal(writersRoot, root);
        }

        [Fact]
        public async Task EvmPolicyService_GetEpoch_ReturnsLocalConfigWithoutRpc()
        {
            var config = new PolicyConfig { Epoch = 7 };
            var service = new EvmPolicyService(config);

            var epoch = await service.GetEpochAsync();

            Assert.Equal(7, epoch);
        }

        [Fact]
        public async Task EvmPolicyService_IsValidWriter_UsesLocalAllowlist()
        {
            var config = new PolicyConfig
            {
                AllowedWriters = new List<string> { "0xtest" }
            };
            var service = new EvmPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xtest", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task EvmPolicyService_IsValidWriter_AllowsAllWhenNoAllowlist()
        {
            var config = new PolicyConfig();
            var service = new EvmPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xany", Array.Empty<byte[]>());

            Assert.True(isValid);
        }
    }

    public class BootstrapPolicyTests
    {
        [Fact]
        public void BootstrapPolicyConfig_DefaultValues_AreCorrect()
        {
            var config = new BootstrapPolicyConfig();

            Assert.Empty(config.AllowedWriters);
            Assert.Empty(config.AllowedAdmins);
            Assert.Equal(128_000, config.MaxCalldataBytes);
            Assert.Equal(1_000_000, config.MaxLogBytes);
            Assert.Equal(30_000_000, config.BlockGasLimit);
            Assert.False(config.OpenWriterAccess);
            Assert.False(config.OpenAdminAccess);
        }

        [Fact]
        public void BootstrapPolicyConfig_OpenAccess_SetsCorrectFlags()
        {
            var config = BootstrapPolicyConfig.OpenAccess();

            Assert.True(config.OpenWriterAccess);
            Assert.True(config.OpenAdminAccess);
        }

        [Fact]
        public void BootstrapPolicyConfig_WithWriters_AddsWriters()
        {
            var config = BootstrapPolicyConfig.WithWriters("0xabc", "0xdef");

            Assert.Equal(2, config.AllowedWriters.Count);
            Assert.Contains("0xabc", config.AllowedWriters);
            Assert.Contains("0xdef", config.AllowedWriters);
        }

        [Fact]
        public async Task BootstrapPolicyService_IsValidWriter_OpenAccess_AllowsAll()
        {
            var config = BootstrapPolicyConfig.OpenAccess();
            var service = new BootstrapPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xany", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task BootstrapPolicyService_IsValidWriter_EmptyList_AllowsAll()
        {
            var config = new BootstrapPolicyConfig();
            var service = new BootstrapPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xany", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task BootstrapPolicyService_IsValidWriter_AllowsListedAddress()
        {
            var config = new BootstrapPolicyConfig
            {
                AllowedWriters = new List<string> { "0xabcd" }
            };
            var service = new BootstrapPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xabcd", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task BootstrapPolicyService_IsValidWriter_RejectsUnlistedAddress()
        {
            var config = new BootstrapPolicyConfig
            {
                AllowedWriters = new List<string> { "0xabcd" }
            };
            var service = new BootstrapPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0x1234", Array.Empty<byte[]>());

            Assert.False(isValid);
        }

        [Fact]
        public async Task BootstrapPolicyService_IsValidWriter_IsCaseInsensitive()
        {
            var config = new BootstrapPolicyConfig
            {
                AllowedWriters = new List<string> { "0xABCD" }
            };
            var service = new BootstrapPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xabcd", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public void BootstrapPolicyService_IsValidAdmin_AllowsListedAdmin()
        {
            var config = new BootstrapPolicyConfig
            {
                AllowedAdmins = new List<string> { "0xadmin" }
            };
            var service = new BootstrapPolicyService(config);

            var isValid = service.IsValidAdmin("0xadmin");

            Assert.True(isValid);
        }

        [Fact]
        public void BootstrapPolicyService_IsValidAdmin_RejectsUnlistedAdmin()
        {
            var config = new BootstrapPolicyConfig
            {
                AllowedAdmins = new List<string> { "0xadmin" }
            };
            var service = new BootstrapPolicyService(config);

            var isValid = service.IsValidAdmin("0xother");

            Assert.False(isValid);
        }

        [Fact]
        public void BootstrapPolicyService_AddWriter_AddsNewWriter()
        {
            var config = new BootstrapPolicyConfig();
            var service = new BootstrapPolicyService(config);

            service.AddWriter("0xnewwriter");

            Assert.Contains("0xnewwriter", config.AllowedWriters);
        }

        [Fact]
        public void BootstrapPolicyService_RemoveWriter_RemovesWriter()
        {
            var config = new BootstrapPolicyConfig
            {
                AllowedWriters = new List<string> { "0xwriter" }
            };
            var service = new BootstrapPolicyService(config);

            service.RemoveWriter("0xwriter");

            Assert.Empty(config.AllowedWriters);
        }

        [Fact]
        public async Task BootstrapPolicyService_GetCurrentPolicy_ReturnsConfigValues()
        {
            var config = new BootstrapPolicyConfig
            {
                MaxCalldataBytes = 200_000,
                MaxLogBytes = 2_000_000,
                BlockGasLimit = 50_000_000,
                SequencerAddress = "0xseq"
            };
            var service = new BootstrapPolicyService(config);

            var policy = await service.GetCurrentPolicyAsync();

            Assert.Equal(200_000, policy.MaxCalldataBytes);
            Assert.Equal(2_000_000, policy.MaxLogBytes);
            Assert.Equal(50_000_000, policy.BlockGasLimit);
            Assert.Equal("0xseq", policy.Sequencer);
            Assert.Equal(0, policy.Version);
        }

        [Fact]
        public async Task BootstrapPolicyService_GetEpoch_ReturnsZero()
        {
            var config = new BootstrapPolicyConfig();
            var service = new BootstrapPolicyService(config);

            var epoch = await service.GetEpochAsync();

            Assert.Equal(BigInteger.Zero, epoch);
        }

        [Fact]
        public async Task BootstrapPolicyService_GetWritersRoot_ReturnsNull()
        {
            var config = new BootstrapPolicyConfig();
            var service = new BootstrapPolicyService(config);

            var root = await service.GetWritersRootAsync();

            Assert.Null(root);
        }
    }

    public class PolicyMigrationTests
    {
        [Fact]
        public void PolicyMigrationService_ComputeMerkleRoot_EmptyList_ReturnsZeroRoot()
        {
            var service = new PolicyMigrationService();

            var root = service.ComputeMerkleRoot(Array.Empty<string>());

            Assert.NotNull(root);
            Assert.Equal(32, root.Length);
            Assert.All(root, b => Assert.Equal(0, b));
        }

        [Fact]
        public void PolicyMigrationService_ComputeMerkleRoot_SingleAddress_ReturnsNonZero()
        {
            var service = new PolicyMigrationService();

            var root = service.ComputeMerkleRoot(new[] { "0x1234567890123456789012345678901234567890" });

            Assert.NotNull(root);
            Assert.Equal(32, root.Length);
            Assert.False(root.All(b => b == 0));
        }

        [Fact]
        public void PolicyMigrationService_ComputeMerkleRoot_IsDeterministic()
        {
            var service = new PolicyMigrationService();
            var addresses = new[] { "0xabc", "0xdef", "0x123" };

            var root1 = service.ComputeMerkleRoot(addresses);
            var root2 = service.ComputeMerkleRoot(addresses);

            Assert.Equal(root1, root2);
        }

        [Fact]
        public void PolicyMigrationService_ComputeMerkleProof_ReturnsProof()
        {
            var service = new PolicyMigrationService();
            var addresses = new[] { "0x1234567890123456789012345678901234567890", "0x2345678901234567890123456789012345678901" };

            var proof = service.ComputeMerkleProof(addresses[0], addresses);

            Assert.NotNull(proof);
        }

        [Fact]
        public void PolicyMigrationService_VerifyMerkleProof_EmptyRoot_ReturnsTrue()
        {
            var service = new PolicyMigrationService();
            var emptyRoot = new byte[32];

            var isValid = service.VerifyMerkleProof("0xany", emptyRoot, Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public void PolicyMigrationService_PrepareMigrationData_ReturnsValidData()
        {
            var service = new PolicyMigrationService();
            var config = new BootstrapPolicyConfig
            {
                AllowedWriters = new List<string> { "0x1234567890123456789012345678901234567890", "0x2345678901234567890123456789012345678901" },
                AllowedAdmins = new List<string> { "0x3456789012345678901234567890123456789012" },
                MaxCalldataBytes = 100_000,
                SequencerAddress = "0x4567890123456789012345678901234567890123"
            };

            var data = service.PrepareMigrationData(config);

            Assert.NotNull(data.WritersRoot);
            Assert.NotNull(data.AdminsRoot);
            Assert.Equal(2, data.WriterProofs.Count);
            Assert.Single(data.AdminProofs);
            Assert.Equal(100_000, data.MaxCalldataBytes);
            Assert.Equal("0x4567890123456789012345678901234567890123", data.SequencerAddress);
        }
    }
}
