using System;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
[assembly: InternalsVisibleTo("Nethereum.RPC.Reactive.UnitTests")]

namespace Nethereum.RPC.Reactive.Polling
{
    internal static class ObservableExtensions
    {
        /// <summary>
        /// The same as the <see cref="Observable" /> using factory method,
        /// except the resource can be created asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The type of the elements in the produced sequence.</typeparam>
        /// <typeparam name="TResource">
        /// The type of the resource used during the generation of the resulting sequence.
        /// Needs to implement System.IDisposable.
        /// </typeparam>
        /// <param name="resourceFactory">Asynchronous factory function to obtain a resource object.</param>
        /// <param name="observableFactory">
        /// Factory function to obtain an observable sequence that depends on the obtained
        /// resource.
        /// </param>
        /// <returns>
        /// An observable sequence whose lifetime controls the lifetime of the dependent
        /// resource object
        /// </returns>
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