using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class ERC7562TracingInterceptor
    {
        private readonly ERC7562ValidationContext _context;
        private readonly ERC7562RuleEnforcer _enforcer;
        private readonly HashSet<int> _precompilesActiveAtThisFork;

        private Instruction? _previousOpcode;
        private int _currentProgramCounter;

        private ERC7562Violation? _pendingEntryPointExtCodeSizeViolation;

        public IReadOnlyList<ERC7562Violation> Violations => _context.Violations;
        public bool HasViolations => _context.HasViolations;

        public ERC7562TracingInterceptor(ERC7562ValidationContext context)
            : this(context, null)
        {
        }

        /// <summary>
        /// ERC-7562 [OP-041] forbids calling an address without deployed code, and a precompile
        /// has none - so the exemption must name the precompiles active AT THE SIMULATED FORK.
        /// A fixed range exempts addresses that are ordinary empty accounts at an earlier fork,
        /// and rejects real precompiles added at a later one.
        /// </summary>
        public ERC7562TracingInterceptor(
            ERC7562ValidationContext context, IEnumerable<int> precompilesActiveAtThisFork)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _enforcer = new ERC7562RuleEnforcer();
            _precompilesActiveAtThisFork = precompilesActiveAtThisFork == null
                ? null
                : new HashSet<int>(precompilesActiveAtThisFork);
        }

        public void OnOpcodeExecution(Instruction opcode, string executingAddress, int depth, int programCounter)
        {
            _currentProgramCounter = programCounter;
            _context.CallDepth = depth;
            _context.UpdateCurrentEntity(executingAddress);

            if (_pendingEntryPointExtCodeSizeViolation != null)
            {
                if (opcode != Instruction.ISZERO)
                {
                    _context.Violations.Add(_pendingEntryPointExtCodeSizeViolation);
                }
                _pendingEntryPointExtCodeSizeViolation = null;
            }

            if (_previousOpcode.HasValue &&
                _previousOpcode.Value != Instruction.CREATE &&
                _previousOpcode.Value != Instruction.CREATE2)
            {
                var violation = _enforcer.ValidateOpcode(_previousOpcode.Value, opcode, _context);
                if (violation != null)
                {
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

        public void OnOutOfGas()
        {
            var violation = _enforcer.ValidateOutOfGas(_context);
            _context.Violations.Add(violation);
        }

        public void OnStorageAccess(string contractAddress, BigInteger slot, bool isWrite, bool isTransient = false)
        {
            var violation = _enforcer.ValidateStorageAccess(contractAddress, slot, isWrite, isTransient, _context);
            if (violation != null)
            {
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

        public void OnCall(string from, string to, BigInteger value, byte[] data, int depth, bool hasCode = true)
        {
            var isPrecompile = IsPrecompileAddress(to);

            if (isPrecompile)
            {
                var precompileAddr = ParsePrecompileAddress(to);
                var violation = _enforcer.ValidatePrecompileCall(precompileAddr, _context);
                if (violation != null)
                {
                    _context.Violations.Add(violation);
                    return;
                }
            }
            else
            {
                var codeViolation = _enforcer.ValidateCodeAccess(to, hasCode, _context);
                if (codeViolation != null)
                {
                    _context.Violations.Add(codeViolation);
                    return;
                }
            }

            var callViolation = _enforcer.ValidateCall(from, to, value, data, _context);
            if (callViolation != null)
            {
                _context.Violations.Add(callViolation);
            }
            else if (ERC7562ValidationContext.AddressEquals(to, _context.EntryPointAddress))
            {
                _context.EnterPermittedEntryPointCall(depth + 1);
            }

            _context.Calls.Add(new TracedCall
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

            if (opcode == Instruction.EXTCODESIZE && violation?.Rule == "OP-054")
            {
                _pendingEntryPointExtCodeSizeViolation = violation;
            }
            else if (violation != null)
            {
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

        public void FinalizeValidation()
        {
            if (_pendingEntryPointExtCodeSizeViolation != null)
            {
                _context.Violations.Add(_pendingEntryPointExtCodeSizeViolation);
                _pendingEntryPointExtCodeSizeViolation = null;
            }

            if (_previousOpcode.HasValue &&
                _previousOpcode.Value != Instruction.CREATE &&
                _previousOpcode.Value != Instruction.CREATE2)
            {
                var violation = _enforcer.ValidateOpcode(_previousOpcode.Value, null, _context);
                if (violation != null)
                {
                    _context.Violations.Add(violation);
                }
            }
        }

        public ERC7562ValidationResult GetResult()
        {
            return new ERC7562ValidationResult
            {
                IsValid = !HasViolations,
                Violations = new List<ERC7562Violation>(_context.Violations),
                StorageAccesses = new List<StorageSlotAccess>(_context.StorageAccesses),
                OpcodeExecutions = new List<OpcodeExecution>(_context.OpcodeExecutions),
                Calls = new List<TracedCall>(_context.Calls),
                AccessedAddresses = new HashSet<string>(_context.AccessedAddresses)
            };
        }

        private bool IsPrecompileAddress(string address)
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
                return _precompilesActiveAtThisFork == null
                    ? val >= 1 && val <= 0x0A
                    : _precompilesActiveAtThisFork.Contains(val);
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

    }

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "ERC7562ValidationResult - the verdict plus the traced opcodes, storage and calls")]
    public class ERC7562ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ERC7562Violation> Violations { get; set; } = new();
        public List<StorageSlotAccess> StorageAccesses { get; set; } = new();
        public List<OpcodeExecution> OpcodeExecutions { get; set; } = new();
        public List<TracedCall> Calls { get; set; } = new();
        public HashSet<string> AccessedAddresses { get; set; } = new();
    }
}
