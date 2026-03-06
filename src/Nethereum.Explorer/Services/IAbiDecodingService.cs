namespace Nethereum.Explorer.Services;

public interface IAbiDecodingService
{
    Task<DecodedFunctionCall?> DecodeFunctionInputAsync(string contractAddress, string inputData);
    Task<List<DecodedEventLog>> DecodeEventLogsAsync(string contractAddress, IEnumerable<RawEventLog> logs);
    Task<DecodedError?> DecodeErrorAsync(string contractAddress, string outputData);
}
