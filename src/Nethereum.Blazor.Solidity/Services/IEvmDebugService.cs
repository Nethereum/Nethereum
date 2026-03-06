namespace Nethereum.Blazor.Solidity.Services;

public interface IEvmDebugService
{
    bool IsAvailable { get; }
    Task<EvmReplayResult> ReplayTransactionAsync(string txHash);
}
