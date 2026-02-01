using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class ERC7562TracingInterceptor
    {
        private readonly ERC7562ValidationContext _context;
        private readonly ERC7562RuleEnforcer _enforcer;
        private readonly List<ERC7562Violation> _violations = new();

        private Instruction? _previousOpcode;
        private int _currentProgramCounter;

        public IReadOnlyList<ERC7562Violation> Violations => _violations;
        public bool HasViolations => _violations.Count > 0;

        public ERC7562TracingInterceptor(ERC7562ValidationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _enforcer = new ERC7562RuleEnforcer();
        }

        public void OnOpcodeExecution(Instruction opcode, string executingAddress, int depth, int programCounter)
        {
            _currentProgramCounter = programCounter;
            _context.CallDepth = depth;
            _context.UpdateCurrentEntity(executingAddress);

            var nextOpcode = _previousOpcode.HasValue ? opcode : (Instruction?)null;
            if (_previousOpcode.HasValue)
            {
                var violation = _enforcer.ValidateOpcode(_previousOpcode.Value, opcode, _context);
                if (violation != null)
                {
                    _violations.Add(violation);
                    _context.Violations.Add(violation);
                }
            }

            _context.OpcodeExecutions.Add(new OpcodeExecution
            {
                Opcode = opcode,
                ExecutedAt = executingAddress,
                Depth = depth,
                ExecutedBy = _context.CurrentEntity,
                ProgramCounter = programCounter
            });

            _context.AccessedAddresses.Add(executingAddress.ToLowerInvariant());
            _previousOpcode = opcode;
        }

        public void OnStorageAccess(string contractAddress, BigInteger slot, bool isWrite, bool isTransient = false)
        {
            var violation = _enforcer.ValidateStorageAccess(contractAddress, slot, isWrite, isTransient, _context);
            if (violation != null)
            {
                _violations.Add(violation);
                _context.Violations.Add(violation);
            }

            _context.StorageAccesses.Add(new StorageSlotAccess
            {
                ContractAddress = contractAddress,
                Slot = slot,
                IsWrite = isWrite,
                IsTransient = isTransient,
                AccessedBy = _context.CurrentEntity,
                Depth = _context.CallDepth
            });
        }

        public void OnCall(string from, string to, BigInteger value, byte[] data, int depth)
        {
            var isPrecompile = IsPrecompileAddress(to);

            if (isPrecompile)
            {
                var precompileAddr = ParsePrecompileAddress(to);
                var violation = _enforcer.ValidatePrecompileCall(precompileAddr, _context);
                if (violation != null)
                {
                    _violations.Add(violation);
                    _context.Violations.Add(violation);
                    return;
                }
            }

            var callViolation = _enforcer.ValidateCall(from, to, value, data, _context);
            if (callViolation != null)
            {
                _violations.Add(callViolation);
                _context.Violations.Add(callViolation);
            }

            _context.Calls.Add(new CallInfo
            {
                From = from,
                To = to ?? "",
                Value = value,
                Data = data ?? Array.Empty<byte>(),
                Depth = depth,
                CalledBy = _context.CurrentEntity
            });

            if (!string.IsNullOrEmpty(to))
            {
                _context.AccessedAddresses.Add(to.ToLowerInvariant());
            }
        }

        public void OnExtCodeAccess(Instruction opcode, string targetAddress, bool hasCode)
        {
            var violation = _enforcer.ValidateExtCodeOpcode(opcode, targetAddress, hasCode, _context);
            if (violation != null)
            {
                _violations.Add(violation);
                _context.Violations.Add(violation);
            }

            _context.AccessedAddresses.Add(targetAddress.ToLowerInvariant());
        }

        public void OnCreate(string creatorAddress, string createdAddress, bool isCreate2)
        {
            var opcode = isCreate2 ? Instruction.CREATE2 : Instruction.CREATE;

            var violation = _enforcer.ValidateOpcode(opcode, null, _context);
            if (violation != null)
            {
                _violations.Add(violation);
                _context.Violations.Add(violation);
            }

            if (isCreate2 && !string.IsNullOrEmpty(createdAddress))
            {
                if (ERC7562ValidationContext.AddressEquals(createdAddress, _context.Sender?.Address))
                {
                    _context.DeployedSenderAddress = createdAddress;
                }
            }

            _context.AccessedAddresses.Add(creatorAddress.ToLowerInvariant());
            if (!string.IsNullOrEmpty(createdAddress))
            {
                _context.AccessedAddresses.Add(createdAddress.ToLowerInvariant());
            }
        }

        public void OnKeccak256(byte[] data, BigInteger resultHash)
        {
            if (data == null || data.Length < 64) return;

            var potentialAddress = TryExtractAddress(data);
            if (!string.IsNullOrEmpty(potentialAddress))
            {
                _context.KeccakPreimages[resultHash] = potentialAddress;

                var senderAddr = _context.Sender?.Address?.ToLowerInvariant();
                if (potentialAddress.ToLowerInvariant() == senderAddr)
                {
                    _context.TrackAssociatedSlot(senderAddr, resultHash);
                }
            }
        }

        public void FinalizeValidation()
        {
            if (_previousOpcode.HasValue)
            {
                var violation = _enforcer.ValidateOpcode(_previousOpcode.Value, null, _context);
                if (violation != null)
                {
                    _violations.Add(violation);
                    _context.Violations.Add(violation);
                }
            }
        }

        public ERC7562ValidationResult GetResult()
        {
            return new ERC7562ValidationResult
            {
                IsValid = !HasViolations,
                Violations = new List<ERC7562Violation>(_violations),
                StorageAccesses = new List<StorageSlotAccess>(_context.StorageAccesses),
                OpcodeExecutions = new List<OpcodeExecution>(_context.OpcodeExecutions),
                Calls = new List<CallInfo>(_context.Calls),
                AccessedAddresses = new HashSet<string>(_context.AccessedAddresses)
            };
        }

        private static bool IsPrecompileAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;

            var addr = address.ToLowerInvariant().RemoveHexPrefix();
            if (addr.Length < 40) addr = addr.PadLeft(40, '0');

            for (int i = 0; i < 38; i++)
            {
                if (addr[i] != '0') return false;
            }

            var lastTwo = addr.Substring(38);
            if (int.TryParse(lastTwo, System.Globalization.NumberStyles.HexNumber, null, out int val))
            {
                return val >= 1 && val <= 0x0A;
            }
            return false;
        }

        private static int ParsePrecompileAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return 0;

            var addr = address.ToLowerInvariant().RemoveHexPrefix();
            if (addr.Length < 40) addr = addr.PadLeft(40, '0');

            var lastTwo = addr.Substring(38);
            if (int.TryParse(lastTwo, System.Globalization.NumberStyles.HexNumber, null, out int val))
            {
                return val;
            }
            return 0;
        }

        private static string TryExtractAddress(byte[] data)
        {
            if (data == null || data.Length < 32) return null;

            var first12Bytes = new byte[12];
            Array.Copy(data, 0, first12Bytes, 0, Math.Min(12, data.Length));

            bool allZero = true;
            for (int i = 0; i < first12Bytes.Length; i++)
            {
                if (first12Bytes[i] != 0) { allZero = false; break; }
            }

            if (allZero && data.Length >= 32)
            {
                var addressBytes = new byte[20];
                Array.Copy(data, 12, addressBytes, 0, 20);
                return "0x" + addressBytes.ToHex();
            }

            return null;
        }
    }

    public class ERC7562ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ERC7562Violation> Violations { get; set; } = new();
        public List<StorageSlotAccess> StorageAccesses { get; set; } = new();
        public List<OpcodeExecution> OpcodeExecutions { get; set; } = new();
        public List<CallInfo> Calls { get; set; } = new();
        public HashSet<string> AccessedAddresses { get; set; } = new();
    }
}
