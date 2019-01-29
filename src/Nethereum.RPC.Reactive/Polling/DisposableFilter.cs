using System;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.RPC.Reactive.Polling
{
    internal sealed class DisposableFilter : IDisposable
    {
        public readonly HexBigInteger ID;

        private readonly IEthUninstallFilter UninstallFilter;

        private bool disposed;

        public DisposableFilter(
            HexBigInteger id,
            IEthUninstallFilter uninstallFilter)
        {
            ID = id;
            UninstallFilter = uninstallFilter;

            disposed = false;
        }

        public void Dispose()
        {
            if (disposed) return;

            // Should be async disposable, but not yet possible until C# 8.
            var _ = UninstallFilter.SendRequestAsync(ID).Result;

            disposed = true;
        }
    }
}