using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    public class ERC7562IntegratedValidationTests
    {
        private readonly ERC7562SimulationService _simulationService;
        private readonly InMemoryNodeDataService _nodeDataService;
        private const string EntryPointAddress = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";

        public ERC7562IntegratedValidationTests()
        {
            _nodeDataService = new InMemoryNodeDataService();
            _simulationService = new ERC7562SimulationService(_nodeDataService, DefaultHardforkConfigs.Osaka);
        }

        private async Task SetupContractAsync(string senderAddress, byte[] contractCode)
        {
            await _nodeDataService.SetCodeAsync(senderAddress, contractCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));
        }

        #region [OP-011] Forbidden Opcode Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-011")]
        public async Task Given_ContractUsesORIGIN_When_ValidationSimulated_Then_DetectsOP011Violation()
        {
            var contractCode = "32600052602060006000F3".HexToByteArray();
            var senderAddress = "0x1111111111111111111111111111111111111111";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-011");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-011")]
        public async Task Given_ContractUsesCOINBASE_When_ValidationSimulated_Then_DetectsOP011Violation()
        {
            var contractCode = "41600052602060006000F3".HexToByteArray();
            var senderAddress = "0x2222222222222222222222222222222222222222";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-011");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-011")]
        public async Task Given_ContractUsesBLOCKHASH_When_ValidationSimulated_Then_DetectsOP011Violation()
        {
            var contractCode = "600140600052602060006000F3".HexToByteArray();
            var senderAddress = "0x3333333333333333333333333333333333333333";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-011");
        }

        #endregion

        #region [OP-012] GAS Opcode Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-012")]
        public async Task Given_ContractUsesGASFollowedByADD_When_ValidationSimulated_Then_DetectsOP012Violation()
        {
            var contractCode = "5A600001600052602060006000F3".HexToByteArray();
            var senderAddress = "0x4444444444444444444444444444444444444444";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-012");
        }

        #endregion

        #region [OP-080] Staked-Only Opcodes Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-080")]
        public async Task Given_UnstakedContractUsesSELFBALANCE_When_ValidationSimulated_Then_DetectsOP080Violation()
        {
            var contractCode = "47600052602060006000F3".HexToByteArray();
            var senderAddress = "0x5555555555555555555555555555555555555555";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-080");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-080")]
        public async Task Given_StakedContractUsesSELFBALANCE_When_ValidationSimulated_Then_NoViolation()
        {
            var contractCode = "47600052602060006000F3".HexToByteArray();
            var senderAddress = "0x6666666666666666666666666666666666666666";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-080");
        }

        #endregion

        #region [OP-061] CALL with Value Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-061")]
        public async Task Given_ContractCallsWithValueToNonEntryPoint_When_ValidationSimulated_Then_DetectsOP061Violation()
        {
            var targetAddress = "0x7777777777777777777777777777777777777777";
            var contractCode = BuildCallWithValueBytecode(targetAddress, 100);
            var senderAddress = "0x8888888888888888888888888888888888888888";

            await SetupContractAsync(senderAddress, contractCode);
            await _nodeDataService.SetCodeAsync(targetAddress, new byte[] { 0x00 });

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.Contains(result.Violations, v => v.Rule == "OP-061");
        }

        #endregion

        #region Valid Contract Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        public async Task Given_ValidSimpleContract_When_ValidationSimulated_Then_NoViolations()
        {
            var contractCode = "6001600052602060006000F3".HexToByteArray();
            var senderAddress = "0x9999999999999999999999999999999999999999";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-011");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        public async Task Given_ContractWithSLOADToOwnStorage_When_ValidationSimulated_Then_NoStorageViolation()
        {
            var contractCode = "600054600052602060006000F3".HexToByteArray();
            var senderAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            Assert.DoesNotContain(result.Violations, v => v.Rule.StartsWith("STO-"));
        }

        #endregion

        #region Helper Methods

        private PackedUserOperationDTO CreateTestUserOp(string senderAddress)
        {
            return new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = "0x".HexToByteArray(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };
        }

        private byte[] BuildCallWithValueBytecode(string targetAddress, long value)
        {
            var bytes = new List<byte>();

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, (byte)value });
            bytes.Add(0x73);
            bytes.AddRange(targetAddress.HexToByteArray());
            bytes.Add(0x5A);
            bytes.Add(0xF1);
            bytes.Add(0x50);
            bytes.Add(0x00);

            return bytes.ToArray();
        }

        #endregion
    }

    public class InMemoryNodeDataService : IStateReader
    {
        private readonly Dictionary<string, byte[]> _code = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BigInteger> _balances = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<BigInteger, byte[]>> _storage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BigInteger> _nonces = new(StringComparer.OrdinalIgnoreCase);

        public Task<byte[]> GetCodeAsync(string address)
        {
            _code.TryGetValue(address, out var code);
            return Task.FromResult(code ?? Array.Empty<byte>());
        }

        public Task<byte[]> GetCodeAsync(byte[] address)
        {
            return GetCodeAsync("0x" + address.ToHex());
        }

        public Task<EvmUInt256> GetBalanceAsync(string address)
        {
            _balances.TryGetValue(address, out var balance);
            return Task.FromResult(EvmUInt256BigIntegerExtensions.FromBigInteger(balance));
        }

        public Task<EvmUInt256> GetBalanceAsync(byte[] address)
        {
            return GetBalanceAsync("0x" + address.ToHex());
        }

        public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
        {
            var positionBig = position.ToBigInteger();
            if (_storage.TryGetValue(address, out var slots))
            {
                if (slots.TryGetValue(positionBig, out var value))
                {
                    return Task.FromResult(value);
                }
            }
            return Task.FromResult(new byte[32]);
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position)
        {
            return GetStorageAtAsync("0x" + address.ToHex(), position);
        }

        public Task<EvmUInt256> GetTransactionCountAsync(string address)
        {
            _nonces.TryGetValue(address, out var nonce);
            return Task.FromResult(EvmUInt256BigIntegerExtensions.FromBigInteger(nonce));
        }

        public Task<EvmUInt256> GetTransactionCountAsync(byte[] address)
        {
            return GetTransactionCountAsync("0x" + address.ToHex());
        }

        public Task SetCodeAsync(string address, byte[] code)
        {
            _code[address] = code;
            return Task.CompletedTask;
        }

        public Task SetBalanceAsync(string address, BigInteger balance)
        {
            _balances[address] = balance;
            return Task.CompletedTask;
        }

        public Task SetStorageAsync(string address, BigInteger slot, byte[] value)
        {
            if (!_storage.TryGetValue(address, out var slots))
            {
                slots = new Dictionary<BigInteger, byte[]>();
                _storage[address] = slots;
            }
            slots[slot] = value;
            return Task.CompletedTask;
        }

        public Task<byte[]> GetBlockHashAsync(long blockNumber)
        {
            return Task.FromResult(new byte[32]);
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            return Task.FromResult(_code.ContainsKey(address) || _balances.ContainsKey(address));
        }
    }
}
