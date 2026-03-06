using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.AppChain.Policy.Bootstrap;
using Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy;
using Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class AppChainPolicyContractTests : IAsyncLifetime
    {
        private DevChainNode? _devChain;
        private Web3.Web3? _web3;
        private AppChainPolicyService? _policyService;
        private string _contractAddress = "";
        private PolicyMigrationService _migrationService = new PolicyMigrationService();

        private const string AdminPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _adminAddress;
        private const string WriterPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318";
        private readonly string _writerAddress;
        private const string SequencerPrivateKey = "0x1234567890123456789012345678901234567890123456789012345678901234";
        private readonly string _sequencerAddress;
        private static readonly BigInteger AppChainId = new BigInteger(420420);

        private byte[] _initialWritersRoot = new byte[32];
        private byte[] _initialAdminsRoot = new byte[32];
        private byte[][] _adminProof = Array.Empty<byte[]>();
        private byte[][] _writerProof = Array.Empty<byte[]>();

        public AppChainPolicyContractTests()
        {
            var adminKey = new Nethereum.Signer.EthECKey(AdminPrivateKey);
            _adminAddress = adminKey.GetPublicAddress();

            var writerKey = new Nethereum.Signer.EthECKey(WriterPrivateKey);
            _writerAddress = writerKey.GetPublicAddress();

            var sequencerKey = new Nethereum.Signer.EthECKey(SequencerPrivateKey);
            _sequencerAddress = sequencerKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            var config = new DevChainConfig
            {
                ChainId = 1337,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("100000000000000000000000")
            };
            _devChain = new DevChainNode(config);
            await _devChain.StartAsync(new[] { _adminAddress, _writerAddress, _sequencerAddress });

            var bootstrapConfig = new BootstrapPolicyConfig
            {
                AllowedWriters = new List<string> { _adminAddress, _writerAddress },
                AllowedAdmins = new List<string> { _adminAddress }
            };
            var migrationData = _migrationService.PrepareMigrationData(bootstrapConfig);
            _initialWritersRoot = migrationData.WritersRoot;
            _initialAdminsRoot = migrationData.AdminsRoot;
            _adminProof = migrationData.AdminProofs[_adminAddress];
            _writerProof = migrationData.WriterProofs[_writerAddress];

            var account = new Account(AdminPrivateKey, 1337);
            var rpcClient = new DevChainRpcClient(_devChain, 1337);
            _web3 = new Web3.Web3(account, rpcClient);
            _web3.TransactionManager.UseLegacyAsDefault = true;

            var deployment = new AppChainPolicyDeployment
            {
                AppChainId = AppChainId,
                Sequencer = _sequencerAddress,
                InitialWritersRoot = _initialWritersRoot,
                InitialAdminsRoot = _initialAdminsRoot
            };

            var receipt = await AppChainPolicyService.DeployContractAndWaitForReceiptAsync(_web3, deployment);
            _contractAddress = receipt.ContractAddress!;
            _policyService = new AppChainPolicyService(_web3, _contractAddress);
        }

        public Task DisposeAsync()
        {
            _devChain?.Dispose();
            _devChain = null;
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Deploy_SetsInitialWritersRoot()
        {
            var writersRoot = await _policyService!.WritersRootQueryAsync();

            Assert.Equal(_initialWritersRoot, writersRoot);
        }

        [Fact]
        public async Task Deploy_SetsInitialAdminsRoot()
        {
            var adminsRoot = await _policyService!.AdminsRootQueryAsync();

            Assert.Equal(_initialAdminsRoot, adminsRoot);
        }

        [Fact]
        public async Task Deploy_BlacklistRootStartsEmpty()
        {
            var blacklistRoot = await _policyService!.BlacklistRootQueryAsync();

            Assert.True(blacklistRoot.All(b => b == 0));
        }

        [Fact]
        public async Task Deploy_EpochStartsAtZero()
        {
            var epoch = await _policyService!.EpochQueryAsync();

            Assert.Equal(0, epoch);
        }

        [Fact]
        public async Task CurrentPolicy_ReturnsDefaultConfig()
        {
            var policy = await _policyService!.CurrentPolicyQueryAsync();

            Assert.True(policy.MaxCalldataBytes > 0);
            Assert.True(policy.MaxLogBytes > 0);
            Assert.True(policy.BlockGasLimit > 0);
            Assert.Equal(_sequencerAddress.ToLowerInvariant(), policy.Sequencer.ToLowerInvariant());
        }

        [Fact]
        public async Task IsValidWriter_ReturnsTrueForWriter()
        {
            var adminWriterProof = _migrationService.ComputeMerkleProof(_adminAddress, new[] { _adminAddress, _writerAddress });

            var isValid = await _policyService!.IsValidWriterQueryAsync(
                _adminAddress,
                adminWriterProof.ToList(),
                new List<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task IsValidWriter_ReturnsFalseForNonWriter()
        {
            var unknownAddress = "0x0000000000000000000000000000000000000001";

            var isValid = await _policyService!.IsValidWriterQueryAsync(
                unknownAddress,
                new List<byte[]>(),
                new List<byte[]>());

            Assert.False(isValid);
        }

        [Fact]
        public async Task Invite_UpdatesWritersRoot()
        {
            var newMember = "0x0000000000000000000000000000000000000099";
            var newWritersList = new List<string> { _adminAddress, _writerAddress, newMember };
            var newWritersRoot = _migrationService.ComputeMerkleRoot(newWritersList);
            var adminWriterProof = _migrationService.ComputeMerkleProof(_adminAddress, new[] { _adminAddress, _writerAddress });

            await _policyService!.InviteRequestAndWaitForReceiptAsync(
                newMember,
                newWritersRoot,
                adminWriterProof.ToList());

            var writersRoot = await _policyService.WritersRootQueryAsync();
            Assert.Equal(newWritersRoot, writersRoot);
        }

        [Fact]
        public async Task Invite_EmitsMemberInvitedEvent()
        {
            var newMember = "0x0000000000000000000000000000000000000099";
            var newWritersList = new List<string> { _adminAddress, _writerAddress, newMember };
            var newWritersRoot = _migrationService.ComputeMerkleRoot(newWritersList);
            var adminWriterProof = _migrationService.ComputeMerkleProof(_adminAddress, new[] { _adminAddress, _writerAddress });

            var receipt = await _policyService!.InviteRequestAndWaitForReceiptAsync(
                newMember,
                newWritersRoot,
                adminWriterProof.ToList());

            var events = receipt.DecodeAllEvents<MemberInvitedEventDTO>();
            Assert.Single(events);
            Assert.Equal(_adminAddress.ToLowerInvariant(), events[0].Event.Inviter.ToLowerInvariant());
            Assert.Equal(newMember.ToLowerInvariant(), events[0].Event.Invitee.ToLowerInvariant());
        }

        [Fact]
        public async Task Invite_OnlyWritersCanInvite()
        {
            var nonWriterKey = "0xabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcd";
            var nonWriterAccount = new Account(nonWriterKey, 1337);
            var nonWriterAddress = nonWriterAccount.Address;

            await _devChain!.SetBalanceAsync(nonWriterAddress, BigInteger.Parse("10000000000000000000"));

            var nonWriterRpcClient = new DevChainRpcClient(_devChain, 1337);
            var nonWriterWeb3 = new Web3.Web3(nonWriterAccount, nonWriterRpcClient);
            nonWriterWeb3.TransactionManager.UseLegacyAsDefault = true;
            var nonWriterPolicyService = new AppChainPolicyService(nonWriterWeb3, _contractAddress);

            var newMember = "0x0000000000000000000000000000000000000099";
            var newWritersRoot = _migrationService.ComputeMerkleRoot(new[] { _adminAddress, _writerAddress, newMember });

            await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
            {
                await nonWriterPolicyService.InviteRequestAndWaitForReceiptAsync(
                    newMember,
                    newWritersRoot,
                    new List<byte[]>());
            });
        }

        [Fact]
        public async Task Ban_UpdatesBlacklistRoot()
        {
            var toBan = _writerAddress;
            var blacklistedAddresses = new[] { toBan };
            var newBlacklistRoot = _migrationService.ComputeMerkleRoot(blacklistedAddresses);

            await _policyService!.BanRequestAndWaitForReceiptAsync(
                toBan,
                newBlacklistRoot,
                _adminProof.ToList());

            var blacklistRoot = await _policyService.BlacklistRootQueryAsync();
            Assert.Equal(newBlacklistRoot, blacklistRoot);
        }

        [Fact]
        public async Task Ban_EmitsMemberBannedEvent()
        {
            var toBan = _writerAddress;
            var blacklistedAddresses = new[] { toBan };
            var newBlacklistRoot = _migrationService.ComputeMerkleRoot(blacklistedAddresses);

            var receipt = await _policyService!.BanRequestAndWaitForReceiptAsync(
                toBan,
                newBlacklistRoot,
                _adminProof.ToList());

            var events = receipt.DecodeAllEvents<MemberBannedEventDTO>();
            Assert.Single(events);
            Assert.Equal(_adminAddress.ToLowerInvariant(), events[0].Event.BannedBy.ToLowerInvariant());
            Assert.Equal(toBan.ToLowerInvariant(), events[0].Event.Banned.ToLowerInvariant());
        }

        [Fact]
        public async Task Ban_OnlyAdminsCanBan()
        {
            var writerAccount = new Account(WriterPrivateKey, 1337);
            var writerRpcClient = new DevChainRpcClient(_devChain!, 1337);
            var writerWeb3 = new Web3.Web3(writerAccount, writerRpcClient);
            writerWeb3.TransactionManager.UseLegacyAsDefault = true;
            var writerPolicyService = new AppChainPolicyService(writerWeb3, _contractAddress);

            var toBan = "0x0000000000000000000000000000000000000001";
            var newBlacklistRoot = _migrationService.ComputeMerkleRoot(new[] { toBan });

            await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
            {
                await writerPolicyService.BanRequestAndWaitForReceiptAsync(
                    toBan,
                    newBlacklistRoot,
                    new List<byte[]>());
            });
        }

        [Fact]
        public async Task UpdatePolicy_ChangesLimits()
        {
            var newMaxCalldata = new BigInteger(256_000);
            var newMaxLog = new BigInteger(2_000_000);
            var newBlockGasLimit = new BigInteger(60_000_000);

            await _policyService!.UpdatePolicyRequestAndWaitForReceiptAsync(
                newMaxCalldata,
                newMaxLog,
                newBlockGasLimit,
                _sequencerAddress,
                _adminProof.ToList());

            var policy = await _policyService.CurrentPolicyQueryAsync();
            Assert.Equal(newMaxCalldata, policy.MaxCalldataBytes);
            Assert.Equal(newMaxLog, policy.MaxLogBytes);
            Assert.Equal(newBlockGasLimit, policy.BlockGasLimit);
        }

        [Fact]
        public async Task UpdatePolicy_EmitsPolicyChangedEvent()
        {
            var newMaxCalldata = new BigInteger(256_000);
            var newMaxLog = new BigInteger(2_000_000);
            var newBlockGasLimit = new BigInteger(60_000_000);

            var receipt = await _policyService!.UpdatePolicyRequestAndWaitForReceiptAsync(
                newMaxCalldata,
                newMaxLog,
                newBlockGasLimit,
                _sequencerAddress,
                _adminProof.ToList());

            var events = receipt.DecodeAllEvents<PolicyChangedEventDTO>();
            Assert.Single(events);
        }

        [Fact]
        public async Task RebuildTrees_IncrementsEpoch()
        {
            var epochBefore = await _policyService!.EpochQueryAsync();

            await _policyService.RebuildTreesRequestAndWaitForReceiptAsync(
                _initialWritersRoot,
                _initialAdminsRoot,
                _adminProof.ToList());

            var epochAfter = await _policyService.EpochQueryAsync();
            Assert.Equal(epochBefore + 1, epochAfter);
        }

        [Fact]
        public async Task RebuildTrees_ClearsBlacklist()
        {
            var toBan = _writerAddress;
            var newBlacklistRoot = _migrationService.ComputeMerkleRoot(new[] { toBan });
            await _policyService!.BanRequestAndWaitForReceiptAsync(
                toBan,
                newBlacklistRoot,
                _adminProof.ToList());

            var blacklistBefore = await _policyService.BlacklistRootQueryAsync();
            Assert.False(blacklistBefore.All(b => b == 0));

            await _policyService.RebuildTreesRequestAndWaitForReceiptAsync(
                _initialWritersRoot,
                _initialAdminsRoot,
                _adminProof.ToList());

            var blacklistAfter = await _policyService.BlacklistRootQueryAsync();
            Assert.True(blacklistAfter.All(b => b == 0));
        }

        [Fact]
        public async Task RebuildTrees_EmitsTreeRebuiltEvent()
        {
            var receipt = await _policyService!.RebuildTreesRequestAndWaitForReceiptAsync(
                _initialWritersRoot,
                _initialAdminsRoot,
                _adminProof.ToList());

            var events = receipt.DecodeAllEvents<TreeRebuiltEventDTO>();
            Assert.Single(events);
            Assert.Equal(1, events[0].Event.NewEpoch);
        }

        [Fact]
        public async Task RebuildTrees_OnlyAdminsCanRebuild()
        {
            var writerAccount = new Account(WriterPrivateKey, 1337);
            var writerRpcClient = new DevChainRpcClient(_devChain!, 1337);
            var writerWeb3 = new Web3.Web3(writerAccount, writerRpcClient);
            writerWeb3.TransactionManager.UseLegacyAsDefault = true;
            var writerPolicyService = new AppChainPolicyService(writerWeb3, _contractAddress);

            await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
            {
                await writerPolicyService.RebuildTreesRequestAndWaitForReceiptAsync(
                    _initialWritersRoot,
                    _initialAdminsRoot,
                    new List<byte[]>());
            });
        }

        [Fact]
        public async Task IsValidWriter_ReturnsFalseForBannedWriter()
        {
            var toBan = _writerAddress;
            var newBlacklistRoot = _migrationService.ComputeMerkleRoot(new[] { toBan });
            await _policyService!.BanRequestAndWaitForReceiptAsync(
                toBan,
                newBlacklistRoot,
                _adminProof.ToList());

            var blacklistProof = _migrationService.ComputeMerkleProof(toBan, new[] { toBan });

            var isValid = await _policyService.IsValidWriterQueryAsync(
                toBan,
                _writerProof.ToList(),
                blacklistProof.ToList());

            Assert.False(isValid);
        }
    }
}
