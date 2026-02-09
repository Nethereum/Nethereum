using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    public class ERC7562IntegratedValidationTests
    {
        private readonly ERC7562SimulationService _simulationService;
        private readonly InMemoryNodeDataService _nodeDataService;
        private const string EntryPointAddress = "0x433709009B8330FDa32311DF1C2AFA402eD8D009"; // v0.9

        public ERC7562IntegratedValidationTests()
        {
            _nodeDataService = new InMemoryNodeDataService();
            _simulationService = new ERC7562SimulationService(_nodeDataService, HardforkConfig.Default);
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
            // GIVEN: Contract that uses ORIGIN opcode
            // ORIGIN (0x32) PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "32600052602060006000F3".HexToByteArray();
            var senderAddress = "0x1111111111111111111111111111111111111111";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false);

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should detect OP-011 violation for ORIGIN
            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-011");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-011")]
        public async Task Given_ContractUsesCOINBASE_When_ValidationSimulated_Then_DetectsOP011Violation()
        {
            // GIVEN: Contract that uses COINBASE opcode
            // COINBASE (0x41) PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "41600052602060006000F3".HexToByteArray();
            var senderAddress = "0x2222222222222222222222222222222222222222";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false);

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should detect OP-011 violation for COINBASE
            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-011");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-011")]
        public async Task Given_ContractUsesBLOCKHASH_When_ValidationSimulated_Then_DetectsOP011Violation()
        {
            // GIVEN: Contract that uses BLOCKHASH opcode
            // PUSH1 0x01 BLOCKHASH (0x40) PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "600140600052602060006000F3".HexToByteArray();
            var senderAddress = "0x3333333333333333333333333333333333333333";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false);

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should detect OP-011 violation for BLOCKHASH
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
            // GIVEN: Contract that uses GAS not followed by CALL
            // GAS (0x5A) PUSH1 0x00 ADD PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "5A600001600052602060006000F3".HexToByteArray();
            var senderAddress = "0x4444444444444444444444444444444444444444";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false);

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should detect OP-012 violation (GAS not followed by CALL)
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
            // GIVEN: Contract that uses SELFBALANCE when not staked
            // SELFBALANCE (0x47) PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "47600052602060006000F3".HexToByteArray();
            var senderAddress = "0x5555555555555555555555555555555555555555";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false); // NOT staked

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should detect OP-080 violation (SELFBALANCE requires staking)
            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-080");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-080")]
        public async Task Given_StakedContractUsesSELFBALANCE_When_ValidationSimulated_Then_NoViolation()
        {
            // GIVEN: Contract that uses SELFBALANCE when staked
            // SELFBALANCE (0x47) PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "47600052602060006000F3".HexToByteArray();
            var senderAddress = "0x6666666666666666666666666666666666666666";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: true); // IS staked

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should NOT detect OP-080 violation when staked
            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-080");
        }

        #endregion

        #region [OP-061] CALL with Value Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Rule", "OP-061")]
        public async Task Given_ContractCallsWithValueToNonEntryPoint_When_ValidationSimulated_Then_DetectsOP061Violation()
        {
            // GIVEN: Contract that does CALL with value to an external address (not EntryPoint)
            // PUSH20 target PUSH1 0x00 PUSH1 0x00 PUSH1 0x00 PUSH1 0x00 PUSH1 0x64 (100 wei value) GAS CALL
            var targetAddress = "0x7777777777777777777777777777777777777777";
            var contractCode = BuildCallWithValueBytecode(targetAddress, 100);
            var senderAddress = "0x8888888888888888888888888888888888888888";

            await SetupContractAsync(senderAddress, contractCode);
            await _nodeDataService.SetCodeAsync(targetAddress, new byte[] { 0x00 });

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false);

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should detect OP-061 violation (CALL with value only allowed to EntryPoint)
            Assert.Contains(result.Violations, v => v.Rule == "OP-061");
        }

        #endregion

        #region Valid Contract Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        public async Task Given_ValidSimpleContract_When_ValidationSimulated_Then_NoViolations()
        {
            // GIVEN: A simple valid contract that just returns success
            // PUSH1 0x01 PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "6001600052602060006000F3".HexToByteArray();
            var senderAddress = "0x9999999999999999999999999999999999999999";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false);

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should have no OP-011 violations (basic opcodes are allowed)
            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-011");
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        public async Task Given_ContractWithSLOADToOwnStorage_When_ValidationSimulated_Then_NoStorageViolation()
        {
            // GIVEN: Contract that reads its own storage (allowed per STO-010)
            // PUSH1 0x00 SLOAD PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var contractCode = "600054600052602060006000F3".HexToByteArray();
            var senderAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            await SetupContractAsync(senderAddress, contractCode);

            var userOp = CreateTestUserOp(senderAddress);
            var sender = EntityInfo.CreateSender(senderAddress, isStaked: false);

            // WHEN: Running validation simulation
            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender);

            // THEN: Should NOT have storage violations for own storage
            Assert.DoesNotContain(result.Violations, v => v.Rule.StartsWith("STO-"));
        }

        #endregion

        #region Bundle Conflict Integration Tests

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Feature", "BundleConflict")]
        public async Task Given_TwoUserOpsAccessingSameStorage_When_BundleValidated_Then_DetectsConflict()
        {
            // GIVEN: Two different senders with contracts that access the same external storage
            var sharedStorageContract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var sender1Address = "0xcccccccccccccccccccccccccccccccccccccccc";
            var sender2Address = "0xdddddddddddddddddddddddddddddddddddddddd";

            // Contract that writes to slot 42 of external contract
            // This is a simplified test - in reality, the detection happens through storage profiles
            var conflictDetector = new BundleStorageConflictDetector();

            var slot42 = new StorageSlotKey(sharedStorageContract, 42);

            var profile1 = new UserOpStorageProfile
            {
                UserOpHash = "0x1111",
                SenderAddress = sender1Address
            };
            profile1.WriteSlots.Add(slot42);

            var profile2 = new UserOpStorageProfile
            {
                UserOpHash = "0x2222",
                SenderAddress = sender2Address
            };
            profile2.WriteSlots.Add(slot42);

            // WHEN: Detecting conflicts
            var conflicts = conflictDetector.DetectConflicts(new[] { profile1, profile2 });

            // THEN: Should detect write-write conflict
            Assert.NotEmpty(conflicts);
            Assert.Contains(conflicts, c =>
                c.Type == StorageConflictType.WriteWrite &&
                c.Slot == 42);
        }

        [Fact]
        [Trait("Category", "ERC7562-Integration")]
        [Trait("Feature", "BundleConflict")]
        public async Task Given_TwoUserOpsWithDifferentStorage_When_BundleValidated_Then_NoConflict()
        {
            // GIVEN: Two senders accessing different storage slots
            var contract = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
            var sender1Address = "0xf1f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1f1";
            var sender2Address = "0xf2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2";

            var conflictDetector = new BundleStorageConflictDetector();

            var profile1 = new UserOpStorageProfile
            {
                UserOpHash = "0x1111",
                SenderAddress = sender1Address
            };
            profile1.WriteSlots.Add(new StorageSlotKey(contract, 1));

            var profile2 = new UserOpStorageProfile
            {
                UserOpHash = "0x2222",
                SenderAddress = sender2Address
            };
            profile2.WriteSlots.Add(new StorageSlotKey(contract, 2)); // Different slot

            // WHEN: Detecting conflicts
            var conflicts = conflictDetector.DetectConflicts(new[] { profile1, profile2 });

            // THEN: Should NOT detect conflict (different slots)
            // Filter out sender conflicts since we have different senders
            var storageConflicts = conflicts.Where(c =>
                c.Type == StorageConflictType.WriteWrite ||
                c.Type == StorageConflictType.ReadWrite).ToList();
            Assert.Empty(storageConflicts);
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
            // Simplified bytecode that does CALL with value
            // In real scenarios this would be more complex
            var bytes = new List<byte>();

            // PUSH1 0x00 (retSize)
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            // PUSH1 0x00 (retOffset)
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            // PUSH1 0x00 (argsSize)
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            // PUSH1 0x00 (argsOffset)
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            // PUSH1 value
            bytes.AddRange(new byte[] { 0x60, (byte)value });
            // PUSH20 address
            bytes.Add(0x73);
            bytes.AddRange(targetAddress.HexToByteArray());
            // GAS
            bytes.Add(0x5A);
            // CALL
            bytes.Add(0xF1);
            // POP
            bytes.Add(0x50);
            // STOP
            bytes.Add(0x00);

            return bytes.ToArray();
        }

        #endregion
    }

    public class InMemoryNodeDataService : INodeDataService
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

        public Task<BigInteger> GetBalanceAsync(string address)
        {
            _balances.TryGetValue(address, out var balance);
            return Task.FromResult(balance);
        }

        public Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            return GetBalanceAsync("0x" + address.ToHex());
        }

        public Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            if (_storage.TryGetValue(address, out var slots))
            {
                if (slots.TryGetValue(position, out var value))
                {
                    return Task.FromResult(value);
                }
            }
            return Task.FromResult(new byte[32]);
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            return GetStorageAtAsync("0x" + address.ToHex(), position);
        }

        public Task<BigInteger> GetTransactionCount(string address)
        {
            _nonces.TryGetValue(address, out var nonce);
            return Task.FromResult(nonce);
        }

        public Task<BigInteger> GetTransactionCount(byte[] address)
        {
            return GetTransactionCount("0x" + address.ToHex());
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

        public Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            return Task.FromResult(new byte[32]);
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            return Task.FromResult(_code.ContainsKey(address) || _balances.ContainsKey(address));
        }
    }
}
