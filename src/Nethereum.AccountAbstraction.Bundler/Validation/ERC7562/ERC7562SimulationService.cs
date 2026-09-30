using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Interfaces.IPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class ERC7562SimulationService
    {
        private readonly IStateReader _nodeDataService;
        private readonly TransactionExecutor _executor;
        private readonly HardforkConfig _hardforkConfig;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string?> _senderCreatorCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        private static readonly Sha3Keccack _keccak = new Sha3Keccack();

        public ERC7562SimulationService(IStateReader nodeDataService, HardforkConfig hardforkConfig)
        {
            _nodeDataService = nodeDataService ?? throw new ArgumentNullException(nameof(nodeDataService));
            _hardforkConfig = hardforkConfig ?? throw new ArgumentNullException(nameof(hardforkConfig));
            _executor = new TransactionExecutor(_hardforkConfig);
        }

        [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "ERC7562SimulationService.ValidateUserOperationAsync - simulate validation and collect rule violations")]
        public async Task<ERC7562ValidationResult> ValidateUserOperationAsync(
            PackedUserOperationDTO userOp,
            string entryPointAddress,
            Erc4337Entity sender,
            Erc4337Entity factory = null,
            Erc4337Entity paymaster = null,
            Erc4337Entity aggregator = null,
            long blockNumber = -1,
            long timestamp = -1,
            string coinbase = null,
            BigInteger chainId = default,
            Authorisation eip7702Auth = null)
        {
            var context = ERC7562ValidationContext.Create(
                entryPointAddress,
                sender,
                factory,
                paymaster,
                aggregator);

            var interceptor = new ERC7562TracingInterceptor(
                context, _hardforkConfig.Precompiles?.GetAddresses());
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
                await SimulateValidationPhaseAsync(userOp, entryPointAddress, context, interceptor, associatedStorage, blockNumber, timestamp, coinbase, chainId, eip7702Auth);
            }
            catch (Exception ex)
            {
                context.AddViolation("SIMULATION_ERROR", $"Simulation failed: {ex.Message}");
            }

            interceptor.FinalizeValidation();
            return interceptor.GetResult();
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
            BigInteger chainId,
            Authorisation eip7702Auth = null)
        {
            var executionState = new ExecutionStateService(_nodeDataService);

            if (eip7702Auth != null && !string.IsNullOrEmpty(eip7702Auth.Address))
            {
                executionState.SaveCode(
                    userOp.Sender,
                    Eip7702DelegationUtils.CreateDelegationCode(eip7702Auth.Address));
            }

            if (context.Factory != null && !string.IsNullOrEmpty(context.Factory.Address))
            {
                context.CurrentEntity = EntityType.Factory;
                context.IsDeploymentPhase = true;

                var initCode = userOp.InitCode;
                TransactionExecutionResult? factoryResult = null;
                string? factoryAddress = null;

                if (initCode != null && initCode.Length >= 20)
                {
                    factoryAddress = "0x" + initCode.Take(20).ToArray().ToHex();
                    var factoryData = initCode.Skip(20).ToArray();

                    var senderCreatorAddress = await ResolveSenderCreatorAddressAsync(
                        executionState, entryPointAddress, blockNumber, timestamp, coinbase, chainId);
                    var factoryCaller = senderCreatorAddress ?? entryPointAddress;

                    factoryResult = await SimulateWithTransactionExecutorAsync(
                        executionState,
                        factoryCaller,
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

                // "touching the sender's address before it has code" window (e.g. the sender
                // noise). ERC-7562's STO-021/022 "deployment phase" gate is a SEPARATE,
                context.IsDeploymentPhase = false;

                if (factoryResult != null && !factoryResult.Success)
                {
                    context.AddViolation(
                        "AA13",
                        $"initCode failed to deploy sender: factory {factoryAddress} reverted " +
                        $"({factoryResult.RevertReason ?? factoryResult.Error ?? "no revert reason"})",
                        factoryAddress);
                    return;
                }
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

            var callData = BuildValidateUserOpCallData(userOp, entryPointAddress, chainId);

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

            var callData = BuildValidatePaymasterCallData(userOp, entryPointAddress, chainId);

            var paymasterResult = await SimulateWithTransactionExecutorAsync(
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

            CheckErep050UnstakedPaymasterContext(paymasterResult, context);
        }

        private static void CheckErep050UnstakedPaymasterContext(
            TransactionExecutionResult? paymasterResult,
            ERC7562ValidationContext context)
        {
            if (paymasterResult == null || !paymasterResult.Success || paymasterResult.ReturnData == null)
            {
                return;
            }

            if (context.Paymaster?.IsStaked == true)
            {
                return;
            }

            byte[] returnedContext;
            try
            {
                returnedContext = new FunctionCallDecoder()
                    .DecodeFunctionOutput(new ValidatePaymasterUserOpOutputDTO(), paymasterResult.ReturnData.ToHex(true))
                    .Context;
            }
            catch
            {
                return;
            }

            if (returnedContext != null && returnedContext.Length > 0)
            {
                context.AddViolation(
                    "EREP-050",
                    "unstaked paymaster returned a context",
                    context.Paymaster?.Address);
            }
        }

        private async Task<TransactionExecutionResult?> SimulateWithTransactionExecutorAsync(
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
                return null;
            }

            var senderBalance = await _nodeDataService.GetBalanceAsync(from);
            executionState.SetInitialChainBalance(from, senderBalance);

            var txContext = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = from,
                To = to,
                Data = data,
                Value = EvmUInt256BigIntegerExtensions.FromBigInteger(value),
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
                ChainId = EvmUInt256BigIntegerExtensions.FromBigInteger(chainId > 0 ? chainId : 1),
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

                    if (trace.OutOfGas)
                    {
                        interceptor.OnOutOfGas();
                    }

                    await ProcessTraceForStorageAndCallsAsync(trace, context, interceptor, associatedStorage, executionState);
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

            return evmResult;
        }

        private async Task<string?> ResolveSenderCreatorAddressAsync(
            ExecutionStateService executionState,
            string entryPointAddress,
            long blockNumber,
            long timestamp,
            string coinbase,
            BigInteger chainId)
        {
            if (_senderCreatorCache.TryGetValue(entryPointAddress, out var cached))
            {
                return cached;
            }

            string? resolved = null;
            try
            {
                var entryPointCode = await executionState.GetCodeAsync(entryPointAddress);
                if (entryPointCode != null && entryPointCode.Length > 0)
                {
                    var callData = new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.SenderCreatorFunction().GetCallData();

                    var txContext = new TransactionExecutionContext
                    {
                        Mode = ExecutionMode.Call,
                        Sender = entryPointAddress,
                        To = entryPointAddress,
                        Data = callData,
                        Value = EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Zero),
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
                        ChainId = EvmUInt256BigIntegerExtensions.FromBigInteger(chainId > 0 ? chainId : 1),
                        ExecutionState = executionState,
                        TraceEnabled = false
                    };

                    var result = await _executor.ExecuteAsync(txContext);
                    if (result.Success && result.ReturnData != null && result.ReturnData.Length >= 20)
                    {
                        resolved = DecodeAddressFromReturnData(result.ReturnData);
                    }
                }
            }
            catch
            {
                resolved = null;
            }

            _senderCreatorCache[entryPointAddress] = resolved;
            return resolved;
        }

        private static string DecodeAddressFromReturnData(byte[] returnData)
        {
            var data = returnData ?? Array.Empty<byte>();
            var addressBytes = data.Skip(Math.Max(0, data.Length - 20)).Take(20).ToArray();
            return "0x" + addressBytes.ToHex();
        }

        private static async Task<bool> HasCodeAsync(ExecutionStateService executionState, string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            var code = await executionState.GetCodeReadOnlyAsync(address);
            return code != null && code.Length > 0;
        }

        private static string NormalizeStackAddress(string stackValueHex)
        {
            var stackValue = stackValueHex.RemoveHexPrefix();
            var address = "0x" + stackValue.PadLeft(40, '0');
            if (address.Length > 42)
            {
                address = "0x" + address.Substring(address.Length - 40);
            }
            return address;
        }

        private async Task ProcessTraceForStorageAndCallsAsync(
            ProgramTrace trace,
            ERC7562ValidationContext context,
            ERC7562TracingInterceptor interceptor,
            AssociatedStorageCalculator associatedStorage,
            ExecutionStateService executionState)
        {
            var instruction = trace.Instruction?.Instruction;
            var opcode = instruction ?? Instruction.STOP;

            switch (opcode)
            {
                case Instruction.SLOAD:
                case Instruction.SSTORE:
                    if (trace.Stack != null && trace.Stack.Count > 0)
                    {
                        var slot = trace.Stack[0].HexToBigInteger(false);
                        var isWrite = opcode == Instruction.SSTORE;

                        var senderAddr = context.Sender?.Address ?? "";
                        if (associatedStorage.IsAssociatedSlot(trace.ProgramAddress, slot, senderAddr))
                        {
                            context.TrackAssociatedSlot(trace.ProgramAddress, slot);
                        }

                        if (context.CurrentEntity != EntityType.Sender)
                        {
                            var entityOwnAddr = context.GetCurrentEntity()?.Address ?? "";
                            if (!string.IsNullOrEmpty(entityOwnAddr) &&
                                associatedStorage.IsAssociatedSlot(trace.ProgramAddress, slot, entityOwnAddr))
                            {
                                context.TrackEntityOwnAssociatedSlot(trace.ProgramAddress, slot);
                            }
                        }

                        interceptor.OnStorageAccess(trace.ProgramAddress, slot, isWrite, false);
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
                        var targetAddr = NormalizeStackAddress(trace.Stack[1]);

                        var hasValueOperand = opcode == Instruction.CALL || opcode == Instruction.CALLCODE;
                        var argsOffsetIndex = hasValueOperand ? 3 : 2;
                        var argsSizeIndex = hasValueOperand ? 4 : 3;

                        BigInteger callValue = BigInteger.Zero;
                        if (hasValueOperand && trace.Stack.Count > 2)
                        {
                            callValue = trace.Stack[2].HexToBigInteger(false);
                        }

                        byte[] callData = null;
                        if (ERC7562ValidationContext.AddressEquals(targetAddr, context.EntryPointAddress) &&
                            trace.Stack.Count > argsSizeIndex && trace.Memory != null)
                        {
                            var argsOffsetBig = trace.Stack[argsOffsetIndex].HexToBigInteger(false);
                            var argsSizeBig = trace.Stack[argsSizeIndex].HexToBigInteger(false);

                            if (argsOffsetBig >= 0 && argsOffsetBig <= int.MaxValue &&
                                argsSizeBig > 0 && argsSizeBig <= int.MaxValue)
                            {
                                var argsOffset = (int)argsOffsetBig;
                                var argsSize = (int)argsSizeBig;
                                var memoryHex = trace.Memory.RemoveHexPrefix();

                                if (argsOffset + argsSize <= memoryHex.Length / 2)
                                {
                                    var inputHex = memoryHex.Substring(argsOffset * 2, argsSize * 2);
                                    callData = inputHex.HexToByteArray();
                                }
                            }
                        }

                        var targetHasCode = await HasCodeAsync(executionState, targetAddr);

                        interceptor.OnCall(trace.ProgramAddress, targetAddr, callValue, callData, trace.Depth, targetHasCode);
                    }
                    break;

                case Instruction.EXTCODESIZE:
                case Instruction.EXTCODEHASH:
                    if (trace.Stack != null && trace.Stack.Count >= 1)
                    {
                        var targetAddr = NormalizeStackAddress(trace.Stack[0]);
                        var hasCode = await HasCodeAsync(executionState, targetAddr);
                        interceptor.OnExtCodeAccess(opcode, targetAddr, hasCode);
                    }
                    break;

                case Instruction.EXTCODECOPY:
                    if (trace.Stack != null && trace.Stack.Count >= 1)
                    {
                        var targetAddr = NormalizeStackAddress(trace.Stack[0]);
                        var hasCode = await HasCodeAsync(executionState, targetAddr);
                        interceptor.OnExtCodeAccess(opcode, targetAddr, hasCode);
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

                            var output = _keccak.CalculateHash(input);
                            associatedStorage.TrackKeccak(input, output);
                        }
                    }
                    break;
            }
        }

        private byte[] BuildValidateUserOpCallData(PackedUserOperationDTO userOp, string entryPointAddress, BigInteger chainId)
        {
            var structOp = ToStructPackedUserOperation(userOp);
            var userOpHash = ComputeUserOpHash(structOp, entryPointAddress, chainId);

            var function = new ValidateUserOpFunction
            {
                UserOp = structOp,
                UserOpHash = userOpHash,
                MissingAccountFunds = BigInteger.Zero
            };

            return function.GetCallData();
        }

        private byte[] BuildValidatePaymasterCallData(PackedUserOperationDTO userOp, string entryPointAddress, BigInteger chainId)
        {
            var structOp = ToStructPackedUserOperation(userOp);
            var userOpHash = ComputeUserOpHash(structOp, entryPointAddress, chainId);

            var function = new ValidatePaymasterUserOpFunction
            {
                UserOp = structOp,
                UserOpHash = userOpHash,
                MaxCost = BigInteger.Zero
            };

            return function.GetCallData();
        }

        private static byte[] ComputeUserOpHash(PackedUserOperation structOp, string entryPointAddress, BigInteger chainId)
        {
            var effectiveChainId = chainId > 0 ? chainId : 1;
            return UserOperationBuilder.HashUserOperation(structOp, entryPointAddress, effectiveChainId);
        }

        private static PackedUserOperation ToStructPackedUserOperation(PackedUserOperationDTO userOp)
        {
            return new PackedUserOperation
            {
                Sender = userOp.Sender,
                Nonce = userOp.Nonce,
                InitCode = userOp.InitCode ?? Array.Empty<byte>(),
                CallData = userOp.CallData ?? Array.Empty<byte>(),
                AccountGasLimits = userOp.AccountGasLimits ?? new byte[32],
                PreVerificationGas = userOp.PreVerificationGas,
                GasFees = userOp.GasFees ?? new byte[32],
                PaymasterAndData = userOp.PaymasterAndData ?? Array.Empty<byte>(),
                Signature = userOp.Signature ?? Array.Empty<byte>()
            };
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
