using System;
using System.Collections.Generic;

namespace Nethereum.MainnetChain.Hosting
{
    internal sealed class ExecutionResourceSet : IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<IDisposable> _resources = new List<IDisposable>();

        public void Add(IDisposable? resource)
        {
            if (resource == null) return;
            lock (_gate) _resources.Add(resource);
        }

        public void Dispose()
        {
            IDisposable[] snapshot;
            lock (_gate)
            {
                snapshot = _resources.ToArray();
                _resources.Clear();
            }
            foreach (var resource in snapshot)
            {
                try { resource.Dispose(); } catch { }
            }
        }
    }
}
