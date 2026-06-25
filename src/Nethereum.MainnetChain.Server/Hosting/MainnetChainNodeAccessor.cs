using System;
using Nethereum.CoreChain;

namespace Nethereum.MainnetChain.Server.Hosting
{
    /// <summary>
    /// Singleton holder for the <see cref="MainnetChainNode"/> instance owned by
    /// <see cref="MainnetChainHostedService"/>. Allows the RPC composition root
    /// to consume the same node instance the follower loop runs, without
    /// constructing a second one at DI build time.
    /// </summary>
    public sealed class MainnetChainNodeAccessor
    {
        private MainnetChainNode _node;

        public MainnetChainNode Node =>
            _node ?? throw new InvalidOperationException(
                "MainnetChainNode has not been set yet — MainnetChainHostedService must initialise it before RPC requests are dispatched.");

        public void Set(MainnetChainNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public bool HasValue => _node != null;
    }
}
