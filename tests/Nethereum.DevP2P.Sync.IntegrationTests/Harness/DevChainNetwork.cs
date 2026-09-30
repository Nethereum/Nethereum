using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    public static class DevChainNetwork
    {
        public static async Task<(DevChainNode A, DevChainNode B)> TwoConnectedNodesAsync(
            CancellationToken ct = default)
        {
            var a = DevChainNode.Create();
            var b = DevChainNode.Create();
            await a.StartAsync(ct).ConfigureAwait(false);
            await b.StartAsync(ct).ConfigureAwait(false);
            await a.ConnectToAsync(b, ct).ConfigureAwait(false);
            await b.ConnectToAsync(a, ct).ConfigureAwait(false);
            return (a, b);
        }

        public static async Task<(DevChainNode A, DevChainNode B, DevChainNode C)> HubWithTwoPeersAsync(
            CancellationToken ct = default)
        {
            var a = DevChainNode.Create();
            var b = DevChainNode.Create();
            var c = DevChainNode.Create();
            await a.StartAsync(ct).ConfigureAwait(false);
            await b.StartAsync(ct).ConfigureAwait(false);
            await c.StartAsync(ct).ConfigureAwait(false);
            await a.ConnectToAsync(b, ct).ConfigureAwait(false);
            await a.ConnectToAsync(c, ct).ConfigureAwait(false);
            return (a, b, c);
        }

        public static async Task<(DevChainNode A, DevChainNode B)> TwoTrustedClusterNodesAsync(
            CancellationToken ct = default)
        {
            var nodes = await TrustedClusterAsync(2, ct).ConfigureAwait(false);
            return (nodes[0], nodes[1]);
        }

        public static async Task<IReadOnlyList<DevChainNode>> TrustedClusterAsync(
            int size, CancellationToken ct = default)
        {
            if (size < 2) throw new ArgumentOutOfRangeException(nameof(size), size, "a cluster needs at least two nodes");

            var keys = Enumerable.Range(0, size).Select(_ => EthECKey.GenerateKey()).ToList();
            var nodeIds = keys.Select(DevChainNode.NodeIdOf).ToList();

            var nodes = keys
                .Select((key, index) => DevChainNode.CreateClusterSibling(
                    key,
                    nodeIds.Where((_, other) => other != index).ToArray()))
                .ToList();

            foreach (var node in nodes)
                await node.StartAsync(ct).ConfigureAwait(false);

            foreach (var node in nodes)
                foreach (var peer in nodes.Where(candidate => !ReferenceEquals(candidate, node)))
                    await node.ConnectToAsync(peer, ct).ConfigureAwait(false);

            return nodes;
        }

        public static async Task<IReadOnlyList<DevChainNode>> ConfiguredClusterAsync(
            int size, CancellationToken ct = default)
        {
            if (size < 2) throw new ArgumentOutOfRangeException(nameof(size), size, "a cluster needs at least two nodes");

            var keys = Enumerable.Range(0, size).Select(_ => EthECKey.GenerateKey()).ToList();
            var ports = ReserveFreeLoopbackPorts(size);
            var enodes = keys.Select((key, index) => DevChainNode.EnodeOf(key, ports[index])).ToList();

            var nodes = keys
                .Select((key, index) => DevChainNode.CreateConfigured(
                    key,
                    ports[index],
                    trustedPeers: enodes.Where((_, other) => other != index).ToArray()))
                .ToList();

            foreach (var node in nodes)
                await node.StartAsync(ct).ConfigureAwait(false);

            return nodes;
        }

        private static IReadOnlyList<int> ReserveFreeLoopbackPorts(int count)
        {
            var ports = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                ports.Add(((IPEndPoint)listener.LocalEndpoint).Port);
                listener.Stop();
            }
            return ports;
        }

        public static async Task<bool> WaitUntilAsync(
            Func<bool> condition, TimeSpan timeout, CancellationToken ct = default)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (ct.IsCancellationRequested) break;
                if (condition()) return true;
                try { await Task.Delay(50, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            return condition();
        }

        public static async Task<bool> WaitUntilAsync(
            Func<Task<bool>> condition, TimeSpan timeout, CancellationToken ct = default)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (ct.IsCancellationRequested) break;
                if (await condition().ConfigureAwait(false)) return true;
                try { await Task.Delay(50, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            return await condition().ConfigureAwait(false);
        }

        public static async Task<bool> StaysFalseAsync(
            Func<bool> condition, TimeSpan window, CancellationToken ct = default)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            var deadline = DateTime.UtcNow + window;
            while (DateTime.UtcNow < deadline)
            {
                if (ct.IsCancellationRequested) break;
                if (condition()) return false;
                try { await Task.Delay(50, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            return !condition();
        }
    }
}
