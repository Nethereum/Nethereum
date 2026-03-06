using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;

namespace Nethereum.Explorer.Services;

public class AbiDecodingService : IAbiDecodingService
{
    private readonly IAbiStorageService _storage;
    private readonly ILogger<AbiDecodingService> _logger;

    private static readonly ErrorABI StandardRevertErrorABI = new("Error")
    {
        InputParameters = new[] { new Parameter("string", "message", 1) }
    };

    private static readonly ErrorABI StandardPanicErrorABI = new("Panic")
    {
        InputParameters = new[] { new Parameter("uint256", "code", 1) }
    };

    public AbiDecodingService(IAbiStorageService storage, ILogger<AbiDecodingService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task<DecodedFunctionCall?> DecodeFunctionInputAsync(string contractAddress, string inputData)
    {
        if (string.IsNullOrEmpty(inputData) || inputData == "0x" || inputData.Length < 10)
            return null;

        var abiInfo = await _storage.GetContractAbiAsync(contractAddress);
        if (abiInfo?.ContractABI == null) return null;

        var functionAbi = abiInfo.ContractABI.FindFunctionABIFromInputData(inputData);
        if (functionAbi == null) return null;

        try
        {
            var decoded = functionAbi.DecodeInputDataToDefault(inputData);
            return new DecodedFunctionCall
            {
                FunctionName = functionAbi.Name,
                Signature = functionAbi.Sha3Signature,
                Parameters = decoded
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to decode function input for {Address}, selector {Selector}", contractAddress, inputData[..10]);
            return new DecodedFunctionCall
            {
                FunctionName = functionAbi.Name,
                Signature = functionAbi.Sha3Signature,
                Parameters = new List<ParameterOutput>()
            };
        }
    }

    public async Task<List<DecodedEventLog>> DecodeEventLogsAsync(string contractAddress, IEnumerable<RawEventLog> logs)
    {
        var result = new List<DecodedEventLog>();
        var abiInfo = await _storage.GetContractAbiAsync(contractAddress);

        foreach (var log in logs)
        {
            var decoded = new DecodedEventLog
            {
                LogIndex = log.LogIndex,
                Address = log.Address
            };

            if (abiInfo?.ContractABI != null && !string.IsNullOrEmpty(log.EventHash))
            {
                var eventAbi = abiInfo.ContractABI.FindEventABI(log.EventHash);
                if (eventAbi != null)
                {
                    decoded.EventName = eventAbi.Name;
                    decoded.Signature = eventAbi.Sha3Signature;
                    decoded.IsDecoded = true;

                    try
                    {
                        var topics = BuildTopicsArray(log);
                        var decoder = new EventTopicDecoder(eventAbi.IsAnonymous);
                        decoded.Parameters = decoder.DecodeDefaultTopics(eventAbi, topics, log.Data ?? "0x");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to decode event log parameters for {EventName} at {Address}", eventAbi.Name, log.Address);
                        decoded.Parameters = new List<ParameterOutput>();
                    }
                }
            }

            result.Add(decoded);
        }

        return result;
    }

    public async Task<DecodedError?> DecodeErrorAsync(string contractAddress, string outputData)
    {
        if (string.IsNullOrEmpty(outputData) || outputData == "0x" || outputData.Length < 10)
            return null;

        try
        {
            if (SignatureEncoder.IsDataForSignature(StandardRevertErrorABI.Sha3Signature, outputData))
            {
                var decoded = StandardRevertErrorABI.DecodeErrorDataToDefault(outputData);
                return new DecodedError
                {
                    ErrorName = "Error",
                    Signature = StandardRevertErrorABI.Sha3Signature,
                    Parameters = decoded,
                    IsStandardRevert = true
                };
            }

            if (SignatureEncoder.IsDataForSignature(StandardPanicErrorABI.Sha3Signature, outputData))
            {
                var decoded = StandardPanicErrorABI.DecodeErrorDataToDefault(outputData);
                return new DecodedError
                {
                    ErrorName = "Panic",
                    Signature = StandardPanicErrorABI.Sha3Signature,
                    Parameters = decoded,
                    IsStandardRevert = true
                };
            }

            if (!string.IsNullOrEmpty(contractAddress))
            {
                var abiInfo = await _storage.GetContractAbiAsync(contractAddress);
                if (abiInfo?.ContractABI != null)
                {
                    var signature = SignatureEncoder.GetSignatureFromData(outputData);
                    var errorAbi = abiInfo.ContractABI.FindErrorABI(signature);
                    if (errorAbi != null)
                    {
                        var decoded = errorAbi.DecodeErrorDataToDefault(outputData);
                        return new DecodedError
                        {
                            ErrorName = errorAbi.Name,
                            Signature = errorAbi.Sha3Signature,
                            Parameters = decoded,
                            IsStandardRevert = false
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to decode error output for {Address}", contractAddress);
        }

        return null;
    }

    private static object[] BuildTopicsArray(RawEventLog log)
    {
        var topics = new List<object>();
        if (!string.IsNullOrEmpty(log.EventHash)) topics.Add(log.EventHash);
        if (!string.IsNullOrEmpty(log.IndexVal1)) topics.Add(log.IndexVal1);
        if (!string.IsNullOrEmpty(log.IndexVal2)) topics.Add(log.IndexVal2);
        if (!string.IsNullOrEmpty(log.IndexVal3)) topics.Add(log.IndexVal3);
        return topics.ToArray();
    }
}
