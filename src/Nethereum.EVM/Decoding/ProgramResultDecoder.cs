using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
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

            // Fallback for when ProgramResult is not available (e.g., precompile-only calls)
            var programResult = new ProgramResult
            {
                Result = executionResult.ReturnData,
                IsRevert = !executionResult.Success,
                Logs = executionResult.Logs ?? new List<RPC.Eth.DTOs.FilterLog>(),
                InnerCalls = executionResult.InnerCalls ?? new List<RPC.Eth.DTOs.CallInput>(),
                CreatedContractAccounts = executionResult.CreatedAccounts ?? new List<string>(),
                DeletedContractAccounts = executionResult.DeletedAccounts ?? new List<string>()
            };
            return Decode(programResult, executionResult.Traces, initialCall, chainId);
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

            foreach (var innerCall in programResult.InnerCalls)
            {
                var decodedInnerCall = DecodeCall(innerCall, chainId, 1);
                result.RootCall.InnerCalls.Add(decodedInnerCall);
            }

            foreach (var log in programResult.Logs)
            {
                var decodedLog = DecodeLog(log, chainId);
                result.DecodedLogs.Add(decodedLog);
            }

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

            return result;
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
                var functionABI = _abiStorage?.FindFunctionABIFromInputData(chainId, call.To, call.Data);

                if (functionABI != null)
                {
                    decodedCall.Function = functionABI;
                    decodedCall.IsDecoded = true;

                    var abiInfo = _abiStorage.GetABIInfo(chainId, call.To);
                    if (abiInfo != null)
                    {
                        decodedCall.ContractName = abiInfo.ContractName;
                    }

                    try
                    {
                        decodedCall.InputParameters = _functionDecoder.DecodeInput(functionABI, call.Data) ?? new List<ParameterOutput>();
                    }
                    catch
                    {
                        decodedCall.InputParameters = new List<ParameterOutput>();
                    }
                }
                else
                {
                    decodedCall.IsDecoded = false;
                }
            }
            catch
            {
                decodedCall.IsDecoded = false;
            }

            return decodedCall;
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
                var eventSignature = log.Topics[0].ToString();
                var eventABI = _abiStorage?.FindEventABI(chainId, log.Address, eventSignature);

                if (eventABI != null)
                {
                    decodedLog.Event = eventABI;
                    decodedLog.IsDecoded = true;

                    var abiInfo = _abiStorage.GetABIInfo(chainId, log.Address);
                    if (abiInfo != null)
                    {
                        decodedLog.ContractName = abiInfo.ContractName;
                    }

                    try
                    {
                        decodedLog.Parameters = _eventDecoder.DecodeDefaultTopics(
                            eventABI,
                            log.Topics,
                            log.Data) ?? new List<ParameterOutput>();
                    }
                    catch
                    {
                        decodedLog.Parameters = new List<ParameterOutput>();
                    }
                }
                else
                {
                    decodedLog.IsDecoded = false;
                }
            }
            catch
            {
                decodedLog.IsDecoded = false;
            }

            return decodedLog;
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

            try
            {
                var errorSignature = revertHex.Substring(0, 10);
                var errorABI = _abiStorage?.FindErrorABI(chainId, contractAddress, errorSignature);

                if (errorABI != null)
                {
                    var decoded = new DecodedError
                    {
                        Error = errorABI,
                        IsDecoded = true,
                        IsStandardError = false,
                        RawData = revertHex
                    };

                    try
                    {
                        decoded.Parameters = _functionDecoder.DecodeError(errorABI, revertHex) ?? new List<ParameterOutput>();
                    }
                    catch
                    {
                        decoded.Parameters = new List<ParameterOutput>();
                    }

                    return decoded;
                }
            }
            catch
            {
            }

            return DecodedError.FromUnknownError(revertHex);
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

        private CallType DetermineCallType(CallInput call)
        {
            if (string.IsNullOrEmpty(call.To))
            {
                return CallType.Create;
            }
            return CallType.Call;
        }
    }
}
