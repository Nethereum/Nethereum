using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Nethereum.RPC.Reactive.Polling
{
    internal static class ObservableExtensions
    {
        internal static IObservable<TResult> Using<TResult, TResource>(
            Func<Task<TResource>> resourceFactory,
            Func<TResource, IObservable<TResult>> observableFactory) where TResource : IDisposable =>
            Observable
                .FromAsync(resourceFactory)
                .SelectMany(resource => Observable.Using(
                    () => resource,
                    observableFactory));
    }
}