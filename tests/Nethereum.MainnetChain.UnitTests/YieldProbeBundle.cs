using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Nethereum.CoreChain.Storage;

namespace Nethereum.MainnetChain.UnitTests
{
    public class YieldProbeBundle : DispatchProxy
    {
        public sealed record ArmCall(bool Armed, bool SnapBootstrapReturned);

        private readonly ConcurrentQueue<ArmCall> _calls = new();

        private IChainStoreBundle _inner = null!;
        private Func<bool> _snapBootstrapReturned = () => false;

        public IReadOnlyList<ArmCall> Calls => _calls.ToArray();

        public static (IChainStoreBundle Bundle, YieldProbeBundle Probe) Wrap(
            IChainStoreBundle inner, Func<bool> snapBootstrapReturned)
        {
            var proxy = Create<IYieldProbe, YieldProbeBundle>();
            var probe = (YieldProbeBundle)(object)proxy;
            probe._inner = inner;
            probe._snapBootstrapReturned = snapBootstrapReturned;
            return (proxy, probe);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;
            if (targetMethod.DeclaringType == typeof(IBackfillPauseControl))
            {
                if (targetMethod.Name == nameof(IBackfillPauseControl.SetYieldToExecution))
                    _calls.Enqueue(new ArmCall((bool)args![0]!, _snapBootstrapReturned()));
                return targetMethod.ReturnType == typeof(bool) ? false : null;
            }
            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        public interface IYieldProbe : IChainStoreBundle, IBackfillPauseControl
        {
        }
    }
}
