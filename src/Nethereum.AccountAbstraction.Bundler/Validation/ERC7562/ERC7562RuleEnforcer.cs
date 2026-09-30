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
            if (!ForbiddenOpcodes.IsValidOpcode(opcode))
            {
                return ERC7562Violation.FromOpcode("OP-013", $"Unassigned opcode: {opcode}", opcode, context);
            }

            if (ForbiddenOpcodes.IsAlwaysForbidden(opcode))
            {
                return ERC7562Violation.FromOpcode("OP-011", $"{context.CurrentEntity.ToErc7562EntityName()} uses banned opcode: {opcode}", opcode, context);
            }

            if (ForbiddenOpcodes.RequiresStaking(opcode))
            {
                var violation = ValidateStakedOnlyOpcode(opcode, context);
                if (violation != null) return violation;
            }

            if (opcode == Instruction.GAS)
            {
                var violation = ValidateGasOpcode(nextOpcode, context);
                if (violation != null) return violation;
            }

            if (opcode == Instruction.CREATE2)
            {
                var violation = ValidateCreate2(context);
                if (violation != null) return violation;
            }

            if (opcode == Instruction.CREATE)
            {
                var violation = ValidateCreate(context);
                if (violation != null) return violation;
            }

            return null;
        }

        public ERC7562Violation ValidateOutOfGas(ERC7562ValidationContext context)
        {
            return ERC7562Violation.FromOpcode("OP-020", $"{context.CurrentEntity.ToErc7562EntityName()} out-of-gas during validation is forbidden", Instruction.GAS, context);
        }

        public ERC7562Violation? ValidateStorageAccess(
            string address,
            BigInteger slot,
            bool isWrite,
            bool isTransient,
            ERC7562ValidationContext context)
        {
            var entity = context.GetCurrentEntity();

            if (context.CurrentEntity == EntityType.Sender &&
                ERC7562ValidationContext.AddressEquals(address, context.Sender?.Address))
            {
                return null;
            }

            if (entity != null && ERC7562ValidationContext.AddressEquals(address, entity.Address))
            {
                if (entity.IsStaked)
                {
                    return null;
                }
            }

            if (entity?.IsStaked == true && context.IsEntityOwnAssociatedSlot(address, slot))
            {
                return null;
            }

            var isEntityOwnContract = entity != null && ERC7562ValidationContext.AddressEquals(address, entity.Address);
            if (!isEntityOwnContract && context.IsAssociatedSlot(address, slot))
            {
                if (context.Factory?.IsStaked == true || !context.HasDeployingFactory)
                {
                    return null;
                }
            }

            if (entity?.IsStaked == true)
            {
                if (!isWrite && !context.IsEntityAddress(address))
                {
                    return null;
                }

                if (context.IsAssociatedSlot(address, slot))
                {
                    return null;
                }
            }

            if (ERC7562ValidationContext.AddressEquals(address, context.EntryPointAddress))
            {
                if (context.IsInsidePermittedEntryPointCall(context.CallDepth))
                {
                    return null;
                }

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
            if (ERC7562ValidationContext.AddressEquals(target, context.Sender?.Address) && context.IsDeploymentPhase)
            {
                return null;
            }

            if (value > 0)
            {
                if (!ERC7562ValidationContext.AddressEquals(target, context.EntryPointAddress))
                {
                    return ERC7562Violation.FromCall("OP-061", $"CALL with value ({value}) only allowed to EntryPoint", target ?? "", context);
                }
            }

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
            if (!hasCode)
            {
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
            if (ERC7562ValidationContext.AddressEquals(targetAddress, context.EntryPointAddress))
            {
                return ERC7562Violation.FromOpcode("OP-054", $"EXTCODE access to the EntryPoint is forbidden: {targetAddress}", opcode, context);
            }

            if (!hasCode)
            {
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
            var entity = context.GetCurrentEntity();

            if (entity == null || !entity.IsStaked)
            {
                return ERC7562Violation.FromOpcode("OP-080", $"unstaked {context.CurrentEntity.ToErc7562EntityName()} uses banned opcode: {opcode}", opcode, context);
            }

            return null;
        }

        private ERC7562Violation? ValidateGasOpcode(Instruction? nextOpcode, ERC7562ValidationContext context)
        {
            if (!nextOpcode.HasValue || !ForbiddenOpcodes.IsCallOpcode(nextOpcode.Value))
            {
                return ERC7562Violation.FromOpcode("OP-012", $"{context.CurrentEntity.ToErc7562EntityName()} uses banned opcode: GAS", Instruction.GAS, context);
            }

            return null;
        }

        private ERC7562Violation? ValidateCreate2(ERC7562ValidationContext context)
        {
            if (context.Factory?.IsStaked == true &&
                (ERC7562ValidationContext.AddressEquals(context.CurrentAddress, context.Sender?.Address) ||
                 ERC7562ValidationContext.AddressEquals(context.CurrentAddress, context.Factory?.Address)))
            {
                return null;
            }

            if (context.Create2Count > 0)
            {
                return ERC7562Violation.FromOpcode("OP-031", $"{context.CurrentEntity.ToErc7562EntityName()} uses banned opcode: CREATE2", Instruction.CREATE2, context);
            }

            if (!context.IsDeploymentPhase && context.CurrentEntity != EntityType.Factory)
            {
                return ERC7562Violation.FromOpcode("OP-031", $"{context.CurrentEntity.ToErc7562EntityName()} uses banned opcode: CREATE2", Instruction.CREATE2, context);
            }

            context.Create2Count++;
            return null;
        }

        private ERC7562Violation? ValidateCreate(ERC7562ValidationContext context)
        {
            var entity = context.GetCurrentEntity();

            if (context.Factory?.IsStaked == true &&
                (ERC7562ValidationContext.AddressEquals(context.CurrentAddress, context.Sender?.Address) ||
                 ERC7562ValidationContext.AddressEquals(context.CurrentAddress, context.Factory?.Address)))
            {
                return null;
            }

            if (context.Factory != null &&
                ERC7562ValidationContext.AddressEquals(context.CurrentAddress, context.Sender?.Address))
            {
                return null;
            }

            if (context.CurrentEntity == EntityType.Sender && entity?.IsStaked == true)
            {
                return null;
            }

            return ERC7562Violation.FromOpcode("OP-032", $"{context.CurrentEntity.ToErc7562EntityName()} uses banned opcode: CREATE", Instruction.CREATE, context);
        }

        private ERC7562Violation? ValidateEntryPointCall(
            string from,
            byte[]? data,
            ERC7562ValidationContext context)
        {
            if (data == null || data.Length < 4)
            {
                if (context.CurrentEntity != EntityType.Sender)
                {
                    return ERC7562Violation.FromCall("OP-053", "EntryPoint fallback call only allowed from sender", context.EntryPointAddress, context);
                }
                return null;
            }

            var selector = data.Take(4).ToArray().ToHex();

            if (selector == DepositToSelector)
            {
                if (context.CurrentEntity != EntityType.Sender &&
                    context.CurrentEntity != EntityType.Factory)
                {
                    return ERC7562Violation.FromCall("OP-052", "EntryPoint.depositTo only allowed from sender or factory", context.EntryPointAddress, context);
                }
                return null;
            }

            if (selector == IncrementNonceSelector)
            {
                if (context.CurrentEntity != EntityType.Sender)
                {
                    return ERC7562Violation.FromCall("OP-054", "EntryPoint.incrementNonce only allowed from sender", context.EntryPointAddress, context);
                }
                return null;
            }

            return ERC7562Violation.FromCall("OP-055", $"Unauthorized EntryPoint method call: 0x{selector}", context.EntryPointAddress, context);
        }
    }
}
