using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public sealed class NoRewardPolicy : IRewardPolicy
    {
        public static readonly NoRewardPolicy Instance = new();

        public Task<BigInteger> ApplyAsync(
            BlockHeader header,
            IList<BlockHeader> uncles,
            IStateStore stateStore,
            HardforkName fork,
            CancellationToken ct) => Task.FromResult(BigInteger.Zero);
    }
}
