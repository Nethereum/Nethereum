using Nethereum.EVM.Debugging;

namespace Nethereum.Blazor.Solidity.Services;

public class EvmReplayResult
{
    public EVMDebuggerSession? Session { get; set; }
    public int TotalSteps { get; set; }
    public bool IsRevert { get; set; }
    public bool HasSourceMaps { get; set; }
    public string? Error { get; set; }
    public List<string> SourceFiles { get; set; } = new();
    public Dictionary<string, string> FileContents { get; set; } = new();
}
