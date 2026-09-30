using System;
using Nethereum.CoreChain;

namespace Nethereum.MainnetChain.Hosting
{
    public sealed class MainnetChainNodeAccessor
    {
        private volatile FollowerChainNode _node;

        public FollowerChainNode Node =>
            _node ?? throw new InvalidOperationException(
                "FollowerChainNode has not been set yet — MainnetChainHostedService must initialise it before RPC requests are dispatched.");

        public void Set(FollowerChainNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public bool HasValue => _node != null;
    }
}
