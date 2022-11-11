using System;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Nethereum.RPC.Reactive.UnitTests,PublicKey=" + "0024000004800000940000000602000000240000525341310004000001000100d90181381ce37f"
                                                                            + "cd30d5dcbea4eeb9665a845853878b90278cecf8d94965b49c2dfea39e67f397c29719fb6b130d"
                                                                            + "b7d23d1fe3639650974c1013c6f18d02a41b820398561cf9b41c923f9f2bbc7efe314e9d36c610"
                                                                            + "7df2c31658cd4efce0f9e7ff4a41105b61eb999861cff4f1951b0ff62dc1d707c2b82c1ef8ee63"
                                                                            + "5cfbc4b6")]
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