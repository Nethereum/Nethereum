using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Decoding;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.EVM.StateChanges
{
    public interface IStateChangesExtractor
    {
        StateChangesResult ExtractFromDecodedResult(
            DecodedProgramResult decodedResult,
            ExecutionStateService stateService = null,
            string currentUserAddress = null);

        StateChangesResult ExtractFromDecodedResult(
            DecodedProgramResult decodedResult,
            ExecutionStateService stateService,
            string currentUserAddress,
            Func<string, TokenInfo> tokenResolver);

        Task<StateChangesResult> ExtractFromDecodedResultAsync(
            DecodedProgramResult decodedResult,
            ExecutionStateService stateService = null,
            string currentUserAddress = null,
            Func<string, Task<TokenInfo>> tokenResolverAsync = null,
            CancellationToken cancellationToken = default);
    }
}
