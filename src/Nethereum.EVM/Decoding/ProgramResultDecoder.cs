using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.EVM.Compatibility;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.EVM.Decoding
{
    public class ProgramResultDecoder
    {
        private readonly IABIInfoStorage _abiStorage;
        private readonly FunctionCallDecoder _functionDecoder;
        private readonly EventTopicDecoder _eventDecoder;

        public ProgramResultDecoder(IABIInfoStorage abiStorage)
        {
            _abiStorage = abiStorage ?? throw new ArgumentNullException(nameof(abiStorage));
            _functionDecoder = new FunctionCallDecoder();
            _eventDecoder = new EventTopicDecoder();
        }

        public DecodedProgramResult Decode(
            Program program,
            CallInput initialCall,
            BigInteger chainId)
        {
            return Decode(program.ProgramResult, program.Trace, initialCall, chainId);
        }

        public DecodedProgramResult Decode(
            TransactionExecutionResult executionResult,
            CallInput initialCall,
            BigInteger chainId)
        {
            if (executionResult.ProgramResult != null)
            {
                return Decode(executionResult.ProgramResult, executionResult.Traces, initialCall, chainId);
            }

            return Decode(BuildProgramResultWhenNoFrameRan(executionResult), executionResult.Traces, initialCall, chainId);
        }

        private static ProgramResult BuildProgramResultWhenNoFrameRan(TransactionExecutionResult executionResult)
        {
            return new ProgramResult
            {
                Result = executionResult.ReturnData,
                IsRevert = !executionResult.Success,
                Logs = executionResult.Logs ?? new List<RPC.Eth.DTOs.FilterLog>(),
                InnerCalls = executionResult.InnerCalls ?? new List<Types.EvmCallContext>(),
                CreatedContractAccounts = executionResult.CreatedAccounts ?? new List<string>()
            };
        }

        public DecodedProgramResult Decode(
            ProgramResult programResult,
            List<ProgramTrace> trace,
            CallInput initialCall,
            BigInteger chainId)
        {
            var result = new DecodedProgramResult
            {
                OriginalResult = programResult,
                OriginalCall = initialCall,
                ChainId = chainId,
                IsRevert = programResult.IsRevert
            };

            result.RootCall = DecodeCall(initialCall, chainId, 0);
            AddDecodedInnerCalls(result, programResult, chainId);
            AddDecodedLogs(result, programResult, chainId);
            AssignRevertReasonOrReturnValue(result, programResult, initialCall, chainId);

            return result;
        }

        private void AddDecodedInnerCalls(DecodedProgramResult result, ProgramResult programResult, BigInteger chainId)
        {
            foreach (var innerCall in programResult.InnerCalls)
            {
                var decodedInnerCall = DecodeCall(innerCall.ToCallInput(), chainId, 1);
                result.RootCall.InnerCalls.Add(decodedInnerCall);
            }
        }

        private void AddDecodedLogs(DecodedProgramResult result, ProgramResult programResult, BigInteger chainId)
        {
            foreach (var log in programResult.Logs)
            {
                var decodedLog = DecodeLog(log, chainId);
                result.DecodedLogs.Add(decodedLog);
            }
        }

        private void AssignRevertReasonOrReturnValue(
            DecodedProgramResult result, ProgramResult programResult, CallInput initialCall, BigInteger chainId)
        {
            if (programResult.IsRevert)
            {
                result.RevertReason = DecodeRevert(programResult.Result, chainId, initialCall.To);
            }
            else if (programResult.Result != null && programResult.Result.Length > 0)
            {
                result.ReturnValue = DecodeReturnValue(
                    result.RootCall.Function,
                    programResult.Result.ToHex(true));
            }
        }

        public DecodedCall DecodeCall(CallInput call, BigInteger chainId, int depth)
        {
            var decodedCall = new DecodedCall
            {
                From = call.From,
                To = call.To,
                RawInput = call.Data,
                Value = call.Value?.Value ?? BigInteger.Zero,
                Depth = depth,
                OriginalCall = call,
                CallType = DetermineCallType(call)
            };

            if (string.IsNullOrEmpty(call.Data) || call.Data.Length < 10)
            {
                decodedCall.IsDecoded = false;
                return decodedCall;
            }

            try
            {
                DescribeCallAgainstKnownFunction(decodedCall, call, chainId);
            }
            catch
            {
                decodedCall.IsDecoded = false;
            }

            return decodedCall;
        }

        private void DescribeCallAgainstKnownFunction(DecodedCall decodedCall, CallInput call, BigInteger chainId)
        {
            var functionABI = _abiStorage?.FindFunctionABIFromInputData(chainId, call.To, call.Data);

            if (functionABI == null)
            {
                decodedCall.IsDecoded = false;
                return;
            }

            decodedCall.Function = functionABI;
            decodedCall.IsDecoded = true;
            decodedCall.ContractName = FindContractName(chainId, call.To);
            decodedCall.InputParameters = DecodedOrEmpty(() => _functionDecoder.DecodeInput(functionABI, call.Data));
        }

        public DecodedLog DecodeLog(FilterLog log, BigInteger chainId)
        {
            var decodedLog = new DecodedLog
            {
                ContractAddress = log.Address,
                OriginalLog = log,
                LogIndex = (int)(log.LogIndex?.Value ?? 0)
            };

            if (log.Topics == null || log.Topics.Length == 0)
            {
                decodedLog.IsDecoded = false;
                return decodedLog;
            }

            try
            {
                DescribeLogAgainstKnownEvent(decodedLog, log, chainId);
            }
            catch
            {
                decodedLog.IsDecoded = false;
            }

            return decodedLog;
        }

        private void DescribeLogAgainstKnownEvent(DecodedLog decodedLog, FilterLog log, BigInteger chainId)
        {
            var eventSignature = log.Topics[0].ToString();
            var eventABI = _abiStorage?.FindEventABI(chainId, log.Address, eventSignature);

            if (eventABI == null)
            {
                decodedLog.IsDecoded = false;
                return;
            }

            decodedLog.Event = eventABI;
            decodedLog.IsDecoded = true;
            decodedLog.ContractName = FindContractName(chainId, log.Address);
            decodedLog.Parameters = DecodedOrEmpty(
                () => _eventDecoder.DecodeDefaultTopics(eventABI, log.Topics, log.Data));
        }

        public DecodedError DecodeRevert(byte[] revertData, BigInteger chainId, string contractAddress)
        {
            if (revertData == null || revertData.Length < 4)
            {
                return DecodedError.FromUnknownError(revertData?.ToHex(true));
            }

            var revertHex = revertData.ToHex(true);

            if (ErrorFunction.IsErrorData(revertHex))
            {
                var errorMessage = _functionDecoder.DecodeFunctionErrorMessage(revertHex);
                return DecodedError.FromStandardError(errorMessage, revertHex);
            }

            return DecodeCustomError(revertHex, chainId, contractAddress)
                ?? DecodedError.FromUnknownError(revertHex);
        }

        private DecodedError DecodeCustomError(string revertHex, BigInteger chainId, string contractAddress)
        {
            try
            {
                var errorSignature = revertHex.Substring(0, 10);
                var errorABI = _abiStorage?.FindErrorABI(chainId, contractAddress, errorSignature);

                if (errorABI == null)
                {
                    return null;
                }

                return new DecodedError
                {
                    Error = errorABI,
                    IsDecoded = true,
                    IsStandardError = false,
                    RawData = revertHex,
                    Parameters = DecodedOrEmpty(() => _functionDecoder.DecodeError(errorABI, revertHex))
                };
            }
            catch
            {
                return null;
            }
        }

        public List<ParameterOutput> DecodeReturnValue(FunctionABI functionABI, string output)
        {
            if (functionABI == null || string.IsNullOrEmpty(output) || output == "0x")
            {
                return new List<ParameterOutput>();
            }

            try
            {
                var outputParams = functionABI.OutputParameters;
                if (outputParams == null || outputParams.Length == 0)
                {
                    return new List<ParameterOutput>();
                }

                var decoder = new ParameterDecoder();
                return decoder.DecodeDefaultData(output, outputParams);
            }
            catch
            {
                return new List<ParameterOutput>();
            }
        }

        private string FindContractName(BigInteger chainId, string address)
        {
            var abiInfo = _abiStorage.GetABIInfo(chainId, address);
            return abiInfo?.ContractName;
        }

        private static List<ParameterOutput> DecodedOrEmpty(Func<List<ParameterOutput>> decode)
        {
            try
            {
                return decode() ?? new List<ParameterOutput>();
            }
            catch
            {
                return new List<ParameterOutput>();
            }
        }

        private static CallType DetermineCallType(CallInput call) =>
            string.IsNullOrEmpty(call.To) ? CallType.Create : CallType.Call;
    }
}
