using System.Linq;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class ERC7562RuleEnforcer
    {
        private const string DepositToSelector = "b760faf9";
        private const string IncrementNonceSelector = "0bd28e3b";

        public ERC7562Violation? ValidateOpcode(
            Instruction opcode,
            Instruction? nextOpcode,
            ERC7562ValidationContext context)
        {
            // [OP-013] Unassigned opcodes are always forbidden
            if (!ForbiddenOpcodes.IsValidOpcode(opcode))
            {
                return ERC7562Violation.FromOpcode("OP-013", $"Unassigned opcode: {opcode}", opcode, context);
            }

            // [OP-011] Always forbidden opcodes
            if (ForbiddenOpcodes.IsAlwaysForbidden(opcode))
            {
                return ERC7562Violation.FromOpcode("OP-011", $"Forbidden opcode during validation: {opcode}", opcode, context);
            }

            // [OP-080] Staked-only opcodes (BALANCE, SELFBALANCE)
            if (ForbiddenOpcodes.RequiresStaking(opcode))
            {
                var violation = ValidateStakedOnlyOpcode(opcode, context);
                if (violation != null) return violation;
            }

            // [OP-012] GAS opcode must be followed by CALL
            if (opcode == Instruction.GAS)
            {
                var violation = ValidateGasOpcode(nextOpcode, context);
                if (violation != null) return violation;
            }

            // [OP-031] CREATE2 restrictions
            if (opcode == Instruction.CREATE2)
            {
                var violation = ValidateCreate2(context);
                if (violation != null) return violation;
            }

            // [OP-032] CREATE restrictions
            if (opcode == Instruction.CREATE)
            {
                var violation = ValidateCreate(context);
                if (violation != null) return violation;
            }

            return null;
        }

        public ERC7562Violation? ValidateStorageAccess(
            string address,
            BigInteger slot,
            bool isWrite,
            bool isTransient,
            ERC7562ValidationContext context)
        {
            var entityInfo = context.GetCurrentEntityInfo();

            // [STO-010] Sender can always access own storage
            if (context.CurrentEntity == EntityType.Sender &&
                ERC7562ValidationContext.AddressEquals(address, context.Sender?.Address))
            {
                return null;
            }

            // Entity accessing its own storage
            if (entityInfo != null && ERC7562ValidationContext.AddressEquals(address, entityInfo.Address))
            {
                if (entityInfo.IsStaked)
                {
                    return null;
                }
            }

            // [STO-021, STO-022] Associated storage rules
            if (context.IsAssociatedSlot(address, slot))
            {
                if (context.Factory?.IsStaked == true || !context.IsDeploymentPhase)
                {
                    return null;
                }
            }

            // [STO-031, STO-032, STO-033] Staked entity privileges
            if (entityInfo?.IsStaked == true)
            {
                // [STO-033] Staked can read any non-entity storage
                if (!isWrite && !context.IsEntityAddress(address))
                {
                    return null;
                }

                // [STO-031, STO-032] Staked can R/W associated storage
                if (context.IsAssociatedSlot(address, slot))
                {
                    return null;
                }
            }

            // Check if accessing EntryPoint
            if (ERC7562ValidationContext.AddressEquals(address, context.EntryPointAddress))
            {
                return ERC7562Violation.FromStorage("STO-010", "Direct EntryPoint storage access not allowed", address, slot, context);
            }

            var rule = isWrite ? "STO-032" : "STO-031";
            var storageType = isTransient ? "transient storage" : "storage";
            return ERC7562Violation.FromStorage(rule, $"Unauthorized {storageType} {(isWrite ? "write" : "read")}: contract={address}, slot={slot}", address, slot, context);
        }

        public ERC7562Violation? ValidateCall(
            string from,
            string? target,
            BigInteger value,
            byte[]? data,
            ERC7562ValidationContext context)
        {
            // [OP-042] Exception: access to sender address allowed during deployment
            if (ERC7562ValidationContext.AddressEquals(target, context.Sender?.Address) && context.IsDeploymentPhase)
            {
                return null;
            }

            // [OP-061] CALL with value forbidden except to EntryPoint
            if (value > 0)
            {
                if (!ERC7562ValidationContext.AddressEquals(target, context.EntryPointAddress))
                {
                    return ERC7562Violation.FromCall("OP-061", $"CALL with value ({value}) only allowed to EntryPoint", target ?? "", context);
                }
            }

            // [OP-051 through OP-055] EntryPoint call restrictions
            if (ERC7562ValidationContext.AddressEquals(target, context.EntryPointAddress))
            {
                var violation = ValidateEntryPointCall(from, data, context);
                if (violation != null) return violation;
            }

            return null;
        }

        public ERC7562Violation? ValidateCodeAccess(
            string target,
            bool hasCode,
            ERC7562ValidationContext context)
        {
            // [OP-041] Access to address without code is forbidden for EXTCODE* and *CALL
            if (!hasCode)
            {
                // [OP-042] Exception: sender address during deployment
                if (ERC7562ValidationContext.AddressEquals(target, context.Sender?.Address) && context.IsDeploymentPhase)
                {
                    return null;
                }

                return ERC7562Violation.FromCall("OP-041", $"Access to address without deployed code: {target}", target, context);
            }

            return null;
        }

        public ERC7562Violation? ValidatePrecompileCall(
            int precompileAddress,
            ERC7562ValidationContext context)
        {
            // [OP-062] Only known precompiles allowed
            if (!ForbiddenOpcodes.IsAllowedPrecompile(precompileAddress, context.AllowRip7212Precompile))
            {
                return new ERC7562Violation
                {
                    Rule = "OP-062",
                    Message = $"Precompile not allowed: 0x{precompileAddress:X}",
                    Address = $"0x{precompileAddress:X40}",
                    Entity = context.CurrentEntity
                };
            }

            return null;
        }

        public ERC7562Violation? ValidateExtCodeOpcode(
            Instruction opcode,
            string targetAddress,
            bool hasCode,
            ERC7562ValidationContext context)
        {
            // [OP-041] EXTCODE* to address without code is forbidden
            if (!hasCode)
            {
                // [OP-042] Exception: sender during deployment
                if (ERC7562ValidationContext.AddressEquals(targetAddress, context.Sender?.Address) && context.IsDeploymentPhase)
                {
                    return null;
                }

                return ERC7562Violation.FromOpcode("OP-041", $"EXTCODE access to address without code: {targetAddress}", opcode, context);
            }

            return null;
        }

        private ERC7562Violation? ValidateStakedOnlyOpcode(Instruction opcode, ERC7562ValidationContext context)
        {
            var entityInfo = context.GetCurrentEntityInfo();

            if (entityInfo == null || !entityInfo.IsStaked)
            {
                return ERC7562Violation.FromOpcode("OP-080", $"{opcode} opcode requires staked entity", opcode, context);
            }

            return null;
        }

        private ERC7562Violation? ValidateGasOpcode(Instruction? nextOpcode, ERC7562ValidationContext context)
        {
            // [OP-012] GAS is allowed only if immediately followed by *CALL
            if (!nextOpcode.HasValue || !ForbiddenOpcodes.IsCallOpcode(nextOpcode.Value))
            {
                return ERC7562Violation.FromOpcode("OP-012", "GAS opcode must be immediately followed by CALL/STATICCALL/DELEGATECALL/CALLCODE", Instruction.GAS, context);
            }

            return null;
        }

        private ERC7562Violation? ValidateCreate2(ERC7562ValidationContext context)
        {
            var entityInfo = context.GetCurrentEntityInfo();

            // Staked entities can use CREATE2 freely
            if (entityInfo?.IsStaked == true)
            {
                return null;
            }

            // [OP-031] Unstaked: CREATE2 allowed exactly once to deploy sender
            if (context.Create2Count > 0)
            {
                return ERC7562Violation.FromOpcode("OP-031", "CREATE2 already used - unstaked entity can only use it once for sender deployment", Instruction.CREATE2, context);
            }

            // Must be in deployment phase (factory context)
            if (!context.IsDeploymentPhase && context.CurrentEntity != EntityType.Factory)
            {
                return ERC7562Violation.FromOpcode("OP-031", "CREATE2 only allowed in deployment phase by factory", Instruction.CREATE2, context);
            }

            context.Create2Count++;
            return null;
        }

        private ERC7562Violation? ValidateCreate(ERC7562ValidationContext context)
        {
            var entityInfo = context.GetCurrentEntityInfo();

            // [EREP-060, EREP-061] Staked factory can use CREATE
            if (context.Factory?.IsStaked == true)
            {
                return null;
            }

            // [OP-032] CREATE allowed if factory exists (even unstaked) and called from sender
            if (context.Factory != null && context.CurrentEntity == EntityType.Sender)
            {
                return null;
            }

            // Staked sender can use CREATE
            if (context.CurrentEntity == EntityType.Sender && entityInfo?.IsStaked == true)
            {
                return null;
            }

            return ERC7562Violation.FromOpcode("OP-032", "CREATE not allowed: requires staked entity or factory-deployed sender", Instruction.CREATE, context);
        }

        private ERC7562Violation? ValidateEntryPointCall(
            string from,
            byte[]? data,
            ERC7562ValidationContext context)
        {
            if (data == null || data.Length < 4)
            {
                // Fallback call (no selector) - [OP-053] allowed from sender only
                if (context.CurrentEntity != EntityType.Sender)
                {
                    return ERC7562Violation.FromCall("OP-053", "EntryPoint fallback call only allowed from sender", context.EntryPointAddress, context);
                }
                return null;
            }

            var selector = data.Take(4).ToArray().ToHex();

            // [OP-052] depositTo(address) - allowed from sender or factory
            if (selector == DepositToSelector)
            {
                if (context.CurrentEntity != EntityType.Sender &&
                    context.CurrentEntity != EntityType.Factory)
                {
                    return ERC7562Violation.FromCall("OP-052", "EntryPoint.depositTo only allowed from sender or factory", context.EntryPointAddress, context);
                }
                return null;
            }

            // [OP-054] incrementNonce(uint192) - allowed from sender only
            if (selector == IncrementNonceSelector)
            {
                if (context.CurrentEntity != EntityType.Sender)
                {
                    return ERC7562Violation.FromCall("OP-054", "EntryPoint.incrementNonce only allowed from sender", context.EntryPointAddress, context);
                }
                return null;
            }

            // [OP-055] Any other access to EntryPoint is forbidden
            return ERC7562Violation.FromCall("OP-055", $"Unauthorized EntryPoint method call: 0x{selector}", context.EntryPointAddress, context);
        }
    }
}
