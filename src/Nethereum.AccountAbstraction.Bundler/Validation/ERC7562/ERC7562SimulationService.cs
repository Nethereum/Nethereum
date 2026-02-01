using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class ERC7562SimulationService
    {
        private readonly INodeDataService _nodeDataService;
        private readonly TransactionExecutor _executor;
        private readonly HardforkConfig _hardforkConfig;

        public ERC7562SimulationService(INodeDataService nodeDataService, HardforkConfig hardforkConfig = null)
        {
            _nodeDataService = nodeDataService ?? throw new ArgumentNullException(nameof(nodeDataService));
            _hardforkConfig = hardforkConfig ?? HardforkConfig.Default;
            _executor = new TransactionExecutor(_hardforkConfig);
        }

        public async Task<ERC7562ValidationResult> ValidateUserOperationAsync(
            PackedUserOperationDTO userOp,
            string entryPointAddress,
            EntityInfo sender,
            EntityInfo factory = null,
            EntityInfo paymaster = null,
            EntityInfo aggregator = null,
            long blockNumber = -1,
            long timestamp = -1,
            string coinbase = null,
            BigInteger chainId = default)
        {
            var context = ERC7562ValidationContext.Create(
                entryPointAddress,
                sender,
                factory,
                paymaster,
                aggregator);

            var interceptor = new ERC7562TracingInterceptor(context);
            var associatedStorage = new AssociatedStorageCalculator();

            if (!string.IsNullOrEmpty(sender?.Address))
            {
                for (int i = 0; i < 10; i++)
                {
                    associatedStorage.RegisterSenderSlot(sender.Address, i);
                }
            }

            try
            {
                await SimulateValidationPhaseAsync(userOp, entryPointAddress, context, interceptor, associatedStorage, blockNumber, timestamp, coinbase, chainId);
            }
            catch (Exception ex)
            {
                context.AddViolation("SIMULATION_ERROR", $"Simulation failed: {ex.Message}");
            }

            interceptor.FinalizeValidation();
            return interceptor.GetResult();
        }

        public async Task<UserOpStorageProfile> GetStorageProfileAsync(
            PackedUserOperationDTO userOp,
            string entryPointAddress,
            EntityInfo sender,
            EntityInfo factory = null,
            EntityInfo paymaster = null,
            long blockNumber = -1,
            long timestamp = -1,
            string coinbase = null,
            BigInteger chainId = default)
        {
            var context = ERC7562ValidationContext.Create(
                entryPointAddress,
                sender,
                factory,
                paymaster,
                null);

            var interceptor = new ERC7562TracingInterceptor(context);
            var associatedStorage = new AssociatedStorageCalculator();

            try
            {
                await SimulateValidationPhaseAsync(userOp, entryPointAddress, context, interceptor, associatedStorage, blockNumber, timestamp, coinbase, chainId);
            }
            catch
            {
            }

            var profile = new UserOpStorageProfile
            {
                SenderAddress = sender?.Address ?? userOp.Sender,
                Factory = factory?.Address,
                Paymaster = paymaster?.Address
            };

            foreach (var access in context.StorageAccesses)
            {
                var slotKey = new StorageSlotKey(access.ContractAddress, access.Slot);
                if (access.IsWrite)
                {
                    profile.WriteSlots.Add(slotKey);
                }
                else
                {
                    profile.ReadSlots.Add(slotKey);
                }
            }

            foreach (var addr in context.AccessedAddresses)
            {
                profile.AccessedContracts.Add(addr);
            }

            return profile;
        }

        private async Task SimulateValidationPhaseAsync(
            PackedUserOperationDTO userOp,
            string entryPointAddress,
            ERC7562ValidationContext context,
            ERC7562TracingInterceptor interceptor,
            AssociatedStorageCalculator associatedStorage,
            long blockNumber,
            long timestamp,
            string coinbase,
            BigInteger chainId)
        {
            var executionState = new ExecutionStateService(_nodeDataService);

            if (context.Factory != null && !string.IsNullOrEmpty(context.Factory.Address))
            {
                context.CurrentEntity = EntityType.Factory;
                context.IsDeploymentPhase = true;

                var initCode = userOp.InitCode;
                if (initCode != null && initCode.Length >= 20)
                {
                    var factoryAddress = "0x" + initCode.Take(20).ToArray().ToHex();
                    var factoryData = initCode.Skip(20).ToArray();

                    await SimulateWithTransactionExecutorAsync(
                        executionState,
                        entryPointAddress,
                        factoryAddress,
                        factoryData,
                        BigInteger.Zero,
                        context,
                        interceptor,
                        associatedStorage,
                        blockNumber,
                        timestamp,
                        coinbase,
                        chainId);
                }

                context.IsDeploymentPhase = false;
            }

            context.CurrentEntity = EntityType.Sender;
            await SimulateSenderValidationAsync(
                executionState,
                userOp,
                entryPointAddress,
                context,
                interceptor,
                associatedStorage,
                blockNumber,
                timestamp,
                coinbase,
                chainId);

            if (context.Paymaster != null && !string.IsNullOrEmpty(context.Paymaster.Address))
            {
                context.CurrentEntity = EntityType.Paymaster;
                await SimulatePaymasterValidationAsync(
                    executionState,
                    userOp,
                    entryPointAddress,
                    context,
                    interceptor,
                    associatedStorage,
                    blockNumber,
                    timestamp,
                    coinbase,
                    chainId);
            }
        }

        private async Task SimulateSenderValidationAsync(
            ExecutionStateService executionState,
            PackedUserOperationDTO userOp,
            string entryPointAddress,
            ERC7562ValidationContext context,
            ERC7562TracingInterceptor interceptor,
            AssociatedStorageCalculator associatedStorage,
            long blockNumber,
            long timestamp,
            string coinbase,
            BigInteger chainId)
        {
            var senderAddress = userOp.Sender;
            var senderCode = await executionState.GetCodeAsync(senderAddress);

            if (senderCode == null || senderCode.Length == 0)
            {
                if (userOp.InitCode == null || userOp.InitCode.Length == 0)
                {
                    context.AddViolation("AA20", $"Sender {senderAddress} has no code and no initCode provided");
                    return;
                }
            }

            var validateUserOpSelector = "3a871cdd";
            var callData = BuildValidateUserOpCallData(validateUserOpSelector, userOp);

            await SimulateWithTransactionExecutorAsync(
                executionState,
                entryPointAddress,
                senderAddress,
                callData,
                BigInteger.Zero,
                context,
                interceptor,
                associatedStorage,
                blockNumber,
                timestamp,
                coinbase,
                chainId);
        }

        private async Task SimulatePaymasterValidationAsync(
            ExecutionStateService executionState,
            PackedUserOperationDTO userOp,
            string entryPointAddress,
            ERC7562ValidationContext context,
            ERC7562TracingInterceptor interceptor,
            AssociatedStorageCalculator associatedStorage,
            long blockNumber,
            long timestamp,
            string coinbase,
            BigInteger chainId)
        {
            var paymasterAddress = context.Paymaster.Address;
            var paymasterCode = await executionState.GetCodeAsync(paymasterAddress);

            if (paymasterCode == null || paymasterCode.Length == 0)
            {
                context.AddViolation("AA30", $"Paymaster {paymasterAddress} has no code deployed");
                return;
            }

            var validatePaymasterSelector = "f465c77e";
            var callData = BuildValidatePaymasterCallData(validatePaymasterSelector, userOp);

            await SimulateWithTransactionExecutorAsync(
                executionState,
                entryPointAddress,
                paymasterAddress,
                callData,
                BigInteger.Zero,
                context,
                interceptor,
                associatedStorage,
                blockNumber,
                timestamp,
                coinbase,
                chainId);
        }

        private async Task SimulateWithTransactionExecutorAsync(
            ExecutionStateService executionState,
            string from,
            string to,
            byte[] data,
            BigInteger value,
            ERC7562ValidationContext context,
            ERC7562TracingInterceptor interceptor,
            AssociatedStorageCalculator associatedStorage,
            long blockNumber,
            long timestamp,
            string coinbase,
            BigInteger chainId)
        {
            var code = await executionState.GetCodeAsync(to);
            if (code == null || code.Length == 0)
            {
                interceptor.OnExtCodeAccess(Instruction.EXTCODESIZE, to, false);
                return;
            }

            var senderBalance = await _nodeDataService.GetBalanceAsync(from);
            executionState.SetInitialChainBalance(from, senderBalance);

            var txContext = new TransactionExecutionContext
            {
                Sender = from,
                To = to,
                Data = data,
                Value = value,
                GasLimit = 10_000_000,
                GasPrice = 1,
                MaxFeePerGas = 1,
                MaxPriorityFeePerGas = 0,
                Nonce = 0,
                IsEip1559 = true,
                IsContractCreation = false,
                BlockNumber = blockNumber > 0 ? blockNumber : 1,
                Timestamp = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = coinbase ?? "0x0000000000000000000000000000000000000000",
                BaseFee = 1,
                Difficulty = 0,
                BlockGasLimit = 30_000_000,
                ChainId = chainId > 0 ? chainId : 1,
                ExecutionState = executionState,
                TraceEnabled = true
            };

            var evmResult = await _executor.ExecuteAsync(txContext);

            if (evmResult.Traces != null)
            {
                foreach (var trace in evmResult.Traces)
                {
                    var instruction = trace.Instruction?.Instruction;
                    var opcode = instruction ?? Instruction.STOP;
                    interceptor.OnOpcodeExecution(
                        opcode,
                        trace.ProgramAddress,
                        trace.Depth,
                        trace.Instruction?.Step ?? 0);

                    ProcessTraceForStorageAndCalls(trace, context, interceptor, associatedStorage);
                }
            }

            if (evmResult.InnerCalls != null)
            {
                foreach (var call in evmResult.InnerCalls)
                {
                    if (!string.IsNullOrEmpty(call.To))
                    {
                        context.AccessedAddresses.Add(call.To.ToLowerInvariant());
                    }
                }
            }
        }

        private void ProcessTraceForStorageAndCalls(
            ProgramTrace trace,
            ERC7562ValidationContext context,
            ERC7562TracingInterceptor interceptor,
            AssociatedStorageCalculator associatedStorage)
        {
            var instruction = trace.Instruction?.Instruction;
            var opcode = instruction ?? Instruction.STOP;

            switch (opcode)
            {
                case Instruction.SLOAD:
                case Instruction.SSTORE:
                    if (trace.Storage != null)
                    {
                        foreach (var kvp in trace.Storage)
                        {
                            var slot = kvp.Key.HexToBigInteger(false);
                            var isWrite = opcode == Instruction.SSTORE;

                            var senderAddr = context.Sender?.Address ?? "";
                            if (associatedStorage.IsAssociatedSlot(trace.ProgramAddress, slot, senderAddr))
                            {
                                context.TrackAssociatedSlot(trace.ProgramAddress, slot);
                            }

                            interceptor.OnStorageAccess(trace.ProgramAddress, slot, isWrite, false);
                        }
                    }
                    break;

                case Instruction.TLOAD:
                case Instruction.TSTORE:
                    if (trace.Stack != null && trace.Stack.Count > 0)
                    {
                        var slot = trace.Stack[0].HexToBigInteger(false);
                        var isWrite = opcode == Instruction.TSTORE;
                        interceptor.OnStorageAccess(trace.ProgramAddress, slot, isWrite, true);
                    }
                    break;

                case Instruction.CALL:
                case Instruction.STATICCALL:
                case Instruction.DELEGATECALL:
                case Instruction.CALLCODE:
                    if (trace.Stack != null && trace.Stack.Count >= 2)
                    {
                        var stackValue = trace.Stack[1].RemoveHexPrefix();
                        var targetAddr = "0x" + stackValue.PadLeft(40, '0');
                        if (targetAddr.Length > 42)
                        {
                            targetAddr = "0x" + targetAddr.Substring(targetAddr.Length - 40);
                        }

                        BigInteger callValue = BigInteger.Zero;
                        if (opcode == Instruction.CALL && trace.Stack.Count >= 3)
                        {
                            callValue = trace.Stack[2].HexToBigInteger(false);
                        }

                        interceptor.OnCall(trace.ProgramAddress, targetAddr, callValue, null, trace.Depth);
                    }
                    break;

                case Instruction.CREATE:
                case Instruction.CREATE2:
                    interceptor.OnCreate(trace.ProgramAddress, null, opcode == Instruction.CREATE2);
                    break;

                case Instruction.KECCAK256:
                    if (trace.Memory != null && trace.Stack != null && trace.Stack.Count >= 2)
                    {
                        var offset = (int)trace.Stack[0].HexToBigInteger(false);
                        var size = (int)trace.Stack[1].HexToBigInteger(false);

                        if (offset >= 0 && size > 0 && offset + size <= trace.Memory.Length / 2)
                        {
                            var memoryHex = trace.Memory.RemoveHexPrefix();
                            var inputHex = memoryHex.Substring(offset * 2, size * 2);
                            var input = inputHex.HexToByteArray();

                            if (trace.Stack.Count > 2)
                            {
                                var resultHash = trace.Stack[2].HexToBigInteger(false);
                                associatedStorage.TrackKeccakFromHash(input, resultHash);
                                interceptor.OnKeccak256(input, resultHash);
                            }
                        }
                    }
                    break;
            }
        }

        private byte[] BuildValidateUserOpCallData(string selector, PackedUserOperationDTO userOp)
        {
            return selector.HexToByteArray();
        }

        private byte[] BuildValidatePaymasterCallData(string selector, PackedUserOperationDTO userOp)
        {
            return selector.HexToByteArray();
        }
    }

    public class PackedUserOperationDTO
    {
        public string Sender { get; set; }
        public BigInteger Nonce { get; set; }
        public byte[] InitCode { get; set; }
        public byte[] CallData { get; set; }
        public byte[] AccountGasLimits { get; set; }
        public BigInteger PreVerificationGas { get; set; }
        public byte[] GasFees { get; set; }
        public byte[] PaymasterAndData { get; set; }
        public byte[] Signature { get; set; }
    }
}
